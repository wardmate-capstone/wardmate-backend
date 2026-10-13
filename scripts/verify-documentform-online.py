"""Local-only HTTP smoke test; creates synthetic records in a dedicated test database/blob container.
Start the new API with local PostgreSQL + Azurite and run: python scripts/verify-documentform-online.py
No third-party Python packages required. Does not run against Azure.
"""
import io
import os
import base64
from pathlib import Path
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
manager_token = os.environ.get("DOCUMENTFORM_MANAGER_TOKEN")
citizen_token = os.environ.get("DOCUMENTFORM_CITIZEN_TOKEN")
assert manager_token and citizen_token, "Set DOCUMENTFORM_MANAGER_TOKEN and DOCUMENTFORM_CITIZEN_TOKEN to local IAM access tokens"
claims_part = citizen_token.split(".")[1]
claims = json.loads(base64.urlsafe_b64decode(claims_part + "=" * (-len(claims_part) % 4)))



def call(method, path, body=None, expected=200, content_type="application/json", raw=False):
    global checks
    data = body if isinstance(body, bytes) else json.dumps(body).encode() if body is not None else None
    request = urllib.request.Request(base + path, data=data, method=method, headers={"Content-Type": content_type, "Authorization": "Bearer " + (citizen_token if "/citizen/" in path else manager_token)})
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


def multipart(method, path, fields, data=None, expected=200, filename="edited.docx"):
    boundary = "wm-" + uuid.uuid4().hex
    parts = []
    for key, value in fields.items():
        parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{key}"\r\n\r\n{value}\r\n'.encode())
    if data is not None:
        parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="{filename}"\r\nContent-Type: application/vnd.openxmlformats-officedocument.wordprocessingml.document\r\n\r\n'.encode() + data + b"\r\n")
    parts.append(f"--{boundary}--\r\n".encode())
    return call(method, path, b"".join(parts), expected, "multipart/form-data; boundary=" + boundary)


def edit(data, text):
    output = io.BytesIO()
    with zipfile.ZipFile(io.BytesIO(data)) as source, zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as target:
        for item in source.infolist():
            content = source.read(item.filename)
            if item.filename == "word/document.xml":
                root = ET.fromstring(content)
                node = root.find(".//{http://schemas.openxmlformats.org/wordprocessingml/2006/main}t")
                assert node is not None
                node.text = text
                content = ET.tostring(root, encoding="utf-8", xml_declaration=True)
            target.writestr(item, content)
    return output.getvalue()


applicant = str(uuid.UUID(claims["sub"]))
c = "/api/v1/citizen/submissions"
template = call("POST", "/api/v1/form-templates", {"code": "DOCX-" + uuid.uuid4().hex[:12], "title": "Synthetic editor test"}, 201)["id"]
t = "/api/v1/form-templates/" + template
original = Path(sys.argv[2]).read_bytes() if len(sys.argv) > 2 else fixture()
fields = {"templateId": template, "applicantId": applicant}
assert not call("GET", t)["onlineReady"]
multipart("POST", c + "/draft", fields, original, 404)
multipart("POST", t + "/upload-docx", {}, b"broken", 400)
multipart("POST", t + "/upload-docx", {}, original, filename="mẫu gốc.docx")
assert call("GET", t + "/download-docx", raw=True) == original
meta = call("GET", t)
assert meta["onlineReady"] and "schemaDefinition" not in meta
edited = edit(original, "Synthetic citizen edit one")
updated = edit(original, "Synthetic citizen edit two")
call("POST", c + "/draft", fields, 415)
multipart("POST", c + "/draft", fields, expected=400)
multipart("POST", c + "/draft", fields, b"", 400)
multipart("POST", c + "/draft", fields, b"broken", 400)
multipart("POST", c + "/draft", fields, edited, 400, filename="wrong.txt")
draft = multipart("POST", c + "/draft", fields, edited, 201)["submissionId"]
d = c + "/" + draft
query = "?applicantId=" + applicant
assert call("GET", d + "/download-docx" + query, raw=True) == edited
assert call("GET", d + query)["status"] == "Draft"
call("GET", d + "?applicantId=" + str(uuid.uuid4()), expected=403)
call("GET", d + "/download-docx?applicantId=" + str(uuid.uuid4()), expected=403)
multipart("PUT", d + "/draft", {"applicantId": str(uuid.uuid4())}, updated, 403)
multipart("PUT", d + "/draft", {"applicantId": applicant}, updated)
assert call("GET", d + "/download-docx" + query, raw=True) == updated
assert call("GET", t + "/download-docx", raw=True) == original
# A later admin upload must not change the saved citizen document.
multipart("POST", t + "/upload-docx", {}, edited)
assert call("GET", d + "/download-docx" + query, raw=True) == updated
assert call("POST", d + "/submit", {"applicantId": applicant})["status"] == "Submitted"
multipart("PUT", d + "/draft", {"applicantId": applicant}, edited, 400)
call("POST", d + "/submit", {"applicantId": applicant}, 400)
assert call("GET", d + "/download-docx" + query, raw=True) == updated
call("GET", c, expected=403)
assert any(item["id"] == draft for item in call("GET", c + query)["items"])
spec = call("GET", "/swagger/v1/swagger.json")
assert not any(any(x in p for x in ("online-config", "docx-structure", "officer/", "docx-url")) for p in spec["paths"])
content = spec["paths"][c + "/draft"]["post"]["requestBody"]["content"]
assert "multipart/form-data" in content and "application/json" not in content
print(f"PASS: {checks} HTTP checks; unchanged originals, exact edited bytes, draft reopen/update, ownership, submit locking, multipart contract and removed APIs.")
