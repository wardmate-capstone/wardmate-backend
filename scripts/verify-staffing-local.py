"""Local HTTP checks: real Workflow + PostgreSQL, synthetic IAM/Catalog contracts.
Build Release first. Requires Docker and .NET 8. No existing DB, secrets or test project.
Creates/removes only its own disposable container. This is not Azure/IAM end-to-end coverage.
"""
import base64, concurrent.futures, datetime as dt, hashlib, hmac, http.server, json
import os, secrets, socket, subprocess, threading, time, urllib.error, urllib.request, uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LOG = ROOT / 'logs'
LOG.mkdir(exist_ok=True)
KEY, PASSWORD = secrets.token_urlsafe(48), secrets.token_urlsafe(32)
CONTAINER = 'wardmate-staffing-smoke-' + uuid.uuid4().hex[:10]
PROCESSES, HANDLES = [], []
COUNT = 0
IAM_OK = True
DIRECTORY_OK = True
def port():
    with socket.socket() as s:
        s.bind(('127.0.0.1', 0)); return s.getsockname()[1]
DBPORT, STUBPORT, WFPORT = [port() for _ in range(3)]
BASE = f'http://127.0.0.1:{WFPORT}'
IDS = {n: str(uuid.uuid4()) for n in ['manager','foreign','citizen','o1','o2','wrong','inactive','procedure']}
MANAGER = ['workflow.dispatch.read','workflow.dispatch.write','workflow.staffing.read','workflow.staffing.manage','workflow.consultation.read','workflow.read']
OFFICER = ['workflow.read','workflow.queue.read','workflow.assign','workflow.comments.write','workflow.staffing.read','workflow.leave.request','workflow.consultation.read','workflow.consultation.respond']
ACCESS = {
    'manager': dict(roles=['MANAGER'], wardCode='W1', permissions=MANAGER, assignedCategories=[]),
    'foreign': dict(roles=['MANAGER'], wardCode='W2', permissions=MANAGER, assignedCategories=[]),
    'citizen': dict(roles=['REGISTERED_CITIZEN'], wardCode=None, permissions=['workflow.create','workflow.read','workflow.submit','workflow.cancel'], assignedCategories=[]),
}
for n in ['o1','o2','wrong','inactive']:
    ACCESS[n] = dict(roles=['FRONT_DESK_OFFICER'], wardCode='W1', permissions=OFFICER.copy(), assignedCategories=[2 if n == 'wrong' else 1])
def b64(b): return base64.urlsafe_b64encode(b).rstrip(b'=').decode()
def token(who):
    raw = b64(b'{"alg":"HS256","typ":"JWT"}') + '.' + b64(json.dumps({'sub': IDS[who], 'iss':'wardmate', 'aud':'wardmate-client','exp':int(time.time())+3600,'role':['MANAGER'],'ward_code':'FORGED','assigned_category':'999'}).encode())
    return raw + '.' + b64(hmac.new(KEY.encode(), raw.encode(), hashlib.sha256).digest())
