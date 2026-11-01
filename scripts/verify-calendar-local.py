"""Docker Workflow/PostgreSQL HTTP checks; synthetic IAM/Catalog, no production data.
Requires image wardmate-workflow:calendar-local. Removes only containers it creates.
"""
import importlib.util, datetime as dt, json, os, subprocess, threading, time, uuid, http.server
from pathlib import Path
spec=importlib.util.spec_from_file_location('staffing_checks',Path(__file__).with_name('verify-staffing-local.py'))
t=importlib.util.module_from_spec(spec);spec.loader.exec_module(t)
appcontainer=t.CONTAINER+'-api'
t.IDS['admin']=str(uuid.uuid4());t.ACCESS['admin']=dict(roles=['IT_ADMIN'],wardCode=None,permissions=['workflow.calendar.manage'],assignedCategories=[])
t.IDS['no_permission']=str(uuid.uuid4());t.ACCESS['no_permission']=dict(roles=['MANAGER'],wardCode='W1',permissions=[],assignedCategories=[])
def run():
    t.cmd(['docker','run','-d','--rm','--name',t.CONTAINER,'-e','POSTGRES_PASSWORD','-e','POSTGRES_DB=staffing','-p',f'127.0.0.1:{t.DBPORT}:5432','postgres:16-alpine'],env={**os.environ,'POSTGRES_PASSWORD':t.PASSWORD})
    for _ in range(100):
        if subprocess.run(['docker','exec',t.CONTAINER,'pg_isready','-U','postgres'],capture_output=True).returncode==0: break
        time.sleep(.5)
    server=http.server.ThreadingHTTPServer(('0.0.0.0',t.STUBPORT),t.Stub)
    threading.Thread(target=server.serve_forever,daemon=True).start()
    env={**os.environ,'Jwt__Key':t.KEY,'Jwt__Issuer':'wardmate','Jwt__Audience':'wardmate-client','Iam__BaseUrl':f'http://host.docker.internal:{t.STUBPORT}/','ProcedureCatalog__BaseUrl':f'http://host.docker.internal:{t.STUBPORT}/','Database__AutoMigrate':'true','RabbitMq__Enabled':'false','ASPNETCORE_ENVIRONMENT':'Development','ConnectionStrings__WorkflowDatabase':f'Host=host.docker.internal;Port={t.DBPORT};Database=staffing;Username=postgres;Password={t.PASSWORD}'}
    args=['docker','run','-d','--rm','--name',appcontainer,'-p',f'127.0.0.1:{t.WFPORT}:8080']
    for key in env:
        if key not in os.environ or key in ['ASPNETCORE_ENVIRONMENT']: args+=['-e',key]
    t.cmd(args+['wardmate-workflow:calendar-local'],env=env)
    for _ in range(180):
        try:
            if t.req('/health',None)[0]==200: break
        except OSError: pass
        time.sleep(.5)
    root='/api/v1/staffing/closures'
    today=dt.datetime.now(dt.timezone(dt.timedelta(hours=7))).date(); future=today+dt.timedelta(days=2)
    while future.weekday()>4: future+=dt.timedelta(days=1)
    dates=f'?from={today}&to={today+dt.timedelta(days=60)}'
    t.sql((t.ROOT/'docs/sql/workflow-calendar-closures.sql').read_text(encoding='utf-8'))
    t.sql((t.ROOT/'docs/sql/workflow-calendar-closures.sql').read_text(encoding='utf-8'))
    t.check('idempotent migration script',t.sql("SELECT count(*) FROM \"__EFMigrationsHistory\" WHERE migration_id='20261101055515_CalendarClosures'").strip()=='1')
    t.check('migration applied',t.sql("SELECT count(*) FROM calendar_closures").strip()=='0')
    t.expect('anonymous denied',root+dates,401,None)
    t.expect('citizen denied',root+dates,403,'citizen')
    t.expect('manager without permission denied',root+dates,403,'no_permission')
    t.expect('officer can list',root+dates,who='o1')
    t.expect('missing dates rejected',root,400)
    t.expect('malformed dates rejected',root+'?from=bad&to=bad',400)
    t.expect('range over 400 rejected',root+f'?from={today}&to={today+dt.timedelta(days=400)}',400)
    body=dict(date=str(future),reason=' Synthetic closure ')
    t.expect('officer cannot create',root,403,'o1','POST',body)
    t.expect('manager cannot create national',root+'/national',403,'manager','POST',body)
    t.expect('admin cannot create ward',root,403,'admin','POST',body)
    for label,data in [('past',dict(date=str(today-dt.timedelta(days=1)),reason='x')),('far',dict(date=str(today+dt.timedelta(days=367)),reason='x')),('empty reason',dict(date=str(future),reason='  ')),('long reason',dict(date=str(future),reason='x'*501))]:
        t.expect(label,root,400,method='POST',data=data)
    # Real application creation and database-backed read checks exercise existing DI registrations.
    app=t.expect('create W1 application','/api/v1/applications',201,'citizen','POST',dict(procedureId=t.IDS['procedure'],wardCode='W1',formData={}))['id']
    app2=t.expect('create W2 application','/api/v1/applications',201,'citizen','POST',dict(procedureId=t.IDS['procedure'],wardCode='W2',formData={}))['id']
    policy=json.dumps(dict(Enabled=True,Applicable=True,WorkingMinutes=2700))
    due=dt.datetime.combine(future+dt.timedelta(days=7),dt.time(8),tzinfo=dt.timezone.utc).isoformat()
    t.sql(f"UPDATE applications SET status='SUBMITTED',first_submitted_at_utc=now(),deadline_policy_code='TEST',deadline_policy_version='1',deadline_calendar_code='VN',deadline_calendar_version='1',deadline_status='KNOWN',due_at_utc='{due}',deadline_policy_snapshot='{policy}'::jsonb WHERE id IN ('{app}','{app2}')")
    old=t.sql(f"SELECT due_at_utc FROM applications WHERE id='{app}'").strip()
    row=t.expect('create ward closure',root,method='POST',data=body)
    cid=row['closure']['id'];t.check('ward and reason from trusted context',row['closure']['wardCode']=='W1' and row['closure']['reason']=='Synthetic closure')
    t.check('only ward application rescheduled',row['rescheduledApplications']==1)
    t.expect('duplicate conflict',root,409,method='POST',data=body)
    t.expect('foreign ward cannot cancel',root+'/'+cid+'/cancel',403,'foreign','POST',dict(reason='x'))
    t.expect('admin cannot cancel ward',root+'/'+cid+'/cancel',403,'admin','POST',dict(reason='x'))
    national=t.expect('create national',root+'/national',who='admin',method='POST',data=body)
    t.check('national union does not double shift W1',national['rescheduledApplications']==1)
    t.check('IT admin lists only national',len(t.expect('admin list',root+dates,who='admin'))==1)
    t.check('W2 cannot list W1 closure',len(t.expect('foreign list',root+dates,who='foreign'))==1)
    t.expect('manager cannot cancel national',root+'/'+national['closure']['id']+'/cancel',403,method='POST',data=dict(reason='x'))
    cancel=t.expect('cancel ward overlapping national',root+'/'+cid+'/cancel',method='POST',data=dict(reason=' Reopen '))
    t.check('overlap cancellation keeps due',cancel['rescheduledApplications']==0)
    cancel=t.expect('cancel national',root+'/'+national['closure']['id']+'/cancel',who='admin',method='POST',data=dict(reason='x'))
    t.check('national cancel affects both wards',cancel['rescheduledApplications']==2)
    t.check('deadline restored',t.sql(f"SELECT due_at_utc FROM applications WHERE id='{app}'").strip()==old)
    t.expect('cancel twice conflict',root+'/'+cid+'/cancel',409,method='POST',data=dict(reason='x'))
    t.expect('unknown closure',root+'/'+str(uuid.uuid4())+'/cancel',404,method='POST',data=dict(reason='x'))
    same=t.expect('today accepted',root,method='POST',data=dict(date=str(today),reason='today'))
    t.expect('today cannot cancel',root+'/'+same['closure']['id']+'/cancel',409,method='POST',data=dict(reason='x'))
    # Concurrent duplicate creation must produce one success and one conflict, never 500.
    import concurrent.futures
    with concurrent.futures.ThreadPoolExecutor() as pool:
        codes=list(pool.map(lambda _:t.req(root,'manager','POST',body)[0],range(2)))
    t.check('concurrent duplicate serialized',sorted(codes)==[200,409])
    t.expect('manager applications resolves DI','/api/v1/manager/applications?page=1&pageSize=20')
    t.expect('staffing leaves resolves DI','/api/v1/staffing/leaves'+f'?from={today}T00:00:00Z&to={today+dt.timedelta(days=7)}T00:00:00Z')
    t.expect('consultation inbox resolves DI','/api/v1/staffing/consultations',who='o1')
    t.IAM_OK=False
    t.expect('IAM outage fails closed',root+dates,503)
    t.IAM_OK=True
    t.check('four closure operations in Swagger',sum(len([m for m in v if m in ['get','post']]) for k,v in t.req('/swagger/v1/swagger.json',None)[1]['paths'].items() if '/staffing/closures' in k)==4)
    print(f'TOTAL {t.COUNT} passed; Docker Workflow + PostgreSQL, synthetic IAM/Catalog.',flush=True)
try: run()
except subprocess.CalledProcessError as e:
    print(e.stderr, flush=True)
    raise
finally:
    p=subprocess.run(['docker','logs',appcontainer],capture_output=True,text=True,encoding='utf-8',errors='replace')
    (t.LOG/'calendar-docker-api.log').write_text(p.stdout+p.stderr,encoding='utf-8')
    for name in [appcontainer,t.CONTAINER]: subprocess.run(['docker','rm','-f',name],capture_output=True)
