"""Local JWT role checks. Builds no tokens for real accounts; isolated key/server, no DB writes.
Run after Release build: python scripts/verify-documentform-authorization.py
"""
import base64, hashlib, hmac, json, os, secrets, subprocess, time, urllib.request, urllib.error
from pathlib import Path
root = Path(__file__).resolve().parents[1]
api = root / "src/Services/WardMate.Services.DocumentForm/WardMate.Services.DocumentForm.API"
key = secrets.token_bytes(32).hex()
env = os.environ.copy()
env.update(Jwt__Key=key, Jwt__Issuer="wardmate", Jwt__Audience="wardmate-client",
           Database__AutoMigrate="false", ASPNETCORE_URLS="http://127.0.0.1:15024", Swagger__Enabled="true")
subject = "11111111-1111-4111-8111-111111111111"
base = "http://127.0.0.1:15024"
checks = 0
def token(role, **overrides):
    payload = dict(sub=subject, role=role, iss="wardmate", aud="wardmate-client", exp=int(time.time())+120)
    payload.update(overrides)
    enc = lambda x: base64.urlsafe_b64encode(x).rstrip(b"=")
    raw = enc(json.dumps(dict(alg="HS256",typ="JWT")).encode())+b"."+enc(json.dumps(payload).encode())
    return (raw+b"."+enc(hmac.new(key.encode(),raw,hashlib.sha256).digest())).decode()
def call(method, path, expected, jwt=None, body=None):
    global checks
    headers = {"Content-Type":"multipart/form-data; boundary=AuthTest" if method in ("POST", "PUT") and (path.endswith("/draft") or path.endswith("/upload-docx")) else "application/json"}
    if jwt: headers["Authorization"]="Bearer "+jwt
    request=urllib.request.Request(base+path, data=json.dumps(body).encode() if body is not None else None, headers=headers, method=method)
    try: response=urllib.request.urlopen(request,timeout=5)
    except urllib.error.HTTPError as error: response=error
    data=response.read()
    assert response.code==expected,(method,path,response.code,data[:300])
    if expected in (401,403):
        problem=json.loads(data); assert problem["status"]==expected and "traceId" in problem
    checks+=1
    return data
process=subprocess.Popen(["dotnet",str(api/"bin/Release/net8.0/WardMate.Services.DocumentForm.API.dll")],cwd=api,env=env,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
try:
    for attempt in range(60):
        try:
            urllib.request.urlopen(base+"/health",timeout=1).close();break
        except Exception:
            if process.poll() is not None: raise RuntimeError("API startup failed")
            time.sleep(0.5)
    citizen=token("REGISTERED_CITIZEN");manager=token("PROCEDURE_MANAGER")
    t="/api/v1/form-templates"; c="/api/v1/citizen/submissions"; ident="22222222-2222-4222-8222-222222222222"
    routes=[("GET",t), ("POST",t), ("GET",t+"/"+ident), ("POST",t+"/"+ident+"/upload-docx"), ("GET",t+"/"+ident+"/download-docx"),
            ("GET",c),("GET",c+"/"+ident),("POST",c+"/draft"),("PUT",c+"/"+ident+"/draft"),("POST",c+"/"+ident+"/submit"),("GET",c+"/"+ident+"/download-docx")]
    for method,path in routes:
        call(method,path,401)
        call(method,path,403,citizen if path.startswith(t) else manager)
    call("GET",t,401,token("PROCEDURE_MANAGER",exp=int(time.time())-10))
    call("GET",t,401,token("PROCEDURE_MANAGER",aud="wrong"))
    call("GET",t,401,token("PROCEDURE_MANAGER",iss="wrong"))
    call("GET",t,403,token("IT_ADMIN"))
    call("GET",c+"?applicantId="+subject,403,token("REGISTERED_CITIZEN",sub="bad"))
    call("GET",c+"?applicantId="+ident,403,citizen)
    call("GET",c+"/"+ident+"?applicantId="+ident,403,citizen)
    call("GET",c+"/"+ident+"/download-docx?applicantId="+ident,403,citizen)
    call("POST",c+"/"+ident+"/submit",403,citizen,{"applicantId":ident})
    # Valid roles reach business validation without database access.
    call("GET",c+"?applicantId="+subject+"&page=0",400,citizen)
    call("POST",t,400,manager,{"code":"","title":""})
    spec=json.loads(call("GET","/swagger/v1/swagger.json",200))
    assert "Bearer" in spec["components"]["securitySchemes"]
    print(f"PASS: {checks} JWT/role HTTP checks; no database writes")
finally:
    process.terminate()
    try: process.wait(timeout=10)
    except subprocess.TimeoutExpired: process.kill();process.wait()
