"""Real-ledger checks for the advanced dues center. Writes only to the isolated QA snapshot."""
import datetime,json,os,pathlib,uuid,urllib.parse,time,statistics
from support import Client,sql,BASE
assert BASE=='http://127.0.0.1:5098'
assert os.environ['CLUBS_TEST_DATABASE'].startswith('clubs_impact_dues_center_qa_')
base='/finance/dues-center';results=[];tag=uuid.uuid4().hex[:9]
def check(name,ok,detail=None):
 results.append({'test':name,'passed':bool(ok),'detail':None if ok else str(detail)[:500]})
 pathlib.Path('docs/qa/advanced-dues-api-results.json').write_text(json.dumps(results,indent=2))
 print(('PASS ' if ok else 'FAIL ')+name,flush=True)
 assert ok,(name,detail)
def request(c,label,path,method='GET',body=None,expected=200):
 status,data=c.req(path,method,body);check(label,status==expected,(status,data));return data
def close(a,b):return abs(float(a)-float(b))<.005
admin=Client();check('Administrator signs in',admin.login('admin@example.test')[0]==200)
opts=request(admin,'Authorized options',base+'/options')
for report in opts['reports']:
 path=base+'/reports/'+report['id'];d=request(admin,'Report '+report['id'],path)
 check('Paginated '+report['id'],len(d['items'])<=50)
 for fmt,prefix,mime in [('csv',None,'text/csv'),('xlsx',b'PK','application/vnd.openxmlformats-officedocument'),('pdf',b'%PDF','application/pdf')]:
  status,body,headers=admin.request(path+'?format='+fmt)
  check(report['id']+' '+fmt+' export is valid',status==200 and mime in headers.get('Content-Type','') and (prefix is None or body.startswith(prefix)),status)
d=request(admin,'Live intelligence',base+'/intelligence');me=admin.req('/auth/me')[1];workspace=next(w for w in admin.req('/workspaces')[1] if w['id']==me['workspaceId'])
tree=f'''WITH RECURSIVE tree AS(SELECT "Id" FROM "Orgs" WHERE "Id"='{workspace['orgId']}' UNION ALL SELECT o."Id" FROM "Orgs" o JOIN tree t ON o."ParentId"=t."Id")'''
scope=f'''FROM "FinanceInvoices" i JOIN "Memberships" m ON m."Id"=i."MembershipId" WHERE i."TenantId"='{workspace['tenantId']}' AND i."State"='Issued' AND i."Purpose" IN('ClubDues','AssociationDues','DistrictDues','RegionalDues','ZonalDues','NationalDues','InternationalDues','JoiningFee','Renewal') AND m."ClubId" IN(SELECT "Id" FROM tree) AND i."Date">='{d['from']}' AND i."Date"<='{d['to']}' '''
truth=json.loads(sql(tree+''' SELECT coalesce(json_agg(t),'[]') FROM(SELECT i."Currency" AS currency,sum(i."Total") AS billed,sum(i."Paid") AS paid,sum(i."Credited") AS credits,sum(i."Total"-i."Paid"-i."Credited") AS pending '''+scope+''' GROUP BY i."Currency")t'''))
for expected in truth:
 s=next(x for x in d['summary'] if x['currency']==expected['currency']);cur=s['currency']
 for key,column in [('billed','billed'),('collected','paid'),('credits','credits'),('pending','pending')]:check(cur+' '+key+' matches SQL ledger',close(s[key],expected[column]))
 check(cur+' aging reconciles to outstanding',close(sum(x['amount'] for x in d['aging'] if x['currency']==cur),s['pending']))
 check(cur+' monthly generated reconciles',close(sum(x['generated'] for x in d['generated'] if x['currency']==cur),s['billed']))
 for level in ['National','Region','District','Zone','Club']:
  rows=[x for x in d['organizations'] if x['currency']==cur and x['level']==level]
  check(cur+' '+level+' generated rollup',close(sum(x['billed'] for x in rows),s['billed']))
  check(cur+' '+level+' collected rollup',close(sum(x['collected'] for x in rows),s['collected']))
check('Currencies are separate',len(d['summary'])>=2 and len({s['currency'] for s in d['summary']})==len(d['summary']))
check('Intelligence contains no private contact fields',not any(k in json.dumps(d).lower() for k in ['email','mobile','contactvalue']))
for suffix in ['?from=2026-12-31&to=2026-01-01','?currency=TOOLONG','?search='+'a'*101]:request(admin,'Invalid intelligence filter rejected '+suffix[:40],base+'/intelligence'+suffix,expected=400)
foreign='00000000-0000-0000-0000-000000000001'
for route in ['/intelligence','/members','/renewals','/collections','/adjustments']:
 request(admin,'Unknown scope denied '+route,base+route+'?orgId='+foreign,expected=404)
