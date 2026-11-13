import datetime,json,os,pathlib,sys,uuid
from support import Client,sql,BASE
assert BASE=='http://127.0.0.1:5200' and os.environ['CLUBS_TEST_DATABASE'].startswith('clubs_finance_workflows_')
r=[]
def check(name,ok):r.append(dict(test=name,passed=bool(ok)));assert ok,name
def good(c,url,method='GET',body=None):
 status,data=c.req(url,method,body);assert status in (200,201,204),(url,status,data);return data
f=json.load(open('test-results/phase7-fixture.json'));p=f['path'];eid=f['entity']['id'];date=datetime.date.today().isoformat();tag=uuid.uuid4().hex
try:
 a=Client();assert a.login('admin@example.test')[0]==200;w=next(w for w in good(a,'/workspaces') if w['role']=='Association Super Admin' and w['association'].startswith('Global'));a.activate(w)
 v=Client();assert v.login('secretary@example.test')[0]==200;vw=next(x for x in good(v,'/workspaces') if x['role']=='Association Super Admin' and x['orgId']==w['orgId']);v.activate(vw)
 before=good(a,p+'/command-metrics')
 expense=next(x for x in good(a,p+'/expenses') if x['status']=='Approved' and x['amount']>x['paid'])
 posted=sql(f'SELECT count(*) FROM "FinanceJournals" WHERE "EntityId"=\'{eid}\'')
 scheduled=good(a,'/finance/expenses/'+expense['id']+'/schedule','POST',dict(date=date,reason='Isolated QA schedule; no real payment'))
 check('Schedule does not post cash',sql(f'SELECT count(*) FROM "FinanceJournals" WHERE "EntityId"=\'{eid}\'')==posted)
 schedules=good(a,p+'/command/scheduled');check('Scheduled expense visible',any(x['recordId']==expense['id'] for x in schedules['items']))
 check('Scheduled payable reconciles',good(a,p+'/command-metrics')['scheduled']>=expense['amount']-expense['paid'])
 good(a,'/finance/expenses/'+expense['id']+'/schedule','POST',dict(date=None,reason='QA schedule cancellation'))
 check('Cancellation removes planned payment',not any(x['recordId']==expense['id'] for x in good(a,p+'/command/scheduled')['items']))
 draft=next((x for x in good(a,p+'/expenses') if x['status']!='Approved'),None)
 check('Unknown expense schedule blocked',a.req('/finance/expenses/'+str(uuid.uuid4())+'/schedule','POST',dict(date=date,reason='QA invalid'))[0]==404) if draft is None else check('Unapproved schedule blocked',a.req('/finance/expenses/'+draft['id']+'/schedule','POST',dict(date=date,reason='QA invalid'))[0]==409)
 check('Past schedule rejected',a.req('/finance/expenses/'+expense['id']+'/schedule','POST',dict(date='2000-01-01',reason='QA invalid date'))[0]==400)
 payment=good(a,p+'/payments','POST',dict(personId=f['personId'],payer='Fictional matching QA',reference='MATCH-'+tag,method='BankTransfer',currency=f['entity']['currency'],amount=40,date=date,key=tag,evidence='QA only'))
 good(v,'/finance/payments/'+payment['id']+'/verify','POST',{})
 good(v,'/finance/payments/'+payment['id']+'/refunds','POST',dict(invoiceId=None,amount=5,kind='Refund',reference='MATCH-REF-'+tag,reason='QA completed refund before settlement'))
 good(v,'/finance/payments/'+payment['id']+'/settle','POST',dict(bankAccountId=f['bankId'],date=date,fee=1,reference='MATCH-SETTLE-'+tag))
 row=dict(date=date,amount=34,reference='STMT-'+tag,key='KEY-'+tag)
 check('Statement imported',good(a,p+'/reconciliation/import','POST',dict(bankAccountId=f['bankId'],rows=[row]))['imported']==1)
 statement=next(x for x in good(a,p+'/command/reconciliation?pageSize=100')['items'] if x['reference']==row['reference'])
 suggestions=good(a,p+'/reconciliation/'+statement['id']+'/suggestions')
 check('Recommendation uses posted net after refund and fee',any(x['id']==payment['id'] and x['netAmount']==34 for x in suggestions['candidates']))
 history=good(a,p+'/collection-history/'+payment['id']);check('History traces actual refund',sum(x['amount'] for x in history['refunds'])==5)
 posted=sql(f'SELECT count(*) FROM "FinanceJournals" WHERE "EntityId"=\'{eid}\'')
 good(a,'/finance/reconciliation/'+statement['id']+'/match','POST',dict(paymentId=payment['id']))
 check('Confirmed match posts no duplicate cash',sql(f'SELECT count(*) FROM "FinanceJournals" WHERE "EntityId"=\'{eid}\'')==posted)
 check('Matched payment excluded from recommendations',not good(a,p+'/reconciliation/'+statement['id']+'/suggestions')['candidates'])
 check('Duplicate import idempotent',good(a,p+'/reconciliation/import','POST',dict(bankAccountId=f['bankId'],rows=[row]))['imported']==0)
 check('Ledger remains balanced',good(a,p+'/reports/trial-balance')['trialDifference']==0)
except Exception as exc:r.append(dict(test=str(exc),passed=False))
finally:
 pathlib.Path('docs/qa/finance-command-matching-results.json').write_text(json.dumps(r,indent=2));print(json.dumps(dict(passed=sum(x['passed'] for x in r),failed=sum(not x['passed'] for x in r),total=len(r))))
 if any(not x['passed'] for x in r):sys.exit(1)
