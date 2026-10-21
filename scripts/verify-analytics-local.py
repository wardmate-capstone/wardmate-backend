"""TASK-31..33 integration smoke: disposable PostgreSQL, real Workflow/Analytics, stub IAM.
Build Release first. Requires Docker, Python 3, .NET 8 runtime. No existing DB/env is used.
Creates no test project. Tokens/keys stay in memory; logs go to ignored logs/.
"""
import base64
import concurrent.futures
import datetime as dt
import hashlib
import hmac
import http.server
import json
import os
from pathlib import Path
import secrets
import socket
import subprocess
import threading
import time
import urllib.error
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[1]
LOG = ROOT / "logs"
LOG.mkdir(exist_ok=True)
PROCESSES, HANDLES = [], []
CONTAINER = "wardmate-analytics-smoke-" + uuid.uuid4().hex[:10]
KEY = secrets.token_urlsafe(48)
PASSWORD = secrets.token_urlsafe(32)
COUNT = 0

def port():
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0))
        return s.getsockname()[1]

DBPORT, IAMPORT, WFPORT, APIPORT, GWPORT = [port() for _ in range(5)]
BASE = f"http://127.0.0.1:{APIPORT}"
WORKFLOW = f"http://127.0.0.1:{WFPORT}"
IDS = {name: str(uuid.uuid4()) for name in ("citizen", "other", "manager", "manager2", "admin", "officer", "procedure", "app", "draft", "foreign", "app2")}
ACCESS = {
    IDS["citizen"]: (["REGISTERED_CITIZEN"], None), IDS["other"]: (["REGISTERED_CITIZEN"], None),
    IDS["manager"]: (["MANAGER"], "W1"), IDS["manager2"]: (["MANAGER"], "W2"),
    IDS["admin"]: (["IT_ADMIN"], None), IDS["officer"]: (["FRONT_DESK_OFFICER"], "W1")}
IAM_AVAILABLE = True

def b64(data):
    return base64.urlsafe_b64encode(data).rstrip(b"=").decode()

def token(name):
    header = b64(json.dumps({"alg": "HS256", "typ": "JWT"}).encode())
    body = b64(json.dumps({"sub": IDS[name], "iss": "wardmate", "aud": "wardmate-client", "exp": int(time.time()) + 1800,
                           "role": ACCESS[IDS[name]][0], "ward_code": "FORGED"}).encode())
    data = f"{header}.{body}"
    return data + "." + b64(hmac.new(KEY.encode(), data.encode(), hashlib.sha256).digest())

