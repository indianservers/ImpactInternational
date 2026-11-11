"""Real PostgreSQL finance workflow tests. Fictional records; no live payments."""
from support import Client,sql
import uuid,json,pathlib,datetime,concurrent.futures,subprocess
results=[];tag=uuid.uuid4().hex[:8];today=datetime.date.today();date=today.isoformat();start=f'{today.year}-01-01';end=f'{today.year}-12-31'
def check(name,ok,data=None):
 results.append({'test':name,'passed':bool(ok)});pathlib.Path('test-results/phase8.json').write_text(json.dumps(results,indent=2));print(('PASS ' if ok else 'FAIL ')+name,flush=True);assert ok,(name,data)
def good(c,p,m='GET',d=None):
 code,b=c.req(p,m,d);assert code in (200,204),(p,code,b);return b
def login(email,role,org=None):
 c=Client();assert c.login(email)[0]==200;ws=good(c,'/workspaces');w=next(w for w in ws if w['role']==role and (not org or w['orgId']==org) and (role!='Association Super Admin' or w['association'].startswith('Global')));c.activate(w);return c,w
admin,w=login('admin@example.test','Association Super Admin');review,rw=login('secretary@example.test','Association Super Admin',w['orgId']);club=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'GC-006\'');member,mw=login('member@example.test','Club Member',club)
pid=sql('SELECT "Id" FROM "Persons" WHERE "UserId"=(SELECT "Id" FROM "Users" WHERE "Email"=\'member@example.test\')');mid=sql(f'SELECT "Id" FROM "Memberships" WHERE "PersonId"=\'{pid}\' AND "ClubId"=\'{club}\' AND "Status"=\'Active\' LIMIT 1');reviewid=sql('SELECT "Id" FROM "Users" WHERE "Email"=\'secretary@example.test\'')

from urllib.parse import quote
import urllib.request,time,fitz
base='/intelligence'
cat=good(admin,base+'/metrics');check('catalog supplies formulas and privacy',len(cat)>=12 and all(x['numerator'] and x['privacy'] and x['freshness'] for x in cat))
d=good(admin,base+'/dashboard');metrics={m['id']:m for m in d['metrics']};check('affiliations and people are separate',metrics['active-memberships']['value']>=metrics['active-people']['value']);check('stock vs flow policy disclosed','current snapshots' in d['notes']);check('member cannot access officer metrics',member.req(base+'/dashboard')[0]==403)
c=good(admin,base+'/dashboard?orgId='+club);check('child drilldown scopes correctly',c['scope']==club);check('invalid time interval rejected',admin.req(base+'/dashboard?from=2020-01-01&to=2026-01-01')[0]==400)
check('CSV export works',admin.request(base+'/export')[0]==200)
check('literal wildcard does not enumerate records',len(good(admin,base+'/search?query='+quote('%%'))['items'])==0)
check('short query rejected',admin.req(base+'/search?query=x')[0]==400)
sr=good(admin,base+'/search?query=Maya');check('authorized member search returns bounded results',len(sr['items'])<=100 and 'privacy' in sr)
check('search does not expose contact details','email' not in json.dumps(sr).lower())
for question,tool in [('My membership status','my_membership'),('My certificates','my_certificates'),('Upcoming events','upcoming_events'),('My outstanding dues','finance_summary')]:
 answer=good(member,base+'/assistant','POST',{'question':question,'tool':tool});check('read-only tool '+tool,answer['tool']==tool)
