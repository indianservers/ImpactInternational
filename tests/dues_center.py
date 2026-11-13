"""Dues reporting and club collections against isolated QA only; no real funds."""
import datetime, json, os, uuid
from support import Client, sql, BASE
assert BASE == 'http://127.0.0.1:5095' and os.environ['CLUBS_TEST_DATABASE'].startswith('clubs_impact_comms_test_')
run=uuid.uuid4().hex[:10]; date=datetime.date.today(); year=date.year
a=Client(); assert a.login('admin@example.test')[0]==200
p=Client(); assert p.login('club.president@example.test')[0]==200
def good(c,path,method='GET',body=None):
 status,result=c.req(path,method,body); assert status in (200,201,204),(path,status,result); return result
club1=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'DEMO-R01-D01-Z01-C01\'')
club2=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'DEMO-R02-D01-Z01-C01\'')
members=json.loads(sql(f'''SELECT json_agg(t) FROM (SELECT "Id" AS id,"PersonId" AS person FROM "Memberships" WHERE "ClubId"='{club1}' AND "Status"='Active' ORDER BY "Id" LIMIT 3) t'''))
def entity(club,currency):
 e=good(a,'/finance/entities','POST',dict(orgId=club,name='QA Dues '+run+' '+currency,country='India',currency=currency,collectionAuthority='QA fictional member dues only'))
 good(a,'/finance/entities/'+e['id']+'/periods','POST',dict(name='QA '+str(year),startsOn=f'{year}-01-01',endsOn=f'{year}-12-31'))
 return e
def assess(e,club,member,amount=1000):
 plan=good(a,'/finance/entities/'+e['id']+'/dues-plans','POST',dict(name='QA annual '+run,billingOrgId=club,purpose='ClubDues',effectiveFrom=f'{year}-01-01',effectiveTo=f'{year}-12-31',months=12,graceDays=0,components=[dict(description='QA annual dues',amount=amount)],chargeCode=run+'-'+str(uuid.uuid4())))
 return good(a,'/finance/dues-plans/'+plan['id']+'/assess','POST',dict(membershipId=member,startsOn=f'{year}-01-01',endsOn=f'{year+1}-01-01'))['invoice']
