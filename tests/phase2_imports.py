"""Import staging, duplicate resolution, atomic commit, recovery and XLSX validation."""
from support import Client,sql
import uuid,time,json,pathlib,io,csv,os
LARGE_ROWS=int(os.environ.get("CLUBS_IMPORT_ROWS","1000"))
from openpyxl import Workbook
results=[]
def check(name,ok,data=None):
 results.append({'test':name,'passed':bool(ok)});print(('PASS ' if ok else 'FAIL ')+name,'' if ok else str(data)[:600]);pathlib.Path('test-results').mkdir(exist_ok=True);pathlib.Path('test-results/phase2-imports.json').write_text(json.dumps({'tests':results},indent=2));assert ok,(name,data)
def good(c,p,m='GET',b=None,status=200):
 code,data=c.req(p,m,b);check(m+' '+p.split('?')[0],code==status,data);return data
def poll(c,id,states,seconds=60):
 until=time.monotonic()+seconds
 while time.monotonic()<until:
  status,data=c.req('/membership/imports/'+id)
  if status!=200:raise AssertionError((status,data))
  if data['status'] in states:return data
  if data['status']=='Failed' and 'Failed' not in states:raise AssertionError(data['error'])
  time.sleep(.4)
 raise AssertionError('Import did not reach '+str(states))
def content(rows):
 out=io.StringIO();writer=csv.writer(out);writer.writerow(['Name','Email','Phone','ClubCode']);writer.writerows(rows);return out.getvalue().encode()
admin=Client();admin.login('admin@example.test');root=next(w for w in admin.req('/workspaces')[1] if w['role']=='Association Super Admin' and w['association'].startswith('Global'));admin.activate(root)
opts=good(admin,'/membership/options');source=next(c for c in opts['clubs'] if c['name']=='Vijayawada Community Club');destination=next(c for c in opts['clubs'] if c['name']=='Visakhapatnam Service Club');config=good(admin,'/membership/config');category=next(c for c in config['categories'] if c['code']=='REG')
def upload(client,payload,club=destination,filename='members.csv',request_key=None):
 key=request_key or uuid.uuid4().hex;status,data=client.upload('/membership/imports',filename,payload,{'clubId':club['id'],'categoryId':category['id'],'requestKey':key});check('stage CSV or Excel import',status==200,data);return data,key
