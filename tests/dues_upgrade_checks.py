"""Check the dedicated mock dues upgrade dataset. Changes leave financial totals intact."""
import datetime,json,os,pathlib,uuid,sys
from support import Client,BASE,sql
assert BASE=='http://127.0.0.1:5095' and os.environ['CLUBS_TEST_DATABASE'].startswith('clubs_impact_dues_upgrade_')
f=json.loads(pathlib.Path('docs/qa/dues-upgrade-mock.json').read_text())['fixtures']
base='/finance/dues-center'; today=datetime.date.today().isoformat()
p=Client();assert p.login('club.president@example.test')[0]==200
a=Client();assert a.login('admin@example.test')[0]==200
def good(c,path,method='GET',body=None):
 status,result=c.req(path,method,body);assert status in (200,201,204),(path,status,result);return result
summary=good(p,base+'/reports/pending')['summary'][0]
after_browser='--after-browser' in sys.argv
assert summary['billed']==12200 and summary['collected']==(1850 if after_browser else 1050) and summary['pending']==(10350 if after_browser else 11150),summary
assert summary['pendingVerification']==(900 if after_browser else 1500) and summary['availableToCollect']==(9450 if after_browser else 9650) and summary['overdue']==(8500 if after_browser else 9300),summary
assert good(p,base+'/reports/pending')['total']==11
assert good(p,base+'/reports/pending?age=90%2B')['total']==1
assert good(p,base+'/reports/pending?age=31-60')['total']==1
assert good(p,base+'/reports/pending?age=current')['total']==2
assert good(p,base+'/reports/pending?search=MOCK-DUE-00000001')['total']==1
d=good(p,base+'/invoices/'+f['personal-pending']);assert d['pendingVerification']==(0 if after_browser else 600) and d['availableToCollect']==1200
assert d['invoice']['outstanding']==(1200 if after_browser else 1800) and len(d['collections'])==1
paid=good(p,base+'/invoices/'+f['paid']);assert paid['invoice']['outstanding']==0 and not paid['canCollect'] and paid['payments'][0]['receiptNumber']
assert p.req(base+'/invoices/'+f['other-region-0'])[0]==404
assert p.req(base+'/invoices/'+f['other-region-0']+'/followups','POST',dict(outcome='Contacted',notes='not authorized',nextFollowUp=None,promisedOn=None,promisedAmount=None,requestKey=str(uuid.uuid4())))[0]==404
promise=good(p,base+'/reports/promises');assert any(r['invoiceId']==f['promise-today'] for r in promise['items'])
payload=dict(outcome='Contacted',notes='Mock API follow-up recorded; no message sent.',nextFollowUp=today,promisedOn=None,promisedAmount=None,requestKey=str(uuid.uuid4()))
followup=good(p,base+'/invoices/'+f['cancelled']+'/followups','POST',payload)
assert good(p,base+'/invoices/'+f['cancelled']+'/followups','POST',payload)['id']==followup['id']
assert p.req(base+'/invoices/'+f['cancelled']+'/followups','POST',payload|dict(notes='Changed note'))[0]==409
assert p.req(base+'/invoices/'+f['cancelled']+'/followups','POST',payload|dict(outcome='PromiseToPay',promisedOn=today,promisedAmount=10000,requestKey=str(uuid.uuid4())))[0]==409
# A cancelled receipt can be corrected and resubmitted; active duplicates remain blocked.
payment=dict(invoiceId=f['cancelled'],amount=150,date=today,method='Cash',reference='MOCK-CANCELLED',accountHolder='Mock collector',destination='Mock cash desk',evidence='Mock corrected receipt',notes='QA cancellation check',requestKey=str(uuid.uuid4()))
c=good(p,base+'/collections','POST',payment)
assert good(p,base+'/invoices/'+f['cancelled'])['availableToCollect']==550
assert p.req(base+'/collections','POST',payment|dict(requestKey=str(uuid.uuid4())))[0]==409
good(p,base+'/collections/'+c['id']+'/cancel','POST',dict(reason='QA completed; restore original available amount.'))
assert good(p,base+'/invoices/'+f['cancelled'])['availableToCollect']==700
assert p.req(base+'/collections/'+f['paid-collection']+'/cancel','POST',dict(reason='Cannot cancel verified money'))[0]==409
reports=good(a,base+'/options')['reports'];assert len(reports)==25
for report in reports:
 good(a,base+'/reports/'+report['id'])
 status,body,headers=a.request(base+'/reports/'+report['id']+'?format=csv');assert status==200
assert len(good(a,base+'/reports/region')['items'])==2
assert good(p,base+'/reports/unsettled')['total']==(4 if after_browser else 2)
after=good(p,base+'/reports/pending')['summary'][0]
assert after['pending']==summary['pending'] and after['availableToCollect']==summary['availableToCollect'] and after['pendingVerification']==summary['pendingVerification']
assert float(sql('SELECT coalesce(sum("Debit"-"Credit"),0) FROM "FinanceJournalLines"'))==0
result=dict(mockInvoices=18,reportTypes=len(reports),summary=after,ageFilters='passed',search='passed',invoiceHistory='passed',promiseTracking='passed',followupIdempotency='passed',cancelAndResubmit='passed',verifiedCancellation='blocked',crossClubAccess='blocked',allReportCsvExports='passed',balancedBooks='passed')
pathlib.Path('docs/qa/dues-upgrade-after-browser.json' if after_browser else 'docs/qa/dues-upgrade-results.json').write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2))
