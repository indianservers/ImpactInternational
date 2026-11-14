import sys,json,pathlib,subprocess,re
sys.path.insert(0,'tests')
from support import Client,BASE
assert BASE=="http://127.0.0.1:5098"
from playwright.sync_api import sync_playwright
out=[]
def check(name,ok):
 out.append({'test':name,'passed':bool(ok)});print(('PASS ' if ok else 'FAIL ')+name,flush=True);pathlib.Path('docs/qa/compact-pages-final-checks.json').write_text(json.dumps(out,indent=2));assert ok,name
path='client/src/cards/IdCards.tsx';before=subprocess.check_output(['git','show','HEAD:'+path]).decode();after=pathlib.Path(path).read_text();titles=lambda s:set(re.findall(r'(?:<h[23][^>]*>[^<{]+|\btitle="[^"]+")',s));check('ID card studio preserves every original heading',titles(before)<=titles(after))
c=Client();assert c.login('admin@example.test')[0]==200
with sync_playwright() as p:
 b=p.chromium.launch();ctx=b.new_context(viewport={'width':1672,'height':1050});ctx.add_cookies([{'name':x.name,'value':x.value,'domain':'127.0.0.1','path':x.path} for x in c.jar]);page=ctx.new_page();errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
 heights={}
 for route in ['/id-cards','/finance/invoices','/districts','/zones','/clubs','/club-administration','/audit','/intelligence','/finance/dues']:
  page.goto('http://127.0.0.1:1242/app'+route);page.wait_for_timeout(650);height=page.evaluate('document.documentElement.scrollHeight');heights[route]=height;check(route+' default page under 1800px',height<1800)
  if route=='/id-cards':
   page.get_by_role('button',name='Workspace & Activity',exact=True).click();page.get_by_role('heading',name='Recent card activity',exact=True).wait_for();check('Card activity remains accessible',page.get_by_role('heading',name='Your identity workspace',exact=True).count()==1)
  if route=='/finance/invoices':check('Finance defaults to ten rows',page.locator('.fc-register tbody tr').count()<=10)
  if route=='/finance/dues':
   page.locator('.dm-tabs').get_by_role('button',name='Organization View',exact=True).click();page.locator('.dm-table-link').first.click();page.wait_for_timeout(200);check('Hierarchy drill URL records District level','level=District' in page.url);url=page.url;page.reload();page.wait_for_timeout(350);check('Hierarchy level and scope survive reload',page.url==url and page.locator('.dm-level-tabs').get_by_role('button',name='District',exact=True).get_attribute('class')=='active')
   page.locator('.dm-tabs').get_by_role('button',name='Analysis',exact=True).click();page.evaluate("document.documentElement.dataset.theme='dark'");page.wait_for_timeout(100);check('Dark section buttons use dark surface',page.locator('.page-sections-nav button').first.evaluate("e=>getComputedStyle(e).backgroundColor")!='rgb(255, 255, 255)');page.screenshot(path='docs/qa/compact-dues-analysis-dark.png',full_page=True)
   page.evaluate("document.documentElement.dataset.theme='light'");page.set_viewport_size({'width':390,'height':900});page.screenshot(path='docs/qa/compact-dues-analysis-mobile.png',full_page=True)
 check('Focused checks have no runtime errors',not errors)
 b.close()
pathlib.Path('docs/qa/compact-page-height-audit.json').write_text(json.dumps([{**r,'height':heights.get(r['route'],r['height'])} for r in json.loads(pathlib.Path('docs/qa/compact-page-height-audit.json').read_text())],indent=2))