# An invalid row prevents every row from being committed.
job,key=upload(admin,content([['','invalid-email','bad phone',''],['Valid person','valid@example.test','','']]))
good(admin,'/membership/imports/'+job['id']+'/mapping','POST',{'fields':{'name':'Name','email':'Email','phone':'Phone','clubCode':'ClubCode'}},202);state=poll(admin,job['id'],['NeedsReview']);check('bulk import validation reports invalid rows',state['totalErrors']>=1 and state['importedRows']==0)
check('invalid batch cannot commit',admin.req('/membership/imports/'+job['id']+'/commit','POST',{})[0]==409);good(admin,'/membership/imports/'+job['id']+'/cancel','POST',{},204);check('cancel before final commit',admin.req('/membership/imports/'+job['id'])[1]['status']=='Cancelled')
# Shared contacts within a file need a conscious separate-identity decision.
prefix=uuid.uuid4().hex[:8];shared='+910000'+str(int(prefix[:6],16)).zfill(8);payload=content([['Import One '+prefix,'import.one.'+prefix+'@example.test',shared,''],['Import Two '+prefix,'import.two.'+prefix+'@example.test',shared,''],['Import Three '+prefix,'import.three.'+prefix+'@example.test','','']]);job,key=upload(admin,payload)
repeat,_=upload(admin,payload,request_key=key);check('upload retry is idempotent',repeat['id']==job['id'])
good(admin,'/membership/imports/'+job['id']+'/mapping','POST',{'fields':{'name':'Name','email':'Email','phone':'Phone','clubCode':'ClubCode'}},202);state=poll(admin,job['id'],['NeedsReview']);check('shared family mobile is flagged rather than merged',state['totalReview']>=1 and state['totalErrors']==0)
review=state['rows'];good(admin,'/membership/imports/'+job['id']+'/resolve','POST',{'rows':[{'rowId':r['id'],'createSeparate':True} for r in review if r['status']=='NeedsReview']});state=good(admin,'/membership/imports/'+job['id']);check('all duplicate decisions are explicit',state['status']=='Ready');good(admin,'/membership/imports/'+job['id']+'/commit','POST',{},202);state=poll(admin,job['id'],['Completed']);check('atomic import commits all reviewed rows',state['importedRows']==3 and sql(f'SELECT count(*) FROM "Memberships" m JOIN "MemberImportRows" r ON m."ImportRowId"=r."Id" WHERE r."JobId"=\'{job["id"]}\'')=='3')
good(admin,'/membership/imports/'+job['id']+'/commit','POST',{});check('commit retry does not duplicate memberships',sql(f'SELECT count(*) FROM "Memberships" m JOIN "MemberImportRows" r ON m."ImportRowId"=r."Id" WHERE r."JobId"=\'{job["id"]}\'')=='3');check('shared mobile creates distinct persons',sql(f'SELECT count(DISTINCT "PersonId") FROM "PersonContacts" WHERE "NormalizedValue"=\'{shared[1:]}\'')=='2')
# Choose an existing linked person explicitly and preserve verified details.
existing=next(r for r in good(admin,'/members?clubId='+source['id']+'&size=100')['items'] if r['name']=='Maya Sen');before=good(admin,'/members/'+existing['id'])['person'];email=next(c['value'] for c in before['contacts'] if c['kind']=='Email')
original_category=category;category=good(admin,'/membership/categories','POST',{'name':'Reviewed import '+prefix,'code':'IMP'+prefix,'validityMonths':12,'active':True})
job,key=upload(admin,content([['Do Not Overwrite Verified Name',email,'','']]))
good(admin,'/membership/imports/'+job['id']+'/mapping','POST',{'fields':{'name':'Name','email':'Email'}},202);state=poll(admin,job['id'],['NeedsReview']);good(admin,'/membership/imports/'+job['id']+'/resolve','POST',{'rows':[{'rowId':state['rows'][0]['id'],'existingPersonId':existing['personId']}]});good(admin,'/membership/imports/'+job['id']+'/commit','POST',{},202);state=poll(admin,job['id'],['Completed']);after=good(admin,'/members/'+existing['id'])['person'];check('import never overwrites verified profile information',after['legalName']==before['legalName'] and after['contacts']==before['contacts'])
category=original_category
# Real Excel workbook support, and rejected formula cells.
workbook=Workbook();sheet=workbook.active;sheet.append(['Name','Email']);sheet.append(['Excel Member '+prefix,'excel.'+prefix+'@example.test']);stream=io.BytesIO();workbook.save(stream);job,key=upload(admin,stream.getvalue(),filename='members.xlsx');good(admin,'/membership/imports/'+job['id']+'/mapping','POST',{'fields':{'name':'Name','email':'Email'}},202);state=poll(admin,job['id'],['Ready']);good(admin,'/membership/imports/'+job['id']+'/commit','POST',{},202);check('Excel memberships imported',poll(admin,job['id'],['Completed'])['importedRows']==1)
sheet.cell(2,1).value='=HYPERLINK("https://invalid.example","Unsafe")';stream=io.BytesIO();workbook.save(stream);check('Excel formula cells rejected without evaluation',admin.upload('/membership/imports','formula.xlsx',stream.getvalue(),{'clubId':destination['id'],'categoryId':category['id'],'requestKey':uuid.uuid4().hex})[0]==400)
# A scoped officer cannot import club codes outside their club.
secretary=Client();secretary.login('secretary@example.test');sw=next(w for w in secretary.req('/workspaces')[1] if w['role']=='Club Secretary' and w['orgId']==source['id']);secretary.activate(sw)
job,key=upload(secretary,content([['Outside club '+prefix,'scope.'+prefix+'@example.test','','GC-008']]),club=source);good(secretary,'/membership/imports/'+job['id']+'/mapping','POST',{'fields':{'name':'Name','email':'Email','clubCode':'ClubCode'}},202);state=poll(secretary,job['id'],['NeedsReview']);check('cross-club import assignment rejected',state['totalErrors']==1 and state['importedRows']==0)
# Worker authorization is checked again after a staged import is ready.
job,key=upload(secretary,content([['Recovery '+prefix,'recovery.'+prefix+'@example.test','','']]),club=source);good(secretary,'/membership/imports/'+job['id']+'/mapping','POST',{'fields':{'name':'Name','email':'Email'}},202);poll(secretary,job['id'],['Ready'])
good(admin,'/assignments/'+sw['id'],'DELETE',None,204);good(admin,'/membership/imports/'+job['id']+'/commit','POST',{},202);state=poll(admin,job['id'],['Failed']);check('revoked importing permission rolls back safely',state['importedRows']==0 and 'permission' in state['error'])
roles=good(admin,'/roles');role=next(r for r in roles if r['name']=='Club Secretary');good(admin,'/assignments','POST',{'email':'secretary@example.test','roleId':role['id'],'orgId':source['id']},204);new_scope=next(w for w in secretary.req('/workspaces')[1] if w['role']=='Club Secretary' and w['orgId']==source['id']);secretary.activate(new_scope)
good(secretary,'/membership/imports/'+job['id']+'/retry','POST',{},202);poll(secretary,job['id'],['Ready']);good(secretary,'/membership/imports/'+job['id']+'/commit','POST',{},202);check('authorized import retry is safe and idempotent',poll(secretary,job['id'],['Completed'])['importedRows']==1)
# A thousand-row batch validates and commits with bounded batches and observable progress.
large=content([['Batch Member '+prefix+' '+str(i),'batch.'+prefix+'.'+str(i)+'@example.test','',''] for i in range(LARGE_ROWS)]);job,key=upload(admin,large);started=time.perf_counter();good(admin,'/membership/imports/'+job['id']+'/mapping','POST',{'fields':{'name':'Name','email':'Email'}},202);state=poll(admin,job['id'],['Ready'],90);check(str(LARGE_ROWS)+'-row validation completes',state['processedRows']==LARGE_ROWS);good(admin,'/membership/imports/'+job['id']+'/commit','POST',{},202);state=poll(admin,job['id'],['Completed'],120);elapsed=time.perf_counter()-started;check(str(LARGE_ROWS)+'-row background import completes',state['importedRows']==LARGE_ROWS);pathlib.Path('test-results/phase2-import-performance.json').write_text(json.dumps({'rows':LARGE_ROWS,'validation_and_commit_seconds':round(elapsed,3),'conditions':'Local HTTP and PostgreSQL, 200-row EF batches within an atomic final transaction; includes worker polling delay.'},indent=2))
print(len(results),'import checks passed')