class Stub(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        if not IAM_OK: self.send_error(503); return
        if self.path.startswith('/api/v1/procedures/'):
            data = dict(id=IDS['procedure'], title='Synthetic staffing procedure', categoryId=1, isActive=True, checklistSchema=[], formDefinitions=[])
        elif self.path == '/api/v1/wards': data = [dict(code='W1'),dict(code='W2')]
        else:
            try:
                part = self.headers['Authorization'].split('.')[1]
                uid = json.loads(base64.urlsafe_b64decode(part+'='*(-len(part)%4)))['sub']
                name = next(n for n in ACCESS if IDS[n] == uid)
            except Exception: self.send_error(401); return
            if self.path.startswith('/api/v1/manager/officers'):
                if not DIRECTORY_OK: self.send_error(503); return
                data = dict(items=[dict(id=IDS[n], fullName=n, isActive=n!='inactive', **ACCESS[n]) for n in ['o1','o2','wrong','inactive']],total=4)
            else: data = dict(userId=uid, **ACCESS[name])
        body=json.dumps(data).encode(); self.send_response(200); self.send_header('Content-Type','application/json'); self.end_headers(); self.wfile.write(body)
    def log_message(self,*args): pass
def cmd(args, **kw): return subprocess.run(args,check=True,capture_output=True,text=True,encoding='utf-8',**kw).stdout
def sql(text): return cmd(['docker','exec','-i',CONTAINER,'psql','-U','postgres','-d','staffing','-v','ON_ERROR_STOP=1','-At'],input=text)
def req(path, who='manager', method='GET', data=None):
    headers={'Accept':'application/json'}
    if who: headers['Authorization']='Bearer '+token(who)
    if data is not None: headers['Content-Type']='application/json'
    r=urllib.request.Request(BASE+path,headers=headers,method=method,data=None if data is None else json.dumps(data).encode())
    try: response=urllib.request.urlopen(r,timeout=30)
    except urllib.error.HTTPError as e: response=e
    raw=response.read().decode()
    try: body=json.loads(raw)
    except ValueError: body=raw
    return response.status,body
def check(name,condition):
    global COUNT
    if not condition: raise AssertionError(name)
    COUNT+=1; print(f'PASS {COUNT}: {name}',flush=True)
def expect(name,path,status=200,who='manager',method='GET',data=None):
    s,b=req(path,who,method,data); check(f'{name} [{s}]',s==status); return b
def date(seconds): return (dt.datetime.now(dt.timezone.utc)+dt.timedelta(seconds=seconds)).isoformat()
def main():
    global IAM_OK,DIRECTORY_OK
    cmd(['docker','run','-d','--rm','--name',CONTAINER,'-e','POSTGRES_PASSWORD','-e','POSTGRES_DB=staffing','-p',f'127.0.0.1:{DBPORT}:5432','postgres:16-alpine'],env={**os.environ,'POSTGRES_PASSWORD':PASSWORD})
    for _ in range(80):
        if subprocess.run(['docker','exec',CONTAINER,'pg_isready','-U','postgres'],capture_output=True).returncode==0: break
        time.sleep(.5)
    server=http.server.ThreadingHTTPServer(('127.0.0.1',STUBPORT),Stub)
    threading.Thread(target=server.serve_forever,daemon=True).start()
    directory=ROOT/'src/Services/WardMate.Services.ApplicationWorkflow/WardMate.Services.ApplicationWorkflow.API'
    env={**os.environ,'ASPNETCORE_URLS':BASE,'ASPNETCORE_ENVIRONMENT':'Development','Jwt__Key':KEY,'Jwt__Issuer':'wardmate','Jwt__Audience':'wardmate-client',
         'Iam__BaseUrl':f'http://127.0.0.1:{STUBPORT}/','ProcedureCatalog__BaseUrl':f'http://127.0.0.1:{STUBPORT}/','Database__AutoMigrate':'true','RabbitMq__Enabled':'false',
         'ConnectionStrings__WorkflowDatabase':f'Host=127.0.0.1;Port={DBPORT};Username=postgres;Password={PASSWORD};Database=staffing'}
    log=open(LOG/'staffing-workflow.log','w',encoding='utf-8'); HANDLES.append(log)
    process=subprocess.Popen(['dotnet',str(directory/'bin/Release/net8.0/WardMate.Services.ApplicationWorkflow.API.dll')],cwd=directory,env=env,stdout=log,stderr=subprocess.STDOUT); PROCESSES.append(process)
    for _ in range(120):
        if process.poll() is not None: raise RuntimeError('Workflow exited: inspect logs/staffing-workflow.log')
        try:
            if req('/health',None)[0]==200: break
        except OSError: pass
        time.sleep(.5)
    check('five new tables migrated',sql("SELECT count(*) FROM pg_tables WHERE tablename IN ('officer_shifts','officer_leaves','officer_leave_decisions','application_assignment_history','application_consultations')").strip()=='5')
    root='/api/v1/manager/applications'
    expect('anonymous denied',root,401,None)
    expect('citizen cannot use manager dispatch',root,403,'citizen')
    expect('bad page rejected',root+'?pageSize=101',400)
    app=expect('citizen creates application','/api/v1/applications',201,'citizen','POST',dict(procedureId=IDS['procedure'],wardCode='W1',formData={}))['id']
    path=root+'/'+app
    assign=lambda who,version: dict(officerId=IDS[who],expectedAssignmentVersion=version,reasonCode='INITIAL_ASSIGNMENT' if version==0 else 'WORKLOAD_BALANCE',reason='Synthetic assignment')
    expect('draft cannot be assigned',path+'/assignments',409,method='POST',data=assign('o1',0))
    expect('submit empty synthetic checklist','/api/v1/applications/'+app+'/submit',who='citizen',method='POST')
    expect('foreign manager cannot see candidates',path+'/assignment-candidates',404,'foreign')
    candidates=expect('candidates before shift',path+'/assignment-candidates')
    check('no shift blocks candidate',all(not c['eligible'] for c in candidates))
    expect('no shift blocks assignment',path+'/assignments',422,method='POST',data=assign('o1',0))
    shift=dict(officerId=IDS['o1'],startsAt=date(3),endsAt=date(3600))
    expect('officer cannot create shift','/api/v1/staffing/shifts',403,'o1','POST',shift)
    expect('create officer one shift','/api/v1/staffing/shifts',method='POST',data=shift)
    expect('overlapping shift rejected','/api/v1/staffing/shifts',409,method='POST',data=shift)
    shift2=expect('create officer two shift','/api/v1/staffing/shifts',method='POST',data={**shift,'officerId':IDS['o2']})
    time.sleep(3.2)
    candidates=expect('candidates after shift',path+'/assignment-candidates')
    check('eligible candidates use live IAM categories',sum(c['eligible'] for c in candidates)==2)
    expect('wrong category rejected',path+'/assignments',422,method='POST',data=assign('wrong',0))
    expect('inactive officer rejected',path+'/assignments',422,method='POST',data=assign('inactive',0))
    DIRECTORY_OK=False
    expect('directory failure fails closed',path+'/assignments',503,method='POST',data=assign('o1',0)); DIRECTORY_OK=True
    expect('manager initial assignment',path+'/assignments',method='POST',data=assign('o1',0))
    expect('stale assignment revision rejected',path+'/assignments',409,method='POST',data=assign('o2',0))
    expect('assigned officer starts review','/api/v1/officer/applications/'+app+'/assign',who='o1',method='POST')
    check('claim does not duplicate same assignee history',len(expect('assignment history',path+'/assignments'))==1)
    before=req('/api/v1/applications/'+app,'citizen')[1]
    croot='/api/v1/applications/'+app+'/consultations'
    consultation=expect('manager opens consultation',croot,method='POST',data=dict(specialistId=IDS['o2'],expectedAssignmentVersion=1,question='Review synthetic issue'))
    expect('duplicate consultation rejected',croot,409,method='POST',data=dict(specialistId=IDS['o2'],expectedAssignmentVersion=1,question='Duplicate'))
    expect('specialist has document access',croot+'/document-access',who='o2')
    inbox=expect('specialist discovers consultation','/api/v1/staffing/consultations',who='o2')
    check('inbox contains own request',inbox['total']==1 and inbox['items'][0]['id']==consultation['id'])
    check('other officer inbox excludes request',expect('other officer inbox','/api/v1/staffing/consultations',who='o1')['total']==0)
    expect('citizen cannot read consultation inbox','/api/v1/staffing/consultations',403,'citizen')
    expect('unassigned specialist cannot mutate application','/api/v1/officer/applications/'+app+'/comments',403,'o2','POST',dict(versionNumber=1,targetType='FORM_FIELD',targetId='x',fieldLabel='x',commentText='no'))
    expect('wrong officer cannot answer',croot+'/'+consultation['id']+'/answer',403,'o1','POST',dict(expectedVersion=0,text='No'))
    expect('specialist answers',croot+'/'+consultation['id']+'/answer',who='o2',method='POST',data=dict(expectedVersion=0,text='Synthetic answer'))
    expect('duplicate answer rejected',croot+'/'+consultation['id']+'/answer',409,'o2','POST',dict(expectedVersion=0,text='Again'))
    expect('manager closes consultation',croot+'/'+consultation['id']+'/close',method='POST',data=dict(expectedVersion=1,text='Reviewed'))
    expect('closed consultation revokes document access',croot+'/document-access',403,'o2')
    consultation=expect('reopen as separate consultation',croot,method='POST',data=dict(specialistId=IDS['o2'],expectedAssignmentVersion=1,question='Second review'))
    expect('manager transfers to specialist',path+'/assignments',method='POST',data=assign('o2',1))
    after=req('/api/v1/applications/'+app,'citizen')[1]
    check('transfer preserves state and deadline',before['status']==after['status'] and before['dueAtUtc']==after['dueAtUtc'])
    expect('old consultation becomes stale',croot+'/document-access',403,'o2')
    expect('previous officer cannot comment','/api/v1/officer/applications/'+app+'/comments',403,'o1','POST',dict(versionNumber=1,targetType='FORM_FIELD',targetId='x',fieldLabel='x',commentText='no'))
    leave=expect('officer requests leave','/api/v1/staffing/leaves',who='o2',method='POST',data=dict(startsAt=date(-5),endsAt=date(1800),reason='Synthetic leave'))
    leavepath='/api/v1/staffing/leaves/'+leave['id']
    expect('officer cannot approve own leave',leavepath+'/decision',403,'o2','POST',dict(expectedVersion=0,decision='APPROVED',reason='No'))
    expect('foreign manager cannot decide leave',leavepath+'/decision',404,'foreign','POST',dict(expectedVersion=0,decision='APPROVED',reason='No'))
    impact=expect('manager sees affected applications',leavepath+'/impact')
    check('impact includes active assigned application',any(x['id']==app for x in impact))
    expect('manager approves leave',leavepath+'/decision',method='POST',data=dict(expectedVersion=0,decision='APPROVED',reason='Approved'))
    expect('stale leave revision rejected',leavepath+'/decision',409,method='POST',data=dict(expectedVersion=0,decision='CANCELLED',reason='Stale'))
    candidates=expect('leave affects eligibility',path+'/assignment-candidates')
    check('approved leave blocks officer',next(x for x in candidates if x['officerId']==IDS['o2'])['eligible'] is False)
    check('leave does not silently transfer application',req('/api/v1/applications/'+app,'citizen')[1]['assignedOfficerId']==IDS['o2'])
    expect('manager explicitly transfers workload',path+'/assignments',method='POST',data=assign('o1',2))
    expect('leave blocks reassignment',path+'/assignments',422,method='POST',data=assign('o2',3))
    expect('manager cancels approved leave',leavepath+'/decision',method='POST',data=dict(expectedVersion=1,decision='CANCELLED',reason='Returned'))
    decisions=expect('owner reads leave history',leavepath+'/history',who='o2')
    check('leave audit retains approval and cancellation',[x['toStatus'] for x in decisions]==['APPROVED','CANCELLED'])
    expect('colleague cannot read leave history',leavepath+'/history',404,'o1')
    expect('foreign manager cannot read leave history',leavepath+'/history',404,'foreign')
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        outcomes=list(pool.map(lambda _: req(path+'/assignments','manager','POST',assign('o2',3))[0],range(2)))
    check('concurrent reassignment commits only once',sorted(outcomes)==[200,409])
    history=expect('history retains each assignment',path+'/assignments')
    check('assignment versions contiguous', [x['version'] for x in history]==[1,2,3,4])
    expect('cancel shift','/api/v1/staffing/shifts/'+shift2['id']+'/cancel',method='POST',data=dict(reason='Synthetic cancellation'))
    expect('cancel shift twice rejected','/api/v1/staffing/shifts/'+shift2['id']+'/cancel',409,method='POST',data=dict(reason='Again'))
    query='?from='+urllib.parse.quote(date(-3600))+'&to='+urllib.parse.quote(date(7200))
    shifts=expect('officer reads own calendar','/api/v1/staffing/shifts'+query,who='o1')
    check('officer calendar cannot see colleagues',all(x['officerId']==IDS['o1'] for x in shifts))
    expect('manager reads leaves','/api/v1/staffing/leaves'+query)
    expect('invalid date range rejected','/api/v1/staffing/shifts?from=2026-01-01&to=2027-01-01',400)
    IAM_OK=False; expect('IAM unavailable fails closed',root,503); IAM_OK=True
    print(f'COMPLETE: {COUNT} checks passed; IAM/Catalog stubbed; no Azure writes.',flush=True)
if __name__=='__main__':
    try: main()
    finally:
        for p in PROCESSES:
            p.terminate()
            try: p.wait(timeout=15)
            except subprocess.TimeoutExpired: p.kill(); p.wait()
        for h in HANDLES: h.close()
        subprocess.run(['docker','rm','-f',CONTAINER],capture_output=True)
