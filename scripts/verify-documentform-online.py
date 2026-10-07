"""Local-only HTTP smoke test; creates synthetic records in a dedicated test database/blob container.
Start the new API with local PostgreSQL + Azurite and run: python scripts/verify-documentform-online.py
No third-party Python packages required. Does not run against Azure.
"""
import io
import json
import sys
import uuid
import zipfile
import urllib.request
import urllib.error
import urllib.parse
import xml.etree.ElementTree as ET

base = sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:15014"
assert urllib.parse.urlparse(base).hostname in ("localhost", "127.0.0.1"), "Local API only"
checks = 0


def call(method, path, body=None, expected=200, content_type="application/json", raw=False):
    global checks
    data = body if isinstance(body, bytes) else json.dumps(body).encode() if body is not None else None
    request = urllib.request.Request(base + path, data=data, method=method, headers={"Content-Type": content_type})
    try:
        response = urllib.request.urlopen(request, timeout=45)
    except urllib.error.HTTPError as error:
        response = error
    result = response.read()
    assert response.code == expected, (method, path, response.code, result.decode(errors="replace")[:1500])
    checks += 1
    return result if raw or not result else json.loads(result)


def fixture():
    output = io.BytesIO()
    with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as package:
        package.writestr("[Content_Types].xml", '''<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>''')
        package.writestr("_rels/.rels", '''<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>''')
        package.writestr("word/document.xml", '''<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p><w:r><w:t xml:space="preserve">Ho ten: </w:t></w:r><w:r><w:rPr><w:b/></w:rPr><w:t>...</w:t></w:r><w:r><w:t>...</w:t></w:r></w:p><w:p><w:r><w:t xml:space="preserve">Ngay sinh: ......</w:t></w:r></w:p></w:body></w:document>''')
    return output.getvalue()


def upload(template, data):
    boundary = "wm-" + uuid.uuid4().hex
    body = (f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="original.docx"\r\nContent-Type: application/vnd.openxmlformats-officedocument.wordprocessingml.document\r\n\r\n'.encode()
            + data + f"\r\n--{boundary}--\r\n".encode())
    return call("POST", f"/api/v1/form-templates/{template}/upload-docx", body,
                content_type="multipart/form-data; boundary=" + boundary)


applicant = str(uuid.uuid4())
template = call("POST", "/api/v1/form-templates", {"code": "SMOKE-" + uuid.uuid4().hex[:12], "title": "Synthetic online form"}, 201)["id"]
t = "/api/v1/form-templates/" + template
c = "/api/v1/citizen/submissions"
original = fixture()
upload(template, original)
assert call("GET", t + "/download-docx", raw=True) == original
assert not call("GET", t)["onlineReady"]
structure = call("GET", t + "/docx-structure")
assert structure["paragraphs"][0]["text"] == "Ho ten: ......"
schema = {"title": "Synthetic online form", "sections": [{"section_id": "main", "title": "Details", "fields": [
    {"field_id": "ho_ten", "label": "Ho ten", "type": "text", "is_required": True},
    {"field_id": "ngay_sinh", "label": "Ngay sinh", "type": "date", "is_required": True}]}]}
mappings = [{"fieldId": "ho_ten", "paragraphIndex": 0, "start": 8, "length": 6, "expectedText": "......"},
            {"fieldId": "ngay_sinh", "paragraphIndex": 1, "start": 11, "length": 6, "expectedText": "......"}]
config = {"schemaDefinition": schema, "mappings": mappings, "originalSha256": structure["originalSha256"]}
call("PUT", t + "/online-config", dict(config, originalSha256="wrong"), 409)
version = call("PUT", t + "/online-config", config)["templateVersionId"]
detail = call("GET", t)
assert detail["onlineReady"] and detail["templateVersionId"] == version
assert detail["schemaDefinition"] == schema
draft_body = {"templateId": template, "templateVersionId": version, "applicantId": applicant, "formData": {"ho_ten": "Synthetic Applicant"}}
draft = call("POST", c + "/draft", draft_body, 201)["submissionId"]
d = c + "/" + draft
validation = call("POST", d + "/submit", {"applicantId": applicant}, 400)
assert validation["code"] == "document.invalid_form_data" and validation["fieldErrors"]
call("PUT", d + "/draft", {"applicantId": str(uuid.uuid4()), "formData": {}}, 403)
call("PUT", d + "/draft", {"applicantId": applicant, "formData": {"unexpected": 1}}, 400)
data = {"ho_ten": "Synthetic Applicant", "ngay_sinh": "2000-01-02"}
call("PUT", d + "/draft", {"applicantId": applicant, "formData": data})
reopened = call("GET", d + "?applicantId=" + applicant)
assert reopened["formData"] == data and reopened["schemaDefinition"] == schema
assert reopened["templateVersionId"] == version
call("GET", d + "?applicantId=" + str(uuid.uuid4()), expected=403)
generated = call("GET", d + "/download-docx?applicantId=" + applicant, raw=True)
with zipfile.ZipFile(io.BytesIO(generated)) as package:
    xml = ET.fromstring(package.read("word/document.xml"))
    text = "".join(xml.itertext())
    assert "Synthetic Applicant" in text and "2000-01-02" in text
assert call("GET", t + "/download-docx", raw=True) == original
assert call("POST", d + "/submit", {"applicantId": applicant})["status"] == "Submitted"
call("PUT", d + "/draft", {"applicantId": applicant, "formData": data}, 400)
call("POST", d + "/submit", {"applicantId": applicant}, 400)
call("GET", c, expected=400)
assert call("GET", c + "?applicantId=" + applicant)["totalCount"] == 1
# Uploading a new source invalidates current online readiness; previous drafts remain pinned.
second = call("POST", c + "/draft", draft_body, 201)["submissionId"]
upload(template, original)
assert not call("GET", t)["onlineReady"]
call("POST", c + "/draft", draft_body, 409)
call("PUT", c + "/" + second + "/draft", {"applicantId": applicant, "formData": data})
assert call("GET", c + "/" + second + "?applicantId=" + applicant)["templateVersionId"] == version
# Old Word-editing and full-application review APIs must no longer be advertised.
spec = call("GET", "/swagger/v1/swagger.json")
assert not any("officer/" in p or "docx-url" in p for p in spec["paths"])
assert "application/json" in spec["paths"][c + "/draft"]["post"]["requestBody"]["content"]
assert "multipart/form-data" not in spec["paths"][c + "/draft"]["post"]["requestBody"]["content"]
print(f"PASS: {checks} HTTP checks; original byte integrity, JSON drafts, pinned versions, submit validation, generated DOCX, removed APIs.")
