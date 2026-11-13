"""Separate member receipts, combined club bank deposit; real ledger, isolated QA only."""
from support import Client,sql,BASE,PSQL
import os,json,pathlib,uuid,datetime,subprocess
assert BASE=='http://127.0.0.1:5098' and os.environ['CLUBS_TEST_DATABASE'].startswith('clubs_impact_dues_center_qa_')
results=[];tag=uuid.uuid4().hex[:8];base='/finance/dues-center';today=datetime.datetime.now(datetime.timezone.utc).date().isoformat()
def check(name,ok,detail=None):
 results.append({'test':name,'passed':bool(ok)});pathlib.Path('docs/qa/dues-club-bank-results.json').write_text(json.dumps(results,indent=2));print(('PASS ' if ok else 'FAIL ')+name,flush=True);assert ok,(name,detail)
def req(c,name,path,method='GET',data=None,status=200):
 code,b=c.req(path,method,data);check(name,code==status,(code,b));return b
a=Client();assert a.login('admin@example.test')[0]==200
p=Client();assert p.login('club.president@example.test')[0]==200
fixture=json.loads(pathlib.Path('.infra/dues-legacy-center.log').read_text())['fixture'];entity=fixture['entity'];invoices=fixture['invoices'];club=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'DEMO-R01-D01-Z01-C01\'');other=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'DEMO-R02-D01-Z01-C01\'')
payments=[]
for idx,invoice in enumerate([invoices[0],invoices[2]]):
 row=req(p,'Separate member collection '+str(idx),base+'/collections','POST',dict(invoiceId=invoice,amount=20+idx*10,date=today,method='Cash',reference='BANK-CLUB-'+tag+'-'+str(idx),accountHolder='',destination='',evidence='QA member receipt',notes='Isolated bank reconciliation test',requestKey=str(uuid.uuid4())))
 row=req(a,'Independent verification '+str(idx),base+'/collections/'+row['id']+'/review','POST',dict(approve=True,note='QA receipt verified independently'));payments.append(row['paymentId'])