for source in ['', 'Gateway','Manual','Unallocated','Settled','Failed','Refunded']:
 c=request(admin,'Collection source '+(source or 'All'),base+'/collections?source='+source)
 check('Collection page bounded '+(source or 'All'),len(c['items'])<=50)
 if source in ['Gateway','Manual']:check('Source filter '+source,all(r['source']==source for r in c['items']))
for route in ['/members','/collections','/adjustments']:
 for fmt,prefix in [('csv',None),('xlsx',b'PK'),('pdf',b'%PDF')]:
  status,body,_=admin.request(base+route+'?format='+fmt);check(route+' '+fmt+' download',status==200 and (prefix is None or body.startswith(prefix)))
for days in [7,30,60,90]:
 r=request(admin,'Renewals '+str(days)+' days',base+'/renewals?days='+str(days));check('Renewal rows limited '+str(days),len(r['items'])<=50)
request(admin,'Invalid renewal horizon',base+'/renewals?days=12',expected=400)
for status in ['expired','completed']:request(admin,'Renewal status '+status,base+'/renewals?status='+status)
members=request(admin,'Member balances',base+'/members');check('Member aggregate rows bounded',len(members['items'])<=50)
member=members['items'][0];ledger=request(admin,'Complete member ledger',base+'/members/'+member['id']);check('Ledger membership matches',ledger['member']['id']==member['id'])
for s in ledger['summary']:check('Ledger balance identity '+s['currency'],close(s['billed']-s['collected']-s['credits'],s['pending']))
for fmt,prefix in [('csv',None),('xlsx',b'PK'),('pdf',b'%PDF')]:
 status,body,_=admin.request(base+'/members/'+member['id']+'?format='+fmt);check('Member statement '+fmt,status==200 and (prefix is None or body.startswith(prefix)))
president=Client();check('Club president signs in',president.login('club.president@example.test')[0]==200)
club=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'DEMO-R01-D01-Z01-C01\'');other=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'DEMO-R02-D01-Z01-C01\'')
foreignMember=sql(f'''SELECT "Id" FROM "Memberships" WHERE "ClubId"='{other}' LIMIT 1''')
for route in ['/intelligence','/members','/renewals','/collections','/adjustments']:
 request(president,'President cannot read other club '+route,base+route+'?orgId='+other,expected=404)
request(president,'Foreign member ledger denied',base+'/members/'+foreignMember,expected=404)
request(president,'Foreign member statement denied',base+'/members/'+foreignMember+'?format=csv',expected=404)
ordinaryId=str(uuid.uuid4());ordinaryAssignment=str(uuid.uuid4());ordinaryEmail='qa.dues.member.'+tag+'@example.test'
sql(f'''INSERT INTO "Users" ("Id","Email","PasswordHash","Verified","PlatformAdmin","FailedAttempts") SELECT '{ordinaryId}','{ordinaryEmail}',"PasswordHash",true,false,0 FROM "Users" WHERE "Email"='admin@example.test';
INSERT INTO "UserProfiles" ("Id","Name","Phone","Locale","TimeZone","Theme") VALUES ('{ordinaryId}','QA ordinary dues member','','en','Asia/Kolkata','light');
UPDATE "Persons" SET "UserId"='{ordinaryId}' WHERE "Id"=(SELECT m."PersonId" FROM "Memberships" m JOIN "Persons" p ON p."Id"=m."PersonId" WHERE m."ClubId"='{club}' AND p."UserId" IS NULL LIMIT 1);
INSERT INTO "Assignments" ("Id","UserId","TenantId","RoleId","OrgId") SELECT '{ordinaryAssignment}','{ordinaryId}',o."TenantId",r."Id",o."Id" FROM "Orgs" o JOIN "Roles" r ON r."TenantId"=o."TenantId" WHERE o."Id"='{club}' AND r."Name"='Club Member';''')
ordinary=Client();check('Member signs in',ordinary.login(ordinaryEmail)[0]==200)
ws=ordinary.req('/workspaces')[1];candidate=next((w for w in ws if w['role']=='Club Member'),None)
if candidate:ordinary.activate(candidate)
for route in ['/intelligence','/members','/renewals','/collections','/adjustments']:
 request(ordinary,'Member cannot use organizational '+route,base+route,expected=403)
request(ordinary,'Member own finances preserved','/my/finance')
for route in ['/intelligence','/members','/renewals','/collections','/adjustments']:
 request(Client(),'Anonymous denied '+route,base+route,expected=401)