e=entity(club1,'INR'); invoices=[assess(e,club1,m['id']) for m in members]
e2=entity(club2,'INR'); otherMember=sql(f'''SELECT "Id" FROM "Memberships" WHERE "ClubId"='{club2}' AND "Status"='Active' ORDER BY "Id" LIMIT 1'''); other=assess(e2,club2,otherMember)
usd=entity(club1,'USD'); usdInvoice=assess(usd,club1,members[0]['id'],100)
def payload(invoice,method='Cash',amount=250):return dict(invoiceId=invoice['id'],amount=amount,date=date.isoformat(),method=method,reference='QA-'+run+'-'+uuid.uuid4().hex[:8],accountHolder='Demo collector' if method=='PersonalAccount' else '',destination='demo@upi' if method=='PersonalAccount' else '',evidence='QA fictional receipt',notes='No real funds moved',requestKey=str(uuid.uuid4()))
base='/finance/dues-center'
cash=payload(invoices[0]); c=good(p,base+'/collections','POST',cash)
assert c['status']=='Submitted' and good(p,base+'/collections','POST',cash)['id']==c['id']
assert p.req(base+'/collections','POST',{**cash,'amount':251})[0]==409
assert p.req(base+'/collections','POST',payload(invoices[0],amount=800))[0]==409
assert p.req(base+'/collections','POST',payload(other))[0]==404
assert p.req(base+'/collections','POST',payload(invoices[1],'PersonalAccount')|dict(accountHolder='',destination=''))[0]==400
assert p.req(base+'/collections/'+c['id']+'/review','POST',dict(approve=True,note='Self verify'))[0]==403
assert float(sql(f'''SELECT "Paid" FROM "FinanceInvoices" WHERE "Id"='{invoices[0]['id']}' '''))==0
approved=good(a,base+'/collections/'+c['id']+'/review','POST',dict(approve=True,note='QA independent receipt checked'))
assert approved['status']=='Approved' and approved['paymentId']
assert float(sql(f'''SELECT "Paid" FROM "FinanceInvoices" WHERE "Id"='{invoices[0]['id']}' '''))==250
assert a.req(base+'/collections/'+c['id']+'/review','POST',dict(approve=True,note='Duplicate'))[0]==409
personal=good(p,base+'/collections','POST',payload(invoices[1],'PersonalAccount',1000))
good(a,base+'/collections/'+personal['id']+'/review','POST',dict(approve=True,note='QA personal transfer checked; settlement pending'))
assert sql(f'''SELECT "SettlementStatus" FROM "FinancePayments" WHERE "Id"=(SELECT "PaymentId" FROM "DuesCollections" WHERE "Id"='{personal['id']}')''')=='Unsettled'
rejected=good(p,base+'/collections','POST',payload(invoices[2],amount=100))
good(a,base+'/collections/'+rejected['id']+'/review','POST',dict(approve=False,note='QA invalid receipt rejected'))
pending=good(p,base+'/collections','POST',payload(invoices[2],amount=200))
selfClaim=good(a,base+'/collections','POST',payload(invoices[2],amount=100))
assert a.req(base+'/collections/'+selfClaim['id']+'/review','POST',dict(approve=True,note='Self approval'))[0]==403
overdue=good(a,'/finance/entities/'+e['id']+'/invoices','POST',dict(personId=members[0]['person'],membershipId=members[0]['id'],customer='QA overdue member',purpose='ClubDues',date=(date-datetime.timedelta(days=10)).isoformat(),dueOn=(date-datetime.timedelta(days=5)).isoformat(),key='qa-overdue-'+run,lines=[dict(description='QA overdue dues',quantity=1,rate=100,accountCode='4000')]))
good(a,'/finance/invoices/'+overdue['id']+'/issue','POST',{})
options=good(p,base+'/options'); assert len(options['reports'])>=21 and options['canCollect']
assert len(options['organizations'])==1
results={}
for r in options['reports']:
 report=good(p,base+'/reports/'+r['id']); results[r['id']]=report['total']
 assert all('Region 02' not in json.dumps(row) for row in report['items'])
 status,body,headers=p.request(base+'/reports/'+r['id']+'?format=csv'); assert status==200 and 'text/csv' in headers.get('Content-Type','')
summary=good(p,base+'/reports/overview')['summary']; totals={s['currency']:s for s in summary}
assert totals['INR']['billed']>=3000 and totals['INR']['collected']>=1250 and 'USD' in totals
memberReport=good(p,base+'/reports/member')['items']; first=next(r for r in memberReport if r['invoiceId']==invoices[0]['id'])
assert first['pending']==750 and first['collected']==250 and first['status']=='Partially paid'
assert p.req(base+'/reports/overview?orgId='+club2)[0]==404
assert p.req(base+'/reports/overview?from=2026-12-31&to=2026-01-01')[0]==400
assert p.req('/finance/entities/'+e2['id']+'/dashboard')[0]==403
for role in ['zonal','district','regional','national','international']:
 leader=Client(); assert leader.login(role+'.president@example.test')[0]==200
 assert leader.req(base+'/reports/overview')[0]==200
 assert leader.req(base+'/collections','POST',payload(invoices[0]))[0]==403
assert sql('SELECT count(*) FROM "RolePermissions" g JOIN "Roles" r ON r."Id"=g."RoleId" WHERE r."Name"=\'Club Member\' AND g."PermissionId" LIKE \'finance.dues.%\'')=='0'
assert sum(float(x) for x in sql(f'''SELECT coalesce(sum(l."Debit"-l."Credit"),0) FROM "FinanceJournalLines" l WHERE l."EntityId"='{e['id']}' ''').splitlines())==0
print(json.dumps(dict(reports=results,recordVerifyAllocate='passed',personalAccountUnsettled='passed',scope='passed',overpayment='blocked',selfApproval='blocked',retries='passed',multiCurrency='separate',balancedBooks='passed',fixture=dict(entity=e['id'],invoices=[i['id'] for i in invoices],pending=pending['id'])),indent=2))