check('assistant rejects mutation request',member.req(base+'/assistant','POST',{'question':'delete all members'})[0]==400)
check('assistant rejects unknown tool',admin.req(base+'/assistant','POST',{'question':'show members','tool':'run_sql'})[0]==400)
check('remote AI cannot run without tenant opt-in',admin.req(base+'/assistant','POST',{'question':'Membership count','useRemoteModel':True,'acknowledgeProvider':True})[0] in (403,409))
check('audit stores hash instead of question',sql('SELECT count(*) FROM "IntelligenceAudits" WHERE length("QueryHash") <> 64')=='0')
# Real text PDF, not a fake PDF header.
pdf=fitz.open();page=pdf.new_page();page.insert_text((72,72),'Fictional charter: quorum requires seven voting members.');content=pdf.tobytes();pdf.close()
code,doc=admin.upload('/documents','charter.pdf',content,{'orgId':club,'title':'Private charter '+tag,'visibility':'Private'},'application/pdf');check('real PDF upload',code==200,doc)
idx=good(admin,base+'/knowledge/'+doc['id']+'/index','POST',{});check('real PDF extraction indexed',int(sql('SELECT count(*) FROM "KnowledgePassages" WHERE "DocumentId"=\''+doc['id']+'\''))==1)
found=good(admin,base+'/knowledge?query=quorum');check('authorized knowledge source has page and hash',any(x['documentId']==doc['id'] and x['page']==1 and len(x['sourceHash'])==64 for x in found['items']))
check('private source absent for member',not any(x['documentId']==doc['id'] for x in good(member,base+'/knowledge?query=quorum')['items']))
check('private document absent from universal search',not any(x.get('id')==doc['id'] for x in good(member,base+'/search?query='+quote('Private charter '+tag))['items']))
check('private index metadata inaccessible',member.req(base+'/knowledge/indexes')[0]==403)
check('unapproved knowledge not described as approved',not any(x['documentId']==doc['id'] for x in good(admin,base+'/knowledge?query=approved%20quorum')['items']))
# Shared governance engine: drafts and dry runs cannot execute changes.
ruleinput={'orgId':club,'name':'Reviewed QA '+tag,'definition':{'trigger':'TaskOverdue','condition':{},'steps':[{'action':'NotifyCreator','delayMinutes':1,'branch':'Match'}],'days':7},'dailyLimit':5}
r=good(admin,base+'/workflows','POST',ruleinput);edited=good(admin,base+'/workflows/'+r['id'],'PUT',{**ruleinput,'name':ruleinput['name']+' edited','version':r['version']});check('draft edits increment version',edited['version']==r['version']+1);check('stale draft edit rejected',admin.req(base+'/workflows/'+r['id'],'PUT',{**ruleinput,'version':r['version']})[0]==409);r=edited;check('workflow created inactive draft',not r['active'] and r['status']=='Draft');dry=good(admin,base+'/workflows/'+r['id']+'/test','POST',{});check('dry run has zero side effects',dry['dryRun'] and dry['actionsExecuted']==0)
bad={**ruleinput,'definition':{**ruleinput['definition'],'steps':[{'action':'PostJournal'}]}};check('unreviewable workflow action rejected',admin.req(base+'/workflows','POST',bad)[0]==400)
check('ordinary member cannot create workflow',member.req(base+'/workflows','POST',ruleinput)[0]==403)
check('liveness and readiness respond',urllib.request.urlopen('http://127.0.0.1:5081/health/live').status==200 and urllib.request.urlopen('http://127.0.0.1:5081/health/ready').status==200)
ops=good(admin,base+'/operations');check('diagnostics do not claim production readiness',ops['gates']['productionReady']==False)
check('unsafe domain rejected',admin.req(base+'/domains','POST',{'host':'127.0.0.1'})[0]==400)
check('domain URL rejected',admin.req(base+'/domains','POST',{'host':'https://example.org'})[0]==400)
domain=good(admin,base+'/domains','POST',{'host':'qa-'+tag+'.example.org'});check('domain begins pending ownership',domain['domain']['status']=='Pending');check('pending domain cannot activate',admin.req(base+'/domains/'+domain['domain']['id']+'/activate','POST',{})[0] in (400,403,409))
other=next(x for x in good(admin,'/workspaces') if x['tenantId']!=w['tenantId']);admin.activate(other);check('cross tenant knowledge index denied',admin.req(base+'/knowledge/'+doc['id']+'/index','POST',{})[0]==404);admin.activate(w)
# Independent review plus actual durable execution.
task=good(admin,'/tasks','POST',{'orgId':club,'title':'Overdue workflow source '+tag,'dueAt':(datetime.datetime.now(datetime.timezone.utc)-datetime.timedelta(hours=1)).isoformat()})
ruleinput['definition']['steps']=[{'action':'CreateFollowUpTask','branch':'Match','delayMinutes':0}]
r=good(admin,base+'/workflows','POST',ruleinput);submitted=good(admin,base+'/workflows/'+r['id']+'/submit','POST',{});request=submitted['request']
check('requester cannot approve own workflow',admin.req('/governance/requests/'+request['id']+'/decisions','POST',{'decision':'Approve','comment':'QA','version':request['version']})[0]==403)
good(review,'/governance/requests/'+request['id']+'/decisions','POST',{'decision':'Approve','comment':'Independent bounded follow-up review','version':request['version']})
deadline=time.monotonic()+35;runs=[]
while time.monotonic()<deadline:
 runs=good(admin,base+'/workflows/'+r['id']+'/executions')
 if any(x['status']=='Completed' and x['generatedResourceId'] for x in runs):break
 time.sleep(1)
