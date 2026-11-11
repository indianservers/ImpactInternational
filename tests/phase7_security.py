"""Real PostgreSQL finance workflow tests. Fictional records; no live payments."""
from support import Client,sql
import uuid,json,pathlib,datetime,concurrent.futures,subprocess
results=[];tag=uuid.uuid4().hex[:8];today=datetime.date.today();date=today.isoformat();start=f'{today.year}-01-01';end=f'{today.year}-12-31'
def check(name,ok,data=None):
 results.append({'test':name,'passed':bool(ok)});pathlib.Path('test-results/phase7-security.json').write_text(json.dumps(results,indent=2));print(('PASS ' if ok else 'FAIL ')+name,flush=True);assert ok,(name,data)
def good(c,p,m='GET',d=None):
 code,b=c.req(p,m,d);assert code in (200,204),(p,code,b);return b
def login(email,role,org=None):
 c=Client();assert c.login(email)[0]==200;ws=good(c,'/workspaces');w=next(w for w in ws if w['role']==role and (not org or w['orgId']==org) and (role!='Association Super Admin' or w['association'].startswith('Global')));c.activate(w);return c,w
admin,w=login('admin@example.test','Association Super Admin');review,rw=login('secretary@example.test','Association Super Admin',w['orgId']);club=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'GC-006\'');member,mw=login('member@example.test','Club Member',club)
pid=sql('SELECT "Id" FROM "Persons" WHERE "UserId"=(SELECT "Id" FROM "Users" WHERE "Email"=\'member@example.test\')');mid=sql(f'SELECT "Id" FROM "Memberships" WHERE "PersonId"=\'{pid}\' AND "ClubId"=\'{club}\' AND "Status"=\'Active\' LIMIT 1');reviewid=sql('SELECT "Id" FROM "Users" WHERE "Email"=\'secretary@example.test\'')
def entity(name):
 e=good(admin,'/finance/entities','POST',{'orgId':club,'name':name+' '+tag,'country':'US','currency':'USD','decimals':2,'collectionAuthority':'Fictional QA independent club collection'});p='/finance/entities/'+e['id'];good(admin,p+'/periods','POST',{'name':'Fiscal year','startsOn':start,'endsOn':end});good(admin,p+'/grants','POST',{'userId':reviewid,'role':'Owner'});return e,p

f=json.loads(pathlib.Path('test-results/phase7-fixture.json').read_text());e=f['entity'];p=f['path'];adminid=sql('SELECT "Id" FROM "Users" WHERE "Email"=\'admin@example.test\'')
private=good(review,'/finance/entities','POST',{'orgId':club,'name':'Independent private fund '+tag,'country':'US','currency':'USD'});pp='/finance/entities/'+private['id'];check('hierarchy super admin cannot read ungranted books',admin.req(pp+'/dashboard')[0]==403);check('ungranted entity absent from directory',not any(x['id']==private['id'] for x in good(admin,'/finance/entities')))
good(review,pp+'/grants','POST',{'userId':adminid,'role':'Reader'});check('explicit reader can read entity',good(admin,pp+'/dashboard')['cashBank']==0);check('reader cannot create financial entries',admin.req(pp+'/invoices','POST',{'customer':'Forbidden','purpose':'Other','date':date,'dueOn':date,'key':uuid.uuid4().hex,'lines':[{'description':'No','quantity':1,'rate':'1'}]})[0]==403);check('reader cannot expand own entity authority',admin.req(pp+'/grants','POST',{'userId':adminid,'role':'Owner'})[0]==403);good(review,pp+'/grants/'+adminid,'DELETE');check('entity grant revocation persists',admin.req(pp+'/dashboard')[0]==403)
inv={'personId':pid,'customer':'Fictional member','purpose':'Other','date':date,'dueOn':date,'key':uuid.uuid4().hex,'lines':[{'description':'Frozen hash','quantity':1,'rate':'10'}]};i=good(admin,p+'/invoices','POST',inv);check('invoice request retry idempotent',good(admin,p+'/invoices','POST',inv)['id']==i['id']);check('invoice key cannot change request details',admin.req(p+'/invoices','POST',{**inv,'lines':[{'description':'Changed','quantity':1,'rate':'11'}]})[0]==409)
check('null financial nested fields rejected',admin.req(p+'/invoices','POST',{**inv,'key':uuid.uuid4().hex,'lines':[{'description':None,'quantity':1,'rate':'1'}]})[0]==400);check('null collection rejected',admin.req(p+'/invoices','POST',{**inv,'key':uuid.uuid4().hex,'lines':None})[0]==400)
check('wrong native payment currency rejected',admin.req(p+'/payments','POST',{'personId':pid,'payer':'Fictional member','reference':uuid.uuid4().hex,'method':'Cash','currency':'EUR','amount':'1','date':date,'key':uuid.uuid4().hex})[0]==400)
check('gateway alias cannot bind another entity',admin.req(p+'/gateway','PUT',{'reason':'unbound-entity'})[0]==400)
good(admin,'/finance/invoices/'+i['id']+'/issue','POST',{})
def rejected_sql(query):
 try:sql(query);return False
 except subprocess.CalledProcessError:return True
