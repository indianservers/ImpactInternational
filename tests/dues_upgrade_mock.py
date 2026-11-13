"""Seed realistic, repeatable mock dues only in a dedicated isolated upgrade DB."""
import datetime, json, os, pathlib, uuid
from support import Client, sql, BASE
assert BASE=='http://127.0.0.1:5095'
assert os.environ['CLUBS_TEST_DATABASE'].startswith('clubs_impact_dues_upgrade_')
date=datetime.date.today(); year=date.year
a=Client(); assert a.login('admin@example.test')[0]==200
p=Client(); assert p.login('club.president@example.test')[0]==200
def good(c,path,method='GET',body=None):
 status,result=c.req(path,method,body); assert status in (200,201,204),(path,status,result); return result
existing=sql('SELECT count(*) FROM "FinanceEntitys" WHERE "Name" LIKE \'Mock Impact Dues%\'')
assert existing=='0','Mock dataset already exists. Use tests/dues_upgrade_checks.py to retest it.'
base='/finance/dues-center'
fixtures={}; entities=[]
def create_entity(code):
 club=sql(f'''SELECT "Id" FROM "Orgs" WHERE "Code"='{code}' ''')
 e=good(a,'/finance/entities','POST',dict(orgId=club,name='Mock Impact Dues · '+code,country='India',currency='INR',invoicePrefix='MOCK-DUE',receiptPrefix='MOCK-REC',collectionAuthority='Mock testing only. No real money.'))
 good(a,'/finance/entities/'+e['id']+'/periods','POST',dict(name='Mock fiscal year '+str(year),startsOn=f'{year}-01-01',endsOn=f'{year}-12-31'))
 members=json.loads(sql(f'''SELECT json_agg(x) FROM (SELECT "Id" AS id,"PersonId" AS person FROM "Memberships" WHERE "ClubId"='{club}' AND "Status"='Active' ORDER BY "Id" LIMIT 5) x'''))
 entities.append(e['id']); return e,club,members
def invoice(e,member,key,amount,overdue):
 invoice_date=date-datetime.timedelta(days=max(125,overdue+5)); due=date-datetime.timedelta(days=overdue)
 i=good(a,'/finance/entities/'+e['id']+'/invoices','POST',dict(personId=member['person'],membershipId=member['id'],customer=sql(f'''SELECT "DisplayName" FROM "Persons" WHERE "Id"='{member['person']}' '''),purpose='ClubDues',date=invoice_date.isoformat(),dueOn=due.isoformat(),key='mock-'+key,notes='Mock scenario: '+key,lines=[dict(description='Mock annual dues · '+key,quantity=1,rate=amount,accountCode='4000')]))
 good(a,'/finance/invoices/'+i['id']+'/issue','POST',{})
 fixtures[key]=i['id']; return i
def collection(i,key,amount,method='Cash',status='Submitted'):
 c=good(p,base+'/collections','POST',dict(invoiceId=i['id'],amount=amount,date=date.isoformat(),method=method,reference='MOCK-'+key.upper(),accountHolder='Demo Collection Officer' if method=='PersonalAccount' else 'Demo Cash Collector',destination='demo.collector@upi' if method=='PersonalAccount' else 'Mock club cash desk',evidence='Mock receipt / transaction proof',notes='Fictional payment for testing. No real funds.',requestKey=str(uuid.uuid4())))
 fixtures[key+'-collection']=c['id']
 if status in ('Approved','Rejected'):good(a,base+'/collections/'+c['id']+'/review','POST',dict(approve=status=='Approved',note='Mock independent review: '+status))
 if status=='Cancelled':good(p,base+'/collections/'+c['id']+'/cancel','POST',dict(reason='Mock mistaken entry cancelled before verification'))
 return c
e,club,members=create_entity('DEMO-R01-D01-Z01-C01')
cases=[('overdue-95',1200,95),('overdue-45',1500,45),('partial-cash',1000,20),('personal-pending',1800,10),('cash-pending',900,3),('paid',750,15),('due-today',650,0),('upcoming',1200,-7),('promise-today',900,12),('disputed',1100,65),('rejected',500,5),('cancelled',700,2)]
for n,(name,amount,age) in enumerate(cases):
 i=invoice(e,members[n%len(members)],name,amount,age)
 if name=='partial-cash':collection(i,name,300,status='Approved')
 if name=='personal-pending':collection(i,name,600,'PersonalAccount')
 if name=='cash-pending':collection(i,name,900)
 if name=='paid':collection(i,name,750,status='Approved')
 if name=='rejected':collection(i,name,100,status='Rejected')
 if name=='cancelled':collection(i,name,200,status='Cancelled')
 if name in ('promise-today','disputed'):
  good(p,base+'/invoices/'+i['id']+'/followups','POST',dict(outcome='PromiseToPay' if name=='promise-today' else 'Disputed',notes='Mock member promised a partial payment today.' if name=='promise-today' else 'Mock member requested clarification of the dues amount.',nextFollowUp=date.isoformat(),promisedOn=date.isoformat() if name=='promise-today' else None,promisedAmount=300 if name=='promise-today' else None,requestKey=str(uuid.uuid4())))
for region,code in [('same-region','DEMO-R01-D02-Z01-C01'),('other-region','DEMO-R02-D01-Z01-C01')]:
 e,club,members=create_entity(code)
 for n in range(3):invoice(e,members[n],region+'-'+str(n),1000+n*250,15+n*20)
result=dict(database=os.environ['CLUBS_TEST_DATABASE'],entities=entities,fixtures=fixtures,invoices=len(cases)+6,collections=6,followups=2,mock=True)
pathlib.Path('docs/qa/dues-upgrade-mock.json').write_text(json.dumps(result,indent=2))
print(json.dumps(result,indent=2))