check('approved worker executes durable task action',any(x['status']=='Completed' and x['generatedResourceId'] for x in runs),runs)
check('execution stores immutable definition and trace',all(x['definitionSnapshot'] and x['ruleVersion']>0 and json.loads(x['trace']) for x in runs if x['status']=='Completed'))
copy=good(admin,base+'/workflows/'+r['id']+'/copy','POST',{});check('revision is a fresh inactive draft',copy['status']=='Draft' and not copy['active'] and copy['id']!=r['id'])
check('reviewed workflow cannot be edited through API',admin.req(base+'/workflows/'+r['id'],'PUT',{**ruleinput,'version':3})[0]==409);cancelled=good(admin,base+'/workflows/'+r['id']+'/cancel','POST',{});check('cancellation disables future execution',not cancelled['active'])
check('private index omitted for another document administrator',not any(x['documentId']==doc['id'] for x in good(review,base+'/knowledge/indexes')))
check('knowledge content is never returned in index listing','text' not in json.dumps(good(admin,base+'/knowledge/indexes')).lower())
sql(f"UPDATE \"GovernanceDocuments\" SET \"RetainUntil\"=now()-interval '1 day' WHERE \"Id\"='{doc['id']}'")
check('expired knowledge immediately excluded',not any(x['documentId']==doc['id'] for x in good(admin,base+'/knowledge?query=quorum')['items']))
check('expired index metadata immediately excluded',not any(x['documentId']==doc['id'] for x in good(admin,base+'/knowledge/indexes')))
clubmetrics={m['id']:m for m in good(admin,base+'/dashboard?orgId='+club)['metrics']};expected=int(sql(f"SELECT count(*) FROM \"Memberships\" WHERE \"ClubId\"='{club}' AND \"Status\"='Active' AND (\"EndsAt\" IS NULL OR \"EndsAt\">now())"));check('club stock metric reconciles to independent SQL',clubmetrics['active-memberships']['value']==expected)
def guarded(query):
 try:sql(query);return False
 except subprocess.CalledProcessError:return True
check('database prevents reviewed workflow content edits',guarded(f"UPDATE \"CommunicationAutomations\" SET \"Definition\"='{{}}' WHERE \"Id\"='{r['id']}'"))
run=next(x for x in runs if x['status']=='Completed');check('database prevents execution snapshot rewrite',guarded(f"UPDATE \"CommunicationExecutions\" SET \"DefinitionSnapshot\"='{{}}' WHERE \"Id\"='{run['id']}'"))
bad={**ruleinput,'definition':{**ruleinput['definition'],'steps':[{'action':'CreateFollowUpTask'},{'action':'CreateFollowUpTask'}]}};check('multiple task producers blocked against loops',admin.req(base+'/workflows','POST',bad)[0]==400)
bad={**ruleinput,'definition':{**ruleinput['definition'],'steps':[{'action':'NotifyCreator','delayMinutes':10081}]}};check('delay bounded to seven days',admin.req(base+'/workflows','POST',bad)[0]==400)
print(len(results),'Phase 8 checks passed')


