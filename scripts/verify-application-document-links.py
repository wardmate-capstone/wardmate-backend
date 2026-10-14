"""Isolated local HTTP integration: real Workflow/DocumentForm + disposable PostgreSQL.
Uses disposable Azurite (10000) and synthetic IAM/ProcedureCatalog stubs; never calls Azure.
Run after solution Release build. No credentials or JWTs are printed.
"""
import ast, base64, hashlib, hmac, io, json, os, secrets, subprocess, tempfile, threading, time, uuid, zipfile
from pathlib import Path
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import urllib.request, urllib.error, http.client
ROOT=Path(__file__).resolve().parents[1]
DOC='http://127.0.0.1:15034'; WF='http://127.0.0.1:15033'
key=secrets.token_urlsafe(48); user=str(uuid.uuid4()); other=str(uuid.uuid4()); procedure=str(uuid.uuid4()); checks=0

def token(who=user,role='REGISTERED_CITIZEN'):
 def b(x): return base64.urlsafe_b64encode(json.dumps(x).encode()).rstrip(b'=')
 msg=b({'alg':'HS256','typ':'JWT'})+b'.'+b({'sub':who,'role':role,'iss':'wardmate','aud':'wardmate-client','exp':int(time.time())+1800})
 return (msg+b'.'+base64.urlsafe_b64encode(hmac.new(key.encode(),msg,hashlib.sha256).digest()).rstrip(b'=')).decode()

def call(base,method,path,body=None,expected=200,who=user,role='REGISTERED_CITIZEN',ctype='application/json',raw=False):
 global checks
 payload=body if isinstance(body,bytes) else json.dumps(body).encode() if body is not None else None
 req=urllib.request.Request(base+path,data=payload,method=method,headers={'Authorization':'Bearer '+token(who,role),'Content-Type':ctype})
 try: res=urllib.request.urlopen(req,timeout=30)
 except urllib.error.HTTPError as ex: res=ex
 data=res.read()
 assert res.code==expected,(method,path,res.code,data[:900])
 checks+=1
 return data if raw else json.loads(data) if data else None

def multi(path,fields,file,expected=200,method='POST',who=user,role='REGISTERED_CITIZEN',filename='test.docx'):
 boundary=uuid.uuid4().hex
 parts=[f'--{boundary}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n'.encode() for k,v in fields.items()]
 if file is not None: parts += [f'--{boundary}\r\nContent-Disposition: form-data; name="File"; filename="{filename}"\r\nContent-Type: application/octet-stream\r\n\r\n'.encode()+file+b'\r\n']
 parts += [f'--{boundary}--\r\n'.encode()]
 return call(DOC,method,path,b''.join(parts),expected,who,role,'multipart/form-data; boundary='+boundary)

# Reuse only the synthetic DOCX fixture function; do not execute the old smoke script.
fixture_node=next(n for n in ast.parse((ROOT/'scripts/verify-documentform-online.py').read_text()).body if isinstance(n,ast.FunctionDef) and n.name=='fixture')
exec(compile(ast.Module(body=[fixture_node],type_ignores=[]),'<fixture>','exec'))
class Catalog(BaseHTTPRequestHandler):
 def do_GET(self):
  if self.path == '/api/v1/users/access-context':
   payload=self.headers['Authorization'].split('.')[1];claims=json.loads(base64.urlsafe_b64decode(payload+'='*(-len(payload)%4)))
   rights=['workflow.'+x for x in ['create','read','checklist.write','submit','resubmit','comments.read','versions.read','diff.read']]
   if claims['role']=='FRONT_DESK_OFFICER': rights += ['workflow.'+x for x in ['assign','comments.write','revision.request','approve']]
   result={'userId':claims['sub'],'wardCode':'WARD_A','roles':[claims['role']],'permissions':rights}
  elif self.path == '/api/v1/wards': result=[{'code':'WARD_A'}]
  else: result={'id':procedure,'title':'Synthetic procedure','isActive':True,'contentPayload':{'cases':[]},'checklistSchema':[{'checklistId':'FORM','itemName':'Electronic form','isMandatory':True},{'checklistId':'ID','itemName':'Identity attachment','isMandatory':True}]}
  if 'checklistSchema' in result:
   result['formDefinitions']=[{'formCode':'FORM','formName':'Electronic form','formTemplateId':t,'isMandatory':True}]
  data=json.dumps(result).encode()
  self.send_response(200);self.send_header('Content-Type','application/json');self.end_headers();self.wfile.write(data)
 def log_message(self,*args): pass

def run(*args,**kw): return subprocess.run(args,check=True,capture_output=True,**kw)