check('database rejects manually inflated paid balance',rejected_sql(f'UPDATE "FinanceInvoices" SET "Paid"=1 WHERE "Id"=\'{i["id"]}\''))
line=sql(f'SELECT "Id" FROM "FinanceInvoiceLines" WHERE "InvoiceId"=\'{i["id"]}\' LIMIT 1');check('database prevents issued line replacement',rejected_sql(f'UPDATE "FinanceInvoiceLines" SET "Rate"=1 WHERE "Id"=\'{line}\''))
key=uuid.uuid4().hex;manual={'date':date,'key':key,'narration':'Idempotent manual journal','lines':[{'account':'1010','debit':'1','credit':'0'},{'account':'3000','debit':'0','credit':'1'}]};good(admin,p+'/journals','POST',manual);check('manual journal same key different totals rejected',admin.req(p+'/journals','POST',{**manual,'lines':[{'account':'1010','debit':'2','credit':'0'},{'account':'3000','debit':'0','credit':'2'}]})[0]==409)
# Temporary role proves payer/subject conflict checks are independent of administrative privilege.
prior={x['id'] for x in good(member,'/workspaces')};superrole=next(r for r in good(admin,'/roles') if r['name']=='Association Super Admin');good(admin,'/assignments','POST',{'email':'member@example.test','roleId':superrole['id'],'orgId':w['orgId']});elevated=next(x for x in good(member,'/workspaces') if x['id'] not in prior and x['role']=='Association Super Admin');member.activate(elevated);memberid=sql('SELECT "Id" FROM "Users" WHERE "Email"=\'member@example.test\'');good(admin,p+'/grants','POST',{'userId':memberid,'role':'Owner'})
try:
 pa=good(admin,p+'/payments','POST',{'personId':pid,'payer':'Fictional member','reference':'OWN-'+tag,'method':'Cash','currency':'USD','amount':'1','date':date,'key':uuid.uuid4().hex});check('privileged payer cannot verify own collection',member.req('/finance/payments/'+pa['id']+'/verify','POST',{})[0]==403)
finally:
 good(admin,p+'/grants/'+memberid,'DELETE');good(admin,'/assignments/'+elevated['id'],'DELETE');member.activate(mw)
rate=good(admin,p+'/exchange-rates','POST',{'currency':'EUR','rate':'0.92345678','date':date,'source':'Immutable QA rate'});converted=good(admin,p+'/convert','POST',{'amount':'20','currency':'EUR','date':date,'decimals':2});check('decimal historical reporting conversion',converted['nativeAmount']==20 and converted['reportingAmount']==18.47 and converted['rate']==0.92345678);check('conversion keeps native journal currency',good(admin,'/finance/invoices/'+i['id'])['invoice']['currency']=='USD')
for _ in range(30):
 pending=int(sql("SELECT count(*) FROM \"Mails\" WHERE \"Kind\" LIKE 'finance.%' AND \"Channel\"='InApp' AND \"UserId\" IS NOT NULL AND \"SentAt\" IS NOT NULL"))
 if pending>0:break
 import time;time.sleep(.5)
check('finance reuses delivered in-app outbox',pending>0);check('unlinked people never enqueue null user in-app notifications',sql("SELECT count(*) FROM \"Mails\" WHERE \"Kind\" LIKE 'finance.%' AND \"Channel\"='InApp' AND \"UserId\" IS NULL AND NOT \"Suppressed\"")=='0');
# Partial credit rounding and zero-valued concessions cannot create tax drift.
zero=good(admin,p+'/invoices','POST',{'personId':pid,'customer':'Fictional member','purpose':'Other','date':date,'dueOn':date,'key':uuid.uuid4().hex,'lines':[{'description':'Exempt component','quantity':1,'rate':'1','discount':'1'},{'description':'Small taxed component','quantity':1,'rate':'0.05','taxPercent':'20'}]});good(admin,'/finance/invoices/'+zero['id']+'/issue','POST',{})
for _ in range(3):good(admin,'/finance/invoices/'+zero['id']+'/credits','POST',{'amount':'0.02','reason':'Small authorized return','key':uuid.uuid4().hex})
check('repeated partial credit handles zero line and exact tax',good(admin,'/finance/invoices/'+zero['id'])['invoice']['outstanding']==0 and sql(f'SELECT sum(l."Debit") FROM "FinanceJournalLines" l JOIN "FinanceJournals" j ON l."JournalId"=j."Id" JOIN "FinanceAccounts" a ON a."Id"=l."AccountId" WHERE a."Code"=\'2100\' AND j."Source" IN (SELECT \'credit:\'||"Id"::text FROM "FinanceCredits" WHERE "InvoiceId"=\'{zero["id"]}\')')=='0.01000000')
print(len(results),'security finance checks passed')
