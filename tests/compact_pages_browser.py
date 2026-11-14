"""Read-only layout and navigation checks against the isolated QA snapshot."""
import json,pathlib,os,re,subprocess
from playwright.sync_api import sync_playwright
from support import Client,BASE
assert BASE=='http://127.0.0.1:5098'
results=[]
def check(name,ok):
 results.append({'test':name,'passed':bool(ok)});print(('PASS ' if ok else 'FAIL ')+name,flush=True)
 pathlib.Path('docs/qa/compact-pages-results.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
 assert ok,name
# Every original literal panel heading must survive the reorganization.
changed=['DuesManagement','EventPlatform','FinanceCommand','LeadershipCenter','MembershipInsights','Organizations','PersonalEvents','PlatformAdminDashboard','MemberPortal','Intelligence','GovernanceClubs','StructureManagement','Audit']
for name in changed:
 path='client/src/pages/'+name+'.tsx';before=subprocess.check_output(['git','show','HEAD:'+path]).decode('utf-8');after=pathlib.Path(path).read_text(encoding='utf-8')
 headings=lambda s:set(re.findall(r'(?:<h[23][^>]*>[^<{]+|\btitle="[^"]+")',s))
 check(name+' preserves all original headings',headings(before)<=headings(after))
c=Client();assert c.login('admin@example.test')[0]==200
routes=['/finance/dues','/platform-dashboard','/membership-insights','/leadership','/leadership/intelligence','/organizations','/event-intelligence','/my-events','/finance','/finance/reports','/my-membership','/intelligence','/club-administration']
with sync_playwright() as p:
 browser=p.chromium.launch();context=browser.new_context(viewport={'width':1672,'height':1050})
 context.add_cookies([{'name':x.name,'value':x.value,'domain':'127.0.0.1','path':x.path,'httpOnly':True,'secure':False,'sameSite':'Lax'} for x in c.jar])
 page=context.new_page();errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
 for route in routes:
  page.goto('http://127.0.0.1:1242/app'+route);page.wait_for_timeout(600)
  check(route+' renders without runtime errors',not errors)
  check(route+' has no desktop overflow',page.evaluate('document.documentElement.scrollWidth<=innerWidth'))
  # Visit each direct subpage and its nested subpages without performing writes.
  root=page.locator('.page-sections').first
  if root.count():
   nav=root.locator('> .page-sections-nav');count=nav.locator('button').count()
   for i in range(count):
    root=page.locator('.page-sections').first;root.locator('> .page-sections-nav > button').nth(i).click();page.wait_for_timeout(120)
    check(route+' subpage '+str(i+1)+' selected',root.locator('> .page-sections-nav > button').nth(i).get_attribute('aria-current')=='page')
    inner=root.locator('.page-sections').first
    if inner.count():
     for j in range(inner.locator('> .page-sections-nav > button').count()):
      inner.locator('> .page-sections-nav > button').nth(j).click();page.wait_for_timeout(70)
      check(route+' nested section '+str(i+1)+'/'+str(j+1),inner.locator('> .page-sections-nav > button').nth(j).get_attribute('aria-current')=='page')
   selected=page.url;page.reload();page.wait_for_timeout(300);check(route+' reload preserves selected URL',page.url==selected)
  if route=='/finance/dues':
   check('Overview omits deep analysis and matrix',page.get_by_role('heading',name='Organization Collection Matrix').count()==0 and page.get_by_role('heading',name='Member Outstanding Distribution · Top 20').count()==0)
   check('Dues desktop overview fits within 1250 pixels',page.evaluate('document.documentElement.scrollHeight')<1250)
   page.screenshot(path='docs/qa/compact-dues-overview.png',full_page=True)
   page.locator('.dm-tabs').get_by_role('button',name='Organization View',exact=True).click();check('Organization matrix has its own subpage',page.get_by_role('heading',name='Organization Collection Matrix').count()==1)
   page.locator('.dm-tabs').get_by_role('button',name='Analysis',exact=True).click();nav=page.locator('.page-sections-nav')
   check('All eight dues analysis panels retained across four subpages',nav.locator('button').count()==4)
   for i in range(4):nav.locator('button').nth(i).click();check('Dues analysis group '+str(i+1)+' has two panels',page.locator('.dm-analysis-grid > section').count()==2)
   old=page.url;nav.locator('button').first.click();page.go_back();check('Browser Back restores selected analysis subpage',page.url==old and nav.locator('button').nth(3).get_attribute('aria-current')=='page')
   page.screenshot(path='docs/qa/compact-dues-analysis.png',full_page=True)
  if route=='/finance/reports':
   for label in ['Summary','Records','Report Library','Ledger','Forecast']:
    page.get_by_role('navigation',name='Finance subpages').get_by_role('button',name=label,exact=True).click();page.wait_for_timeout(150);check('Finance '+label+' accessible',page.get_by_role('navigation',name='Finance subpages').get_by_role('button',name=label,exact=True).get_attribute('aria-current')=='page')
  for width in [768,390]:
   page.set_viewport_size({'width':width,'height':900});page.wait_for_timeout(100)
   check(route+' '+str(width)+'px has no horizontal overflow',page.evaluate('document.documentElement.scrollWidth<=innerWidth'))
  page.set_viewport_size({'width':1672,'height':1050})
 # Smoke-check remaining static primary workspaces. Their existing tabs, wizards or registers remain intact.
 for route in ['/members','/directory','/international','/national','/regions','/districts','/zones','/clubs','/clubs/directory','/club-administration','/apply','/approvals','/imports','/membership-settings','/transfers','/events','/event-settings','/leadership-calendar','/handovers','/committees','/governance','/governance-settings','/resolutions','/administration','/settings','/audit','/profile','/id-cards','/intelligence','/operations','/communications','/finance/transactions','/finance/receipts','/finance/invoices','/finance/payments','/finance/expenses','/finance/reconciliation','/finance/budgets','/finance/audit']:
  page.goto('http://127.0.0.1:1242/app'+route);page.wait_for_timeout(200);check(route+' smoke render',not errors and page.locator('body').inner_text().strip()!='')
 page.goto('http://127.0.0.1:1242/app/audit');page.locator('tbody tr').first.wait_for()
 expected=c.req('/audit-logs?page=1')[1]['items']
 for number in [1,2,3]:
  page.wait_for_timeout(200);rows=page.locator('tbody tr');check('Audit page '+str(number)+' has at most ten rows',rows.count()<=10)
  if number<=2:
   actual=page.locator('tbody tr td:nth-child(2)').all_text_contents();part=expected[(number-1)*10:number*10];check('Audit page '+str(number)+' preserves exact source order',len(actual)==len(part) and all(a.strip().startswith(r['resource']) for a,r in zip(actual,part)))
  if number<3:page.get_by_role('button',name='Next',exact=True).click()
 check('No runtime errors across audited routes',not errors)
 browser.close()

