from playwright.sync_api import sync_playwright
import json,pathlib
from support import Client
c=Client();assert c.login('admin@example.test')[0]==200
results=[];errors=[]
def check(name,ok):results.append(dict(test=name,passed=bool(ok)))
routes=[('', 'Finance Overview'),('transactions','Transactions'),('receipts','Receipts & Collections'),('invoices','Invoices & Billing'),('payments','Payments'),('expenses','Expenses'),('reconciliation','Bank Reconciliation'),('budgets','Budgets & Allocation'),('reports','Financial Reports & Analytics'),('audit','Audit Trail')]
with sync_playwright() as p:
 browser=p.chromium.launch(headless=True);ctx=browser.new_context(viewport={'width':1672,'height':1050},device_scale_factor=1)
 ctx.add_cookies([{'name':x.name,'value':x.value,'domain':'127.0.0.1','path':x.path,'httpOnly':True,'secure':False,'sameSite':'Lax'} for x in c.jar]);page=ctx.new_page();page.on('pageerror',lambda e:errors.append(str(e)))
 for route,title in routes:
  page.goto('http://127.0.0.1:1249/app/finance'+('/'+route if route else ''));page.get_by_role('heading',name=title,exact=True).wait_for();page.wait_for_timeout(500)
  check(title+' desktop renders',page.locator('.fc-page').count()==1);check(title+' desktop no overflow',page.evaluate('document.documentElement.scrollWidth<=innerWidth'))
  check(title+' no API error',page.locator('.fc-page [role="alert"]').count()==0)
  page.screenshot(path='docs/qa/finance-command-'+(route or 'overview')+'-desktop.png',full_page=True)
  page.get_by_role('textbox',name='Search financial records').fill('no-such-finance-reference');page.wait_for_timeout(450);check(title+' search filters records',page.locator('.fc-empty').count()==1)
  page.get_by_role('button',name='Clear all',exact=True).click();page.wait_for_timeout(400)
  page.set_viewport_size({'width':390,'height':844});page.wait_for_timeout(150);check(title+' mobile no overflow',page.evaluate('document.documentElement.scrollWidth<=innerWidth'));page.screenshot(path='docs/qa/finance-command-'+(route or 'overview')+'-mobile.png',full_page=True);page.set_viewport_size({'width':1672,'height':1050})
 page.goto('http://127.0.0.1:1249/app/finance/receipts');page.locator('.fc-register tbody tr').first.wait_for();page.locator('.fc-register tbody tr .fc-text-button').first.click();page.get_by_role('dialog',name='Financial record details').wait_for();check('Collection detail drawer opens',page.get_by_role('dialog').count()==1);page.keyboard.press('Escape');check('Escape closes drawer',page.get_by_role('dialog').count()==0)
 for report in ['custom','collections','receipts','invoices','outstanding','overdue','payments','expenses','settlements','reconciliation','income-statement','budget-actual','entity']:
  page.goto('http://127.0.0.1:1249/app/finance/reports/'+report);page.locator('.fc-report-library').wait_for();page.wait_for_timeout(350);check('Report route '+report,page.locator('.fc-page [role="alert"]').count()==0)
 for route,button,title in [('invoices','Create invoice','Create invoice'),('receipts','Record collection','Record pending collection'),('expenses','Add expense','Create expense claim'),('budgets','Create budget','Create budget')]:
  page.goto('http://127.0.0.1:1249/app/finance/'+route);page.get_by_role('heading',name={'invoices':'Invoices & Billing','receipts':'Receipts & Collections','expenses':'Expenses','budgets':'Budgets & Allocation'}[route],exact=True).wait_for();page.locator('.fc-page-header').get_by_role('button',name=button,exact=True).click();page.get_by_role('dialog',name=title,exact=True).wait_for();check('Direct creation workflow '+route,page.get_by_role('dialog',name=title,exact=True).count()==1);page.get_by_role('button',name='Close',exact=True).click()
 page.goto('http://127.0.0.1:1249/app/finance/dues');page.get_by_role('heading',name='Dues & Membership',exact=True).wait_for();page.wait_for_timeout(500);check('Dues workspace renders',page.get_by_role('heading',name='Dues & Membership',exact=True).count()==1);check('Dues no API error',page.locator('[role="alert"]').count()==0);check('Dues desktop no overflow',page.evaluate('document.documentElement.scrollWidth<=innerWidth'));page.set_viewport_size({'width':390,'height':844});page.wait_for_timeout(200);check('Dues mobile no overflow',page.evaluate('document.documentElement.scrollWidth<=innerWidth'));page.screenshot(path='docs/qa/finance-command-dues-mobile.png',full_page=True)
 check('No uncaught browser errors',not errors)
 browser.close()
pathlib.Path('docs/qa/finance-command-browser-results.json').write_text(json.dumps(dict(results=results,errors=errors),indent=2));print(json.dumps(dict(passed=sum(r['passed'] for r in results),failed=sum(not r['passed'] for r in results),errors=errors)))
assert all(r['passed'] for r in results)
