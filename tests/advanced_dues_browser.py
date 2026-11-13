"""UI checks use the isolated QA snapshot through Vite 1238."""
from playwright.sync_api import sync_playwright
from support import Client,sql,BASE
import json,pathlib,os
assert BASE=='http://127.0.0.1:5098' and os.environ['CLUBS_TEST_DATABASE'].startswith('clubs_impact_dues_center_qa_')
results=[]
def check(name,ok):
 results.append({'test':name,'passed':bool(ok)});print(('PASS ' if ok else 'FAIL ')+name,flush=True)
 pathlib.Path('docs/qa/advanced-dues-browser-results.json').write_text(json.dumps(results,indent=2));assert ok,name
c=Client();assert c.login('admin@example.test')[0]==200;opts=c.req('/finance/dues-center/options')[1]
with sync_playwright() as p:
 browser=p.chromium.launch(headless=True);context=browser.new_context(viewport={'width':1672,'height':1050},device_scale_factor=1)
 context.add_cookies([{'name':x.name,'value':x.value,'domain':'127.0.0.1','path':x.path,'httpOnly':True,'secure':False,'sameSite':'Lax'} for x in c.jar])
 page=context.new_page();errors=[];serverErrors=[];page.on('pageerror',lambda e:errors.append(str(e)));page.on('response',lambda r:serverErrors.append(r.url) if r.status>=500 else None)
 page.goto('http://127.0.0.1:1238/app/finance/dues');page.get_by_role('heading',name='Dues & Membership',exact=True).wait_for();page.locator('.dm-kpi strong').first.wait_for();page.wait_for_timeout(700)
 check('Dues center uses eight live KPI cards',page.locator('.dm-kpi').count()==8)
 check('No desktop horizontal overflow',page.evaluate('document.documentElement.scrollWidth<=innerWidth'))
 page.screenshot(path='docs/qa/advanced-dues-overview-desktop.png',full_page=True)
 for section in ['Organization View','Member Dues','Club Dues','Collections','Outstanding & Overdue','Renewals','Waivers & Discounts','Reminders','Reports','Overview']:
  page.locator('.dm-tabs').get_by_role('button',name=section,exact=True).click();page.wait_for_timeout(350)
  check(section+' workspace is selectable',page.locator('.dm-tabs').get_by_role('button',name=section,exact=True).get_attribute('aria-current')=='page')
 page.locator('.dm-tabs').get_by_role('button',name='Member Dues',exact=True).click();page.get_by_role('button',name='Open ledger').first.click();page.get_by_role('dialog',name='Member Dues Ledger').wait_for();page.wait_for_timeout(300)
 check('Member ledger shows invoice history',page.get_by_role('heading',name='Invoice and due history').count()==1)
 check('Member ledger has PDF/Excel/CSV statements',page.get_by_role('link',name='Statement PDF').count()==1 and page.get_by_role('link',name='Statement XLSX').count()==1 and page.get_by_role('link',name='Statement CSV').count()==1)
 page.screenshot(path='docs/qa/advanced-dues-member-ledger.png',full_page=True);page.get_by_role('button',name='Close drawer',exact=True).click()
 before=sql('SELECT count(*) FROM "FinanceInvoices"');page.locator('.dm-header .dm-gold').click();page.get_by_role('dialog',name='Generate Dues',exact=True).wait_for()
 check('Generation wizard opens',page.locator('.dm-wizard').count()==1)
 page.get_by_role('button',name='Continue',exact=True).click();page.locator('.dm-wizard input[type=date]').fill(opts['plans'][0]['effectiveFrom']);page.locator('.dm-plan-list input[type=checkbox]').first.check();page.get_by_role('button',name='Continue',exact=True).click();page.locator('.dm-preview-stats').wait_for()
 check('Preview exposes duplicate and exemption state',page.locator('.dm-wizard').get_by_role('columnheader',name='Due date / Status').count()==1)
 page.get_by_role('button',name='Save draft',exact=True).click();check('Draft visibly saved without issuing invoices','Draft saved in this browser' in page.locator('.dm-wizard').inner_text())
 check('Read-only preview and draft do not change invoices',before==sql('SELECT count(*) FROM "FinanceInvoices"'))
 page.screenshot(path='docs/qa/advanced-dues-generation-preview.png',full_page=True);page.get_by_role('button',name='Close drawer',exact=True).click()
 page.locator('.dm-header').get_by_role('button',name='Record Collection',exact=True).click();page.get_by_role('dialog',name='Record member payment').wait_for();check('Existing collection workflow remains available',page.get_by_role('button',name='Close drawer',exact=True).count()==1);page.get_by_role('button',name='Close drawer',exact=True).click()
 page.locator('.dm-tabs').get_by_role('button',name='Collections',exact=True).click();page.get_by_role('button',name='Bulk member collections',exact=True).click();page.get_by_role('dialog',name='Record bulk member collections').wait_for();check('Bulk entry shows independent amount/reference fields',page.get_by_role('columnheader',name='Transaction reference').count()==1);page.get_by_role('button',name='Close drawer',exact=True).click()
 page.locator('.dm-tabs').get_by_role('button',name='Overview',exact=True).click();page.locator('.dm-kpi').nth(3).click();check('Overdue KPI drills into reports',page.locator('.dm-tabs').get_by_role('button',name='Reports',exact=True).get_attribute('aria-current')=='page')
 page.locator('.dm-tabs').get_by_role('button',name='Overview',exact=True).click();page.locator('.dm-level-tabs').get_by_role('button',name='Region',exact=True).click();page.locator('.dm-table-link').first.click();check('Region drills into actual districts',page.locator('.dm-level-tabs').get_by_role('button',name='District',exact=True).get_attribute('class')=='active')
 for width in [768,390]:
  page.set_viewport_size({'width':width,'height':900});page.wait_for_timeout(250);check(str(width)+'px responsive no page overflow',page.evaluate('document.documentElement.scrollWidth<=innerWidth'));page.screenshot(path='docs/qa/advanced-dues-overview-'+str(width)+'.png',full_page=True)
 check('No JavaScript runtime errors',not errors);check('No server errors during user workflows',not serverErrors)
 pathlib.Path('docs/qa/advanced-dues-browser-diagnostics.json').write_text(json.dumps({'runtimeErrors':errors,'serverErrors':serverErrors},indent=2))
 browser.close()