plans=opts['plans'];check('Existing configured plans reused',len(plans)>0)
plan=next(p for p in plans if p['currency']=='INR');start=datetime.date.fromisoformat(plan['effectiveFrom']);start=(start.replace(day=1)+datetime.timedelta(days=32)).replace(day=1).isoformat()
full=next(p for p in admin.req('/finance/entities/'+plan['entityId']+'/dues-plans')[1] if p['id']==plan['id'])
plan=request(admin,'Configure fresh repeatable QA plan','/finance/entities/'+plan['entityId']+'/dues-plans','POST',dict(name='Advanced dues QA '+tag,chargeCode='advanced-dues-'+tag,billingOrgId=plan['billingOrgId'],purpose=plan['purpose'],effectiveFrom=plan['effectiveFrom'],effectiveTo=plan['effectiveTo'],months=plan['months'],graceDays=plan['graceDays'],components=json.loads(full['components'])))
preview=request(admin,'Hierarchy generation preview',base+'/generation/preview','POST',dict(planIds=[plan['id']],startsOn=start,orgId=plan['billingOrgId']))
check('Preview has eligible members',preview['eligible']>0)
check('Preview only emits configured club members',all(r['planId']==plan['id'] for r in preview['items']))
request(admin,'Generation requires selected plans',base+'/generation/preview','POST',dict(planIds=[],startsOn=start),400)
request(admin,'Null plan list rejected',base+'/generation/preview','POST',dict(planIds=None,startsOn=start),400)
request(admin,'Unknown plan denied',base+'/generation/preview','POST',dict(planIds=[foreign],startsOn=start),404)
request(president,'Foreign plan generation denied',base+'/generation/preview','POST',dict(planIds=[plan['id']],startsOn=start,orgId=other),404)
beforeMembership=sql('SELECT md5(string_agg("Id"::text||"Status"||coalesce("EndsAt"::text,\'\'),\'\' ORDER BY "Id")) FROM "Memberships"')
rows=[dict(planId=r['planId'],membershipId=r['membershipId'],planVersion=r['planVersion']) for r in preview['items'] if not r['existing'] and not r['waived']]
check('New assessment candidates available',len(rows)>0)
request(admin,'Duplicate reviewed rows rejected',base+'/generation','POST',dict(startsOn=start,rows=[rows[0],rows[0]]),400)
request(admin,'Stale plan version rejected',base+'/generation','POST',dict(startsOn=start,rows=[rows[0]|dict(planVersion=rows[0]['planVersion']+1)]),409)
generated=request(admin,'Generate reviewed assessments',base+'/generation','POST',dict(startsOn=start,rows=rows));check('Each reviewed row generates one invoice',generated['created']==len(rows))
again=request(admin,'Generation retry is repeat-safe',base+'/generation','POST',dict(startsOn=start,rows=rows));check('No duplicate invoices on retry',again['created']==0 and again['skipped']==len(rows))
afterMembership=sql('SELECT md5(string_agg("Id"::text||"Status"||coalesce("EndsAt"::text,\'\'),\'\' ORDER BY "Id")) FROM "Memberships"');check('Dues generation never renews membership',beforeMembership==afterMembership)
created=next(r for r in generated['items'] if r.get('invoiceId'));invoice=created['invoiceId'];before=admin.req(base+'/invoices/'+invoice)[1]['invoice']
creditPayload=dict(amount=10,reason='Discount: QA explicit approved posting authority',key='advanced-dues-'+tag)
credit=request(admin,'Auditable discount credit', '/finance/invoices/'+invoice+'/credits','POST',creditPayload)
again=request(admin,'Credit retry idempotency','/finance/invoices/'+invoice+'/credits','POST',creditPayload);check('Credit reference reused',credit['id']==again['id'])
after=admin.req(base+'/invoices/'+invoice)[1]['invoice'];check('Issued amount is immutable',before['total']==after['total']);check('Credit changes only balance',close(after['credited']-before['credited'],10))
request(admin,'Over-credit rejected','/finance/invoices/'+invoice+'/credits','POST',dict(amount=before['total']+1,reason='Invalid excessive waiver',key='bad-'+tag),400)
request(admin,'Empty bulk collection rejected',base+'/collections/bulk','POST',dict(batchId=str(uuid.uuid4()),rows=[]),400)
request(admin,'Empty reminders rejected',base+'/reminders','POST',dict(invoiceIds=[],template='QA'),400)
request(admin,'Null reminder template rejected',base+'/reminders','POST',dict(invoiceIds=[invoice],template=None),400)
request(admin,'Null generation rows rejected',base+'/generation','POST',dict(startsOn=start,rows=None),400)
request(ordinary,'Member cannot generate dues',base+'/generation','POST',dict(startsOn=start,rows=rows),403)
request(ordinary,'Member cannot record bulk collections',base+'/collections/bulk','POST',dict(batchId=str(uuid.uuid4()),rows=[]),403)
request(ordinary,'Member cannot queue organizational reminders',base+'/reminders','POST',dict(invoiceIds=[invoice]),403)
unverified=admin.req(base+'/invoices/'+invoice)[1]['invoice'];request(president,'Unauthorized credit posting blocked','/finance/invoices/'+invoice+'/credits','POST',dict(amount=1,reason='Must not post',key='forbidden-'+tag),403)
batch=str(uuid.uuid4());row=dict(invoiceId=invoice,amount=5,date=datetime.date.today().isoformat(),method='Cash',reference='ADV-BULK-'+tag,accountHolder='',destination='',evidence='QA receipt reference',notes='Isolated QA batch',requestKey=str(uuid.uuid4()))
badrow=row|dict(amount=999999999,reference='ADV-BULK-BAD-'+tag,requestKey=str(uuid.uuid4()))
paidBefore=admin.req(base+'/invoices/'+invoice)[1]['invoice']['paid']
bulk=request(admin,'Bulk collection records valid rows and reports invalid rows',base+'/collections/bulk','POST',dict(batchId=batch,rows=[row,badrow]))
check('Bulk partial failure is explicit',bulk['submitted']==1 and bulk['failed']==1)
check('Unverified bulk collections never count as paid',admin.req(base+'/invoices/'+invoice)[1]['invoice']['paid']==paidBefore)
request(admin,'Bulk collection retry',base+'/collections/bulk','POST',dict(batchId=batch,rows=[row]))
check('Bulk retry creates one collection only',sql(f'''SELECT count(*) FROM "DuesCollections" WHERE "Reference"='ADV-BULK-{tag}' ''')=='1')
check('Bulk batch links to an audit record',sql(f'''SELECT count(*) FROM "FinanceAudits" WHERE "Action"='dues.bulk.collection' AND "Detail" LIKE '{batch}|%' ''')=='1')
foreignInvoice=sql(f'''SELECT i."Id" FROM "FinanceInvoices" i JOIN "Memberships" m ON m."Id"=i."MembershipId" WHERE m."ClubId"='{other}' AND i."State"='Issued' AND i."Total">i."Paid"+i."Credited" LIMIT 1''')
blocked=request(president,'Bulk cannot cross club scope',base+'/collections/bulk','POST',dict(batchId=str(uuid.uuid4()),rows=[row|dict(invoiceId=foreignInvoice,reference='CROSS-BULK-'+tag,requestKey=str(uuid.uuid4()))]))
check('Unauthorized bulk row fails without recording',blocked['failed']==1 and blocked['submitted']==0)
linked=sql(f'''SELECT i."Id" FROM "FinanceInvoices" i JOIN "Persons" p ON p."Id"=i."PersonId" WHERE i."PlanId"='{plan['id']}' AND p."UserId" IS NOT NULL LIMIT 1''')
check('Reminder fixture has an authenticated member',bool(linked))
sql(f'''UPDATE "FinanceInvoices" SET "RemindedAt"=NULL WHERE "Id"='{linked}' ''')
reminder=request(admin,'Queue reminder through existing notification infrastructure',base+'/reminders','POST',dict(invoiceIds=[linked],template='QA {member}: {invoice}, {balance}, {dueDate}'))
check('Reminder has one queued or concurrently suppressed outcome',reminder['queued']+reminder['skipped']==1)
repeated=request(admin,'Repeat reminder is suppressed',base+'/reminders','POST',dict(invoiceIds=[linked]))
check('Seven-day suppression prevents duplicate reminders',repeated['queued']==0 and repeated['skipped']==1)
check('Reminder does not change financial balance',admin.req(base+'/invoices/'+invoice)[1]['invoice']['paid']==paidBefore)
check('Financial journals remain balanced',close(sql('SELECT coalesce(sum("Debit"-"Credit"),0) FROM "FinanceJournalLines"'),0))
post=request(admin,'Post-adjustment intelligence',base+'/intelligence');check('All current balance identities reconcile',all(close(s['billed']-s['collected']-s['credits'],s['pending']) for s in post['summary']))
times=[]
for _ in range(5):
 begin=time.monotonic();assert admin.req(base+'/intelligence')[0]==200;times.append((time.monotonic()-begin)*1000)
check('Aggregate queries complete under two seconds at QA scale',max(times)<2000, times)
pathlib.Path('docs/qa/advanced-dues-performance.json').write_text(json.dumps({'milliseconds':times,'median':statistics.median(times),'scope':'Isolated functional QA snapshot; not a million-row benchmark'},indent=2))
print(str(len(results))+' checks passed')