class Iam(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        if not IAM_AVAILABLE:
            self.send_error(503)
            return
        try:
            encoded = self.headers["Authorization"].split(".")[1]
            user = json.loads(base64.urlsafe_b64decode(encoded + "=" * (-len(encoded) % 4)))["sub"]
            roles, ward = ACCESS[user]
            payload = json.dumps({"userId": user, "roles": roles, "wardCode": ward, "permissions": ["workflow.read"]}).encode()
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.end_headers()
            self.wfile.write(payload)
        except Exception:
            self.send_error(401)
    def log_message(self, *_):
        pass

def request(path, who=None, method="GET", body=None, base=BASE, headers=None):
    values = {"Accept": "application/json", **(headers or {})}
    if who:
        values["Authorization"] = "Bearer " + token(who)
    if body is not None:
        values["Content-Type"] = "application/json"
    r = urllib.request.Request(base + path, data=None if body is None else json.dumps(body).encode(), headers=values, method=method)
    try:
        response = urllib.request.urlopen(r, timeout=75)
    except urllib.error.HTTPError as e:
        response = e
    raw = response.read().decode()
    try:
        data = json.loads(raw)
    except ValueError:
        data = raw
    return response.status, data

def check(name, condition):
    global COUNT
    if not condition:
        raise AssertionError(name)
    COUNT += 1
    print(f"PASS {COUNT}: {name}", flush=True)

def cmd(args, **kwargs):
    return subprocess.run(args, check=True, capture_output=True, text=True, encoding="utf-8", **kwargs).stdout

def sql(database, text):
    return cmd(["docker", "exec", "-i", CONTAINER, "psql", "-U", "postgres", "-d", database, "-v", "ON_ERROR_STOP=1", "-At"], input=text)

def launch(service, urlport, extra):
    directory = ROOT / "src" / "Services" / f"WardMate.Services.{service}" / f"WardMate.Services.{service}.API"
    assembly = directory / "bin" / "Release" / "net8.0" / f"WardMate.Services.{service}.API.dll"
    env = {**os.environ, "ASPNETCORE_URLS": f"http://127.0.0.1:{urlport}", "ASPNETCORE_ENVIRONMENT": "Development",
           "Jwt__Key": KEY, "Jwt__Issuer": "wardmate", "Jwt__Audience": "wardmate-client",
           "Iam__BaseUrl": f"http://127.0.0.1:{IAMPORT}/", "Database__AutoMigrate": "true",
           "RabbitMq__Enabled": "false", "Notifications__WorkersEnabled": "false", "Analytics__ServiceKey": KEY, **extra}
    handle = open(LOG / f"task31-33-{service}.log", "w", encoding="utf-8")
    HANDLES.append(handle)
    process = subprocess.Popen(["dotnet", str(assembly)], cwd=directory, env=env, stdout=handle, stderr=subprocess.STDOUT)
    PROCESSES.append(process)
    wait_http(f"http://127.0.0.1:{urlport}", process)

def wait_http(base, process):
    for _ in range(120):
        if process.poll() is not None:
            raise RuntimeError("Service exited; inspect ignored logs/task31-33-*.log")
        try:
            if request("/health", base=base)[0] == 200:
                return
        except (OSError, urllib.error.URLError):
            pass
        time.sleep(0.5)
    raise TimeoutError("Service startup")

def main():
    global IAM_AVAILABLE
    env = {**os.environ, "POSTGRES_PASSWORD": PASSWORD}
    cmd(["docker", "run", "-d", "--rm", "--name", CONTAINER, "-e", "POSTGRES_PASSWORD", "-p", f"127.0.0.1:{DBPORT}:5432", "postgres:16-alpine"], env=env)
    for _ in range(90):
        if subprocess.run(["docker", "exec", CONTAINER, "pg_isready", "-U", "postgres"], capture_output=True).returncode == 0:
            break
        time.sleep(0.5)
    sql("postgres", "CREATE DATABASE workflow_smoke; CREATE DATABASE analytics_smoke;")
    connection = f"Host=127.0.0.1;Port={DBPORT};Username=postgres;Password={PASSWORD};Database="
    launch("ApplicationWorkflow", WFPORT, {"ConnectionStrings__WorkflowDatabase": connection + "workflow_smoke"})
    launch("AnalyticsSystem", APIPORT, {"ConnectionStrings__NotificationsDatabase": connection + "analytics_smoke",
           "Workflow__BaseUrl": WORKFLOW + "/", "Analytics__JobsEnabled": "true", "Analytics__Cron": "0 0 1 1 *", "Analytics__LookbackDays": "1"})
    today = dt.datetime.now(dt.timezone(dt.timedelta(hours=7))).date()
    date = today.isoformat()
    early = (dt.datetime.combine(today, dt.time(1), dt.timezone(dt.timedelta(hours=7)))).isoformat()
    late = (dt.datetime.combine(today, dt.time(3), dt.timezone(dt.timedelta(hours=7)))).isoformat()
    before = (dt.datetime.combine(today, dt.time.min, dt.timezone(dt.timedelta(hours=7))) - dt.timedelta(seconds=1)).isoformat()
    seed = []
    for key, owner, ward, state in [("app", "citizen", "W1", "DOCUMENTS_READY"), ("app2", "citizen", "W1", "DOCUMENTS_READY"),
                                    ("draft", "citizen", "W1", "DRAFT"), ("foreign", "other", "W2", "DOCUMENTS_READY")]:
        seed.append(f"""INSERT INTO applications(id,application_code,user_id,procedure_id,procedure_title,status,form_data,created_at,updated_at,ward_code,assigned_officer_id)
        VALUES('{IDS[key]}','{key}','{IDS[owner]}','{IDS['procedure']}','Synthetic smoke','{state}','{{}}','{early}','{late}','{ward}','{IDS['officer']}');""")
        if state != "DRAFT":
            for old, new, when, actor in [("DRAFT", "SUBMITTED", before if key == "app2" else early, owner),
                                         ("UNDER_REVIEW", "DOCUMENTS_READY", late, "officer")]:
                seed.append(f"INSERT INTO application_status_history(id,application_id,from_status,to_status,changed_by,created_at) VALUES('{uuid.uuid4()}','{IDS[key]}','{old}','{new}','{IDS[actor]}','{when}');")
    sql("workflow_smoke", "\n".join(seed))
    check("both actual EF databases migrated", "citizen_feedback" in sql("analytics_smoke", "SELECT tablename FROM pg_tables WHERE schemaname='analytics'"))
    check("internal source requires key", request(f"/internal/analytics/v1/daily?date={date}", base=WORKFLOW)[0] == 401)
    status, snapshot = request(f"/internal/analytics/v1/daily?date={date}", base=WORKFLOW, headers={"X-Analytics-Key": KEY})
    check("Workflow aggregation returns versioned snapshot", status == 200 and snapshot["contractVersion"] == 1)
    w1 = next(x for x in snapshot["procedures"] if x["wardCode"] == "W1")
    check("UTC+7 boundary excludes previous-day submission; counts readiness today", w1["receivedCount"] == 1 and w1["readyCount"] == 2)
    query = f"?from={date}&to={date}"
    check("anonymous dashboard denied", request("/api/v1/analytics/overview" + query)[0] == 401)
    check("citizen dashboard denied", request("/api/v1/analytics/overview" + query, "citizen")[0] == 403)
    check("manager cannot trigger global rebuild", request("/api/v1/analytics/jobs/rebuild", "manager", "POST", {"from": date, "to": date})[0] == 403)
    check("missing dates are explicit before first run", len(request("/api/v1/analytics/overview" + query, "manager")[1]["coverage"]["missingDates"]) == 1)
    for iteration in range(2):
        previous = request("/api/v1/analytics/overview" + query, "manager")[1]["coverage"]["latestRefreshAtUtc"]
        status, job = request("/api/v1/analytics/jobs/rebuild", "admin", "POST", {"from": date, "to": date})
        check(f"rebuild enqueue {iteration}", status == 202 and bool(job.get("jobId")))
        for _ in range(100):
            status, result = request("/api/v1/analytics/overview" + query, "manager")
            if status == 200 and result["coverage"]["latestRefreshAtUtc"] != previous:
                break
            time.sleep(0.4)
        check(f"Hangfire execution/replay {iteration} preserves counts", result["coverage"]["latestRefreshAtUtc"] != previous and result["totals"]["receivedCount"] == 1 and result["totals"]["readyCount"] == 2 and not result["coverage"]["missingDates"])
        check(f"job status visible to IT_ADMIN {iteration}", request("/api/v1/analytics/jobs/" + job["jobId"], "admin")[0] == 200)
    check("missing SLA stays null", result["onTimeRate"] is None and result["lateRate"] is None)
    check("manager is scoped by live IAM ward not forged JWT", request("/api/v1/analytics/overview" + query, "manager2")[1]["totals"]["readyCount"] == 1)
    check("trends populated", request("/api/v1/analytics/trends" + query, "manager")[1]["items"][0]["totals"]["readyCount"] == 2)
    check("procedure pagination populated", request("/api/v1/analytics/procedures" + query, "manager")[1]["totalCount"] == 1)
    officers = request("/api/v1/analytics/officers" + query, "manager")[1]["items"]
    check("officer uses actual readiness actor and weighted duration", len(officers) == 1 and officers[0]["readyCount"] == 2 and officers[0]["durationSampleCount"] == 2 and officers[0]["averageElapsedMinutes"] > 120)
    check("invalid range rejected", request("/api/v1/analytics/overview?from=1999-01-01&to=" + date, "manager")[0] == 400)
    check("oversized page rejected", request("/api/v1/analytics/procedures" + query + "&pageSize=101", "manager")[0] == 400)
    path = "/api/v1/citizen/feedback/" + IDS["app"]
    check("rating outside 1..5 rejected", request(path, "citizen", "POST", {"rating": 6})[0] == 400)
    check("comment too long rejected", request(path, "citizen", "POST", {"rating": 5, "comment": "a" * 2001})[0] == 400)
    check("other applicant cannot submit feedback", request(path, "other", "POST", {"rating": 5})[0] == 404)
    check("draft not eligible", request("/api/v1/citizen/feedback/" + IDS["draft"], "citizen", "POST", {"rating": 5})[0] == 409)
    check("manager cannot post citizen feedback", request(path, "manager", "POST", {"rating": 5})[0] == 403)
    check("eligible feedback created", request(path, "citizen", "POST", {"rating": 4, "comment": " Synthetic "})[0] == 201)
    check("duplicate feedback conflict", request(path, "citizen", "POST", {"rating": 5})[0] == 409)
    check("owner reads feedback", request(path, "citizen")[1]["comment"] == "Synthetic")
    check("other citizen cannot read feedback", request(path, "other")[0] == 404)
    concurrent_path = "/api/v1/citizen/feedback/" + IDS["app2"]
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        statuses = list(pool.map(lambda _: request(concurrent_path, "citizen", "POST", {"rating": 2})[0], range(2)))
    check("concurrent creates yield one 201 and one 409", sorted(statuses) == [201, 409])
    summary = request("/api/v1/analytics/feedback/summary" + query, "manager")[1]
    check("feedback weighted score and satisfaction", summary["totalCount"] == 2 and summary["averageRating"] == 3 and summary["satisfactionRate"] == 50)
    check("no cross-ward feedback disclosure", request("/api/v1/analytics/feedback" + query, "manager2")[1]["totalCount"] == 0)
    check("empty feedback average null", request("/api/v1/analytics/feedback/summary" + query, "manager2")[1]["averageRating"] is None)
    ACCESS[IDS["manager"]] = (["REGISTERED_CITIZEN"], "W1")
    check("live role revocation enforced", request("/api/v1/analytics/overview" + query, "manager")[0] == 403)
    ACCESS[IDS["manager"]] = (["MANAGER"], None)
    check("manager without ward denied", request("/api/v1/analytics/overview" + query, "manager")[0] == 403)
    ACCESS[IDS["manager"]] = (["MANAGER"], "W1")
    IAM_AVAILABLE = False
    check("IAM outage fails closed", request("/api/v1/analytics/overview" + query, "manager")[0] == 503)
    IAM_AVAILABLE = True
    check("Swagger exposes new APIs", "/api/v1/citizen/feedback/{applicationId}" in request("/swagger/v1/swagger.json")[1]["paths"])
    # Real gateway, not a duplicated route parser.
    gwdir = ROOT / "src/Gateways/WardMate.YarpGateway"
    handle = open(LOG / "task31-33-gateway.log", "w", encoding="utf-8")
    HANDLES.append(handle)
    gw = subprocess.Popen(["dotnet", str(gwdir / "bin/Release/net8.0/WardMate.YarpGateway.dll")], cwd=gwdir,
        env={**os.environ, "ASPNETCORE_URLS": f"http://127.0.0.1:{GWPORT}",
             "ReverseProxy__Clusters__analytics-system__Destinations__primary__Address": BASE + "/"}, stdout=handle, stderr=subprocess.STDOUT)
    PROCESSES.append(gw)
    gateway = f"http://127.0.0.1:{GWPORT}"
    wait_http(gateway, gw)
    check("gateway citizen route overrides DocumentForm catch-all", request(path, "citizen", base=gateway)[0] == 200)
    check("gateway analytics route", request("/api/v1/analytics/overview" + query, "manager", base=gateway)[0] == 200)
    check("gateway Swagger prefix", request("/api/analytics-system/swagger/v1/swagger.json", base=gateway)[0] == 200)
    previous = request("/api/v1/analytics/overview" + query, "manager")[1]
    PROCESSES[0].terminate()
    PROCESSES[0].wait(timeout=10)
    check("Workflow outage returns 503 on feedback, no fabricated eligibility", request(path, "citizen", "POST", {"rating": 5})[0] == 503)
    _, failed_job = request("/api/v1/analytics/jobs/rebuild", "admin", "POST", {"from": date, "to": date})
    state = ""
    for _ in range(100):
        state = request("/api/v1/analytics/jobs/" + failed_job["jobId"], "admin")[1].get("state")
        if state in ("Scheduled", "Failed"):
            break
        time.sleep(0.2)
    check("source outage enters Hangfire retry/failure state", state in ("Scheduled", "Failed"))
    check("failed source preserves previous complete metrics", request("/api/v1/analytics/overview" + query, "manager")[1] == previous)
    print(f"RESULT: {COUNT} checks passed. Real Workflow + Analytics + PostgreSQL + Hangfire + Gateway; IAM simulated.", flush=True)

if __name__ == "__main__":
    server = http.server.ThreadingHTTPServer(("127.0.0.1", IAMPORT), Iam)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    try:
        main()
    finally:
        for process in reversed(PROCESSES):
            process.terminate()
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
        for handle in HANDLES:
            handle.close()
        server.shutdown()
        subprocess.run(["docker", "rm", "-f", CONTAINER], capture_output=True)