processes=[]; container='wm-link-test-'+uuid.uuid4().hex[:8]; server=None; blob_container=container+'-blob'; api_containers=[]
with tempfile.TemporaryDirectory(prefix='wm-link-') as tmp:
 logs=[]
 try:
  env=os.environ.copy();env['POSTGRES_PASSWORD']=secrets.token_urlsafe(24); password=env['POSTGRES_PASSWORD']
  run('docker','run','-d','--rm','--name',container,'-p','127.0.0.1:15439:5432','-e','POSTGRES_PASSWORD','postgres:16-alpine',env=env)
  run('docker','run','-d','--rm','--name',blob_container,'-p','127.0.0.1:10000:10000','mcr.microsoft.com/azure-storage/azurite','azurite-blob','--blobHost','0.0.0.0','--skipApiVersionCheck')
  for _ in range(60):
   if subprocess.run(['docker','exec',container,'pg_isready','-U','postgres'],capture_output=True).returncode==0:break
   time.sleep(.5)
  for name in ['document_test','workflow_test']:run('docker','exec',container,'createdb','-U','postgres',name)
  server=ThreadingHTTPServer(('127.0.0.1',15032),Catalog);threading.Thread(target=server.serve_forever,daemon=True).start()
  for name,port,db,conn in [('DocumentForm',15034,'document_test','DocumentDb'),('ApplicationWorkflow',15033,'workflow_test','WorkflowDatabase')]:
   e=os.environ.copy();e.update({'Jwt__Key':key,'Jwt__Issuer':'wardmate','Jwt__Audience':'wardmate-client','ASPNETCORE_ENVIRONMENT':'Development','ASPNETCORE_URLS':f'http://127.0.0.1:{port}','Database__AutoMigrate':'true','ConnectionStrings__'+conn:f'Host=127.0.0.1;Port=15439;Database={db};Username=postgres;Password={password}','AzureBlob__ConnectionString':'UseDevelopmentStorage=true','AzureBlob__ContainerName':'link-test-'+uuid.uuid4().hex,'Services__ApplicationWorkflow__BaseUrl':WF,'Services__DocumentForm__BaseUrl':DOC,'ProcedureCatalog__BaseUrl':'http://127.0.0.1:15032/'})
   e.update({'Iam__BaseUrl':'http://127.0.0.1:15032/','RabbitMq__Enabled':'false'})
   project=ROOT/f'src/Services/WardMate.Services.{name}/WardMate.Services.{name}.API';log=open(Path(tmp)/(name+'.log'),'w',encoding='utf-8');logs.append(log)
   if os.environ.get('WM_TEST_DOCKER') == '1':
    overrides={k:v for k,v in e.items() if k.startswith(('Jwt__','ASPNETCORE_','Database__','ConnectionStrings__','AzureBlob__','Services__','ProcedureCatalog__','Iam__','RabbitMq__'))}
    overrides={k:v.replace('127.0.0.1','host.docker.internal') for k,v in overrides.items()}
    overrides['ASPNETCORE_URLS']='http://+:8080'
    overrides['AzureBlob__ConnectionString']='UseDevelopmentStorage=true;DevelopmentStorageProxyUri=http://host.docker.internal'
    runtime=os.environ.copy();runtime.update(overrides)
    api_name=container+'-'+name.lower();api_containers.append(api_name)
    args=['docker','run','-d','--rm','--name',api_name,'-p',f'127.0.0.1:{port}:8080']
    for k in overrides:args += ['-e',k]
    args += ['wardmate-'+name.lower()+':application-link-local']
    run(*args,env=runtime)
   else:
    processes.append(subprocess.Popen(['dotnet',str(project/f'bin/Release/net8.0/WardMate.Services.{name}.API.dll')],cwd=project,env=e,stdout=log,stderr=log))
  for base in [DOC,WF]:
   for _ in range(90):
    try:
     urllib.request.urlopen(base+'/health',timeout=1);break
    except (urllib.error.URLError,TimeoutError,http.client.RemoteDisconnected,ConnectionResetError): time.sleep(.5)
   else: raise AssertionError('API startup failed')
  original=fixture(); t=call(DOC,'POST','/api/v1/form-templates',{'code':'LINK-'+uuid.uuid4().hex[:8],'title':'Synthetic template'},201,role='PROCEDURE_MANAGER')['id']
  multi('/api/v1/form-templates/'+t+'/upload-docx',{},original,role='PROCEDURE_MANAGER')
  assert call(DOC,'GET','/api/v1/form-templates/'+t+'/download-docx',raw=True)==original
  def app(): return call(WF,'POST','/api/v1/applications',{'procedureId':procedure,'formData':{},'wardCode':'WARD_A'},201)
  a=app();a2=app();aid=a['id']; ck={c['code']:c['id'] for c in a['checklists']}; c='/api/v1/citizen/submissions'; ap='/api/v1/applications/'+aid
  fields={'TemplateId':t,'ApplicantId':user,'ApplicationId':aid,'ChecklistId':ck['FORM']}
  assert next(c for c in a['checklists'] if c['code']=='FORM')['formTemplateId']==t
  multi(c+'/draft',{**fields,'TemplateId':str(uuid.uuid4())},original,400)
  multi(ap+'/supporting-documents',{'ChecklistId':ck['FORM']},b'%PDF-synthetic',400,filename='wrong-kind.pdf')
  multi(c+'/draft',{**fields,'ApplicationId':str(uuid.uuid4())},original,404)
  multi(c+'/draft',{**fields,'ChecklistId':str(uuid.uuid4())},original,400)
  multi(c+'/draft',{**fields,'ApplicantId':other},original,404,who=other)
  sid=multi(c+'/draft',fields,original,201)['submissionId']; sp=c+'/'+sid
  multi(c+'/draft',fields,original,409)
  meta=call(DOC,'GET',sp+'?applicantId='+user);assert meta['applicationId']==aid and meta['checklistId']==ck['FORM']
  multi(sp+'/draft',{'ApplicantId':user},original,method='PUT')
  call(DOC,'GET',ap+'/contents',who=other,expected=404)
  contents=call(DOC,'GET',ap+'/contents');assert [s['id'] for s in contents['submissions']]==[sid] and not contents['supportingDocuments']
  assert not call(DOC,'GET','/api/v1/applications/'+a2['id']+'/contents')['submissions']
  call(WF,'PATCH',ap+'/checklists/'+ck['FORM'],{'status':'COMPLETED'},422)
  call(DOC,'POST',sp+'/submit',{'applicantId':user})
  call(WF,'PATCH',ap+'/checklists/'+ck['FORM'],{'status':'COMPLETED'})
  call(WF,'POST',ap+'/submit',expected=422)
  doc=multi(ap+'/supporting-documents',{'ChecklistId':ck['ID']},b'%PDF-synthetic',201,filename='test.pdf')['id']
  dp='/api/v1/supporting-documents/'+doc
  call(DOC,'GET',dp+'/download',who=other,expected=404)
  call(DOC,'DELETE',dp,who=other,expected=404)
  assert call(DOC,'GET',dp+'/download',raw=True)==b'%PDF-synthetic'
  call(WF,'PATCH',ap+'/checklists/'+ck['ID'],{'status':'COMPLETED'})
  call(DOC,'DELETE',dp,expected=204)
  call(WF,'POST',ap+'/submit',expected=422)
  multi(ap+'/supporting-documents',{'ChecklistId':ck['ID']},b'%PDF-synthetic',201,filename='replacement.pdf')
  contents=call(DOC,'GET',ap+'/contents');assert len(contents['submissions'])==1 and len(contents['supportingDocuments'])==1
  multi('/api/v1/applications/'+a2['id']+'/generate-pdf',{'submissionId':sid},None,404)
  result=call(WF,'POST',ap+'/submit');assert result['status']=='SUBMITTED'
  multi(ap+'/supporting-documents',{'ChecklistId':ck['ID']},original,409)
  call(DOC,'GET',ap+'/contents')
  multi(sp+'/draft',{'ApplicantId':user},original,409,method='PUT')
  officer=str(uuid.uuid4()); op='/api/v1/officer/applications/'+aid
  call(WF,'POST',op+'/assign',who=officer,role='FRONT_DESK_OFFICER')
  call(WF,'POST',op+'/comments',{'versionNumber':1,'targetType':'CHECKLIST_ITEM','targetId':ck['FORM'],'fieldLabel':'Form','commentText':'Please revise'},who=officer,role='FRONT_DESK_OFFICER')
  call(WF,'POST',op+'/request-revision',{'reason':'Please revise the form'},who=officer,role='FRONT_DESK_OFFICER')
  multi(sp+'/draft',{'ApplicantId':user},original,method='PUT')
  retry={'expectedVersionNumber':1,'formData':{},'checklists':[{'id':v,'status':'COMPLETED'} for v in ck.values()]}
  call(WF,'POST',ap+'/resubmit',retry,422)
  call(DOC,'POST',sp+'/submit',{'applicantId':user})
  call(WF,'POST',ap+'/resubmit',retry)
  multi(sp+'/draft',{'ApplicantId':user},original,409,method='PUT')
  print(f'PASS: {checks} local HTTP checks; isolated databases; synthetic IAM/ProcedureCatalog; no Azure requests.')
 finally:
  for api_name in api_containers:subprocess.run(['docker','rm','-f',api_name],capture_output=True)
  for p in processes:
   p.terminate()
   try:p.wait(timeout=10)
   except subprocess.TimeoutExpired:p.kill()
  for log in logs:log.close()
  if server:server.shutdown()
  subprocess.run(['docker','rm','-f',container],capture_output=True)
  subprocess.run(['docker','rm','-f',blob_container],capture_output=True)
