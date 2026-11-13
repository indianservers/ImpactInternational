from playwright.sync_api import sync_playwright
from support import Client,BASE
import csv,datetime,io,json,os,pathlib,uuid,zipfile,xml.etree.ElementTree as ET
assert BASE=='http://127.0.0.1:5199' and os.environ['CLUBS_TEST_DATABASE'].startswith('clubs_finance_command_')
c=Client();assert c.login('admin@example.test')[0]==200;e=c.req('/finance/entities')[1][0];results=[];name='QA invoice import '+uuid.uuid4().hex;key=uuid.uuid4().hex;date=datetime.date.today().isoformat()
def check(name,ok):results.append(dict(test=name,passed=bool(ok)));assert ok,name
try:
 with sync_playwright() as p:
  b=p.chromium.launch(headless=True);ctx=b.new_context(viewport={'width':1672,'height':1050});ctx.add_cookies([{'name':x.name,'value':x.value,'domain':'127.0.0.1','path':x.path,'httpOnly':True,'secure':False,'sameSite':'Lax'} for x in c.jar]);page=ctx.new_page();page.goto('http://127.0.0.1:1249/app/finance/invoices');page.get_by_role('button',name='Import invoices',exact=True).click();page.get_by_label('Invoice import file').set_input_files(dict(name='qa-invoices.csv',mimeType='text/csv',buffer=('key,customer,date,dueOn,description,rate\n'+key+','+name+','+date+','+date+',QA draft import,12.34').encode()));page.get_by_text('1 invoices ready for review',exact=True).wait_for();check('CSV invoice preview',True)
  page.get_by_role('button',name='Confirm draft import',exact=True).click();page.get_by_role('status').filter(has_text='1 drafts created or safely reused').wait_for();check('Draft import completed',True)
  invoices=c.req('/finance/entities/'+e['id']+'/invoices?search='+name.replace(' ','%20'))[1];check('Import creates one draft',len(invoices)==1 and invoices[0]['state']=='Draft')
  page.get_by_role('button',name='Confirm draft import',exact=True).click();page.get_by_role('status').filter(has_text='1 drafts created or safely reused').wait_for();invoices=c.req('/finance/entities/'+e['id']+'/invoices?search='+name.replace(' ','%20'))[1];check('Repeated import does not duplicate invoices',len(invoices)==1)
  page.goto('http://127.0.0.1:1249/app/finance/reports/custom');page.get_by_role('heading',name='Build a custom report',exact=True).wait_for();page.get_by_label('Record source',exact=True).select_option('invoices');page.get_by_label('party',exact=True).check();page.get_by_label('amount',exact=True).check();page.get_by_label('View name',exact=True).fill('QA selected columns');page.get_by_role('button',name='Save view for this session',exact=True).click();check('Custom report view saved',page.get_by_role('button',name='QA selected columns',exact=True).count()==1)
  page.locator('.fc-export summary').click()
  with page.expect_download() as dl:page.get_by_role('link',name='Download CSV',exact=True).click()
  download=dl.value;data=pathlib.Path(download.path()).read_text(encoding='utf-8-sig');check('CSV contains selected columns',next(csv.reader(io.StringIO(data)))==['Party','Amount'])
  page.locator('.fc-export summary').click();page.locator('.fc-export summary').click()
  with page.expect_download() as dl:page.get_by_role('link',name='Download XLSX',exact=True).click()
  with zipfile.ZipFile(dl.value.path()) as z:
   root=ET.fromstring(z.read('xl/worksheets/sheet1.xml'));ns={'s':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'};check('Excel export has numeric amounts',any(cell.find('s:v',ns) is not None for cell in root.findall('.//s:c',ns)));check('Excel export has no formulas',not root.findall('.//s:f',ns))
  b.close()
except Exception as exc:results.append(dict(test=str(exc),passed=False))
finally:
 pathlib.Path('docs/qa/finance-command-import-browser-results.json').write_text(json.dumps(results,indent=2));print(json.dumps(dict(passed=sum(r['passed'] for r in results),failed=sum(not r['passed'] for r in results),total=len(results))))
 assert all(r['passed'] for r in results)
