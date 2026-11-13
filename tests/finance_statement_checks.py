import base64,datetime,io,json,os,pathlib,sys,zipfile
from support import Client,BASE
assert BASE=='http://127.0.0.1:5199' and os.environ['CLUBS_TEST_DATABASE'].startswith('clubs_finance_command_')
r=[]
def check(name,ok):r.append(dict(test=name,passed=bool(ok)));assert ok,name
c=Client();assert c.login('admin@example.test')[0]==200
e=c.req('/finance/entities')[1][0];url='/finance/entities/'+e['id']+'/reconciliation/parse'
date=datetime.date.today().isoformat()
def parse(text,name='statement.csv'):
 return c.req(url,'POST',dict(name=name,content=base64.b64encode(text.encode() if isinstance(text,str) else text).decode()))
try:
 s,data=parse('Date,Amount,Reference\n'+date+',100.00,BANK-01');check('CSV parsed',s==200 and len(data['rows'])==1)
 s,d2=parse('Date,Amount,Reference\n'+date+',100.0,BANK-01');check('Keys stable across decimal formatting',s==200 and data['rows'][0]['key']==d2['rows'][0]['key'])
 s,data=parse('Date,Amount,Reference,Key\r\n'+date+',-10.50,"Bank, fee",bank-key');check('Quoted CSV and negative amounts',s==200 and data['rows'][0]['reference']=='Bank, fee' and data['rows'][0]['amount']==-10.5)
 for label,text in [('bad date','Date,Amount,Reference\nwrong,5,REF'),('future date','Date,Amount,Reference\n2999-01-01,5,REF'),('zero amount','Date,Amount,Reference\n'+date+',0,REF'),('bad amount','Date,Amount,Reference\n'+date+',abc,REF'),('missing header','wrong,amount,reference\n'+date+',1,REF'),('missing reference','Date,Amount,Reference\n'+date+',1,'),('empty statement','Date,Amount,Reference'),('unclosed quote','Date,Amount,Reference\n'+date+',1,"REF'),('duplicate key','Date,Amount,Reference,Key\n'+date+',1,R1,K\n'+date+',2,R2,K')]:
  check(label+' rejected',parse(text)[0]==400)
 check('Unsupported file rejected',parse('text','statement.exe')[0]==400)
 check('Malformed XLSX rejected',parse(b'not a zip','bad.xlsx')[0]==400)
 output=io.BytesIO()
 with zipfile.ZipFile(output,'w') as z:
  z.writestr('xl/worksheets/sheet1.xml','<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData><row><c r="A1" t="inlineStr"><is><t>Date</t></is></c><c r="B1" t="inlineStr"><is><t>Amount</t></is></c><c r="C1" t="inlineStr"><is><t>Reference</t></is></c></row><row><c r="A2" t="inlineStr"><is><t>'+date+'</t></is></c><c r="B2"><v>123.45</v></c><c r="C2" t="inlineStr"><is><t>BANK-XLSX</t></is></c></row></sheetData></worksheet>')
 s,data=parse(output.getvalue(),'valid.xlsx');check('XLSX values parsed',s==200 and data['rows'][0]['amount']==123.45)
 check('Large file rejected',parse(b'x'*2000001)[0] in (400,413))
 anon=Client();check('Anonymous parser denied',anon.req(url,'POST',dict(name='x.csv',content=''))[0] in (401,403))
 member=Client();assert member.login('member.club1@example.test')[0]==200
 check('Ordinary member parser denied',member.req(url,'POST',dict(name='x.csv',content=''))[0] in (401,403,404))
except Exception as exc:r.append(dict(test=str(exc),passed=False))
finally:
 pathlib.Path('docs/qa/finance-statement-results.json').write_text(json.dumps(r,indent=2));print(json.dumps(dict(total=len(r),passed=sum(x['passed'] for x in r),failed=sum(not x['passed'] for x in r))))
 if any(not x['passed'] for x in r):sys.exit(1)