check('Payments retain different member payers',sql(f'''SELECT count(DISTINCT "PersonId") FROM "FinancePayments" WHERE "Id" IN('{payments[0]}','{payments[1]}')''')=='2')
check('Separate receipt numbers preserved',sql(f'''SELECT count(DISTINCT "ReceiptNumber") FROM "FinancePayments" WHERE "Id" IN('{payments[0]}','{payments[1]}')''')=='2')
bank=req(a,'Configure QA receiving bank','/finance/entities/'+entity+'/bank-accounts','POST',dict(name='QA Club Remittance '+tag,accountNumber='0000111122223333',routingDetails='Fictional QA bank; no real funds',accountCode='1010'))
req(a,'Import real-shaped bank statement rows','/finance/entities/'+entity+'/reconciliation/import','POST',dict(bankAccountId=bank['id'],rows=[dict(date=today,amount=value,reference='CLUB-DEPOSIT-'+tag+'-'+str(value),key='bank-remit-'+tag+'-'+str(value)) for value in [47,48,19]]))
statements=a.req('/finance/entities/'+entity+'/reconciliation')[1];rows=[r for r in statements if r['reference'].startswith('CLUB-DEPOSIT-'+tag)]
match=next(r for r in rows if r['amount']==47);wrong=next(r for r in rows if r['amount']==48);single=next(r for r in rows if r['amount']==19)
body=dict(paymentId=str(uuid.UUID(int=0)),paymentIds=payments,clubId=club)
req(a,'Unsettled receipts cannot match a club deposit','/finance/reconciliation/'+match['id']+'/match','POST',body,409)
req(a,'Duplicate receipt IDs rejected','/finance/reconciliation/'+match['id']+'/match','POST',body|dict(paymentIds=[payments[0],payments[0]]),400)
req(p,'Collector cannot bypass bank reconciliation authority','/finance/reconciliation/'+match['id']+'/match','POST',body,403)
for idx,payment in enumerate(payments):req(a,'Post separate member settlement '+str(idx),'/finance/payments/'+payment+'/settle','POST',dict(bankAccountId=bank['id'],date=today,fee=idx+1,reference=match['reference']))
candidates=req(a,'Club bank-remittance candidates',base+'/bank-remittances?entityId='+entity+'&clubId='+club)
check('Both separate receipts appear as candidates',all(any(p['id']==id for p in candidates['payments']) for id in payments))
check('Protected bank account is never exposed','0000111122223333' not in json.dumps(candidates))
check('Posted member bank amounts reconcile after fees',sum(p['postedBankAmount'] for p in candidates['payments'] if p['id'] in payments)==47)
req(a,'Wrong combined bank amount rejected','/finance/reconciliation/'+wrong['id']+'/match','POST',body,409)
req(a,'Receipts cannot be assigned to a different club','/finance/reconciliation/'+match['id']+'/match','POST',body|dict(clubId=other),409)
countPayments=sql('SELECT count(*) FROM "FinancePayments"');countJournals=sql('SELECT count(*) FROM "FinanceJournals"')
balances=sql('SELECT md5(string_agg("Id"::text||"Total"::text||"Paid"::text||"Credited"::text,\'\' ORDER BY "Id")) FROM "FinanceInvoices"')
matched=req(a,'Match one combined club deposit','/finance/reconciliation/'+match['id']+'/match','POST',body)
check('One bank row retains both member receipt links',matched['status']=='Matched' and sorted(matched['matchedPaymentIds'])==sorted(payments) and matched['remittanceClubId']==club and matched['paymentId'] is None)
check('Matching creates no duplicate payment',countPayments==sql('SELECT count(*) FROM "FinancePayments"'))
check('Matching creates no additional journal',countJournals==sql('SELECT count(*) FROM "FinanceJournals"'))
check('Member dues balances are unchanged by bank grouping',balances==sql('SELECT md5(string_agg("Id"::text||"Total"::text||"Paid"::text||"Credited"::text,\'\' ORDER BY "Id")) FROM "FinanceInvoices"'))
check('Database stores separate immutable match links',sql(f'''SELECT count(*) FROM "FinanceBankPaymentMatches" WHERE "BankTransactionId"='{match['id']}' ''')=='2')
again=req(a,'Reordered remittance retry is idempotent','/finance/reconciliation/'+match['id']+'/match','POST',body|dict(paymentIds=payments[::-1]));check('Retry reuses original group',again['id']==match['id'])
req(a,'Grouped receipt cannot be reused as a single bank match','/finance/reconciliation/'+single['id']+'/match','POST',dict(paymentId=payments[0]),409)
req(a,'Receipts cannot be grouped into a second deposit','/finance/reconciliation/'+wrong['id']+'/match','POST',body,409)
updated=req(a,'Matched club remittance history',base+'/bank-remittances?entityId='+entity+'&clubId='+club)
check('Matched receipts leave the candidate queue',not any(p['id'] in payments for p in updated['payments']))
check('Club remittance history links member receipts',any(r['id']==match['id'] and len(r['matchedPaymentIds'])==2 for r in updated['matched']))
duplicate=f'''BEGIN; INSERT INTO "FinanceBankPaymentMatches" ("Id","BankTransactionId","PaymentId","ClubId","BankAmount","TenantId","OrgId","EntityId","Version") SELECT gen_random_uuid(),'{wrong['id']}',"PaymentId","ClubId","BankAmount","TenantId","OrgId","EntityId",1 FROM "FinanceBankPaymentMatches" WHERE "PaymentId"='{payments[0]}'; ROLLBACK;'''
raw=subprocess.run([PSQL,'-h','127.0.0.1','-p','55432','-U','clubs','-d',os.environ['CLUBS_TEST_DATABASE'],'-v','ON_ERROR_STOP=1','-c',duplicate],capture_output=True,text=True)
check('Unique database constraint blocks duplicate payment-bank links',raw.returncode!=0 and 'unique' in raw.stderr.lower())
check('Financial journals remain balanced',float(sql('SELECT coalesce(sum("Debit"-"Credit"),0) FROM "FinanceJournalLines"'))==0)
pathlib.Path('docs/qa/dues-club-bank-fixture.json').write_text(json.dumps(dict(entity=entity,club=club,bank=bank['id'],statement=match['id'],payments=payments),indent=2))
print(str(len(results))+' club bank checks passed')
