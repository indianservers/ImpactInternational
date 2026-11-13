"""Browser checks against the isolated events QA API through Vite 1237."""
from playwright.sync_api import sync_playwright
from support import Client
import json,pathlib
c=Client();assert c.login('admin@example.test')[0]==200
w=next(w for w in c.req('/workspaces')[1] if w['role']=='Association Super Admin' and w['association'].startswith('Global'));c.activate(w)
results=[]
with sync_playwright() as p:
 browser=p.chromium.launch(headless=True);context=browser.new_context(viewport={'width':1672,'height':1050},device_scale_factor=1)
 context.add_cookies([{'name':x.name,'value':x.value,'domain':'127.0.0.1','path':x.path,'httpOnly':True,'secure':False,'sameSite':'Lax'} for x in c.jar])
 page=context.new_page();errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
 for route,title in [('events','Events'),('my-events','My Impact Events'),('event-intelligence','Impact Event Intelligence')]:
  page.goto('http://127.0.0.1:1237/app/'+route);page.get_by_role('heading',name=title,exact=True).wait_for();page.wait_for_timeout(1200)
  page.screenshot(path='docs/qa/events-'+route+'-desktop.png',full_page=True)
  results.append({'test':route+' desktop page renders','passed':page.locator('.ep-hero').count()==1})
  results.append({'test':route+' no horizontal overflow','passed':page.evaluate('document.documentElement.scrollWidth<=innerWidth')})
  if route=='events':
   for view in ['Week','Day','Agenda','List','Month']:
    page.get_by_role('tab',name=view,exact=True).click();page.wait_for_timeout(150)
    results.append({'test':'Calendar '+view+' view','passed':page.get_by_role('tab',name=view,exact=True).get_attribute('aria-selected')=='true'})
  page.set_viewport_size({'width':390,'height':844});page.wait_for_timeout(300);page.screenshot(path='docs/qa/events-'+route+'-mobile.png',full_page=True)
  results.append({'test':route+' mobile no horizontal overflow','passed':page.evaluate('document.documentElement.scrollWidth<=innerWidth')})
  if route=='events':
   page.locator('.ep-metric').first.click();page.wait_for_timeout(200)
   results.append({'test':'Discovery metric opens list view','passed':page.get_by_role('tab',name='List',exact=True).get_attribute('aria-selected')=='true'})
  if route=='my-events':
   page.locator('.ep-metric').nth(1).click();page.wait_for_timeout(200)
   results.append({'test':'Pending invitation metric opens inbox','passed':page.get_by_role('tab',name='Invitations',exact=True).get_attribute('aria-selected')=='true'})
  page.set_viewport_size({'width':1672,'height':1050})
 page.goto('http://127.0.0.1:1237/app/events/new');page.get_by_role('heading',name='Create an event',exact=True).wait_for()
 results.append({'test':'Seven creation steps','passed':page.locator('.event-wizard aside>button').count()==7})
 page.get_by_label('Event title',exact=True).fill('Browser QA premium event')
 page.locator('.event-wizard select').nth(1).select_option(index=1)
 page.get_by_role('button',name='Continue to schedule',exact=True).click()
 page.get_by_label('Venue / physical address',exact=True).fill('QA Community Conference Hall')
 for expected in ['Audience & Scope','Registration','Program & Agenda','Communication','Review & Publish']:
  page.get_by_role('button',name='Save draft & continue',exact=True).click()
  page.locator('.event-wizard section h2').filter(has_text=expected).wait_for()
  results.append({'test':'Wizard saves and advances to '+expected,'passed':True})
 page.get_by_role('button',name='Save draft',exact=True).click();page.wait_for_timeout(300)
 results.append({'test':'Review draft persists without publication','passed':page.get_by_role('button',name='Submit for approval',exact=True).count()==1})
 page.goto('http://127.0.0.1:1237/app/event-intelligence');page.locator('.ep-report-grid').wait_for()
 for interval in ['Daily','Weekly','Monthly','Quarterly']:
  page.get_by_label('Registration chart interval').select_option(interval)
  results.append({'test':'Registration '+interval+' interval','passed':page.get_by_label('Registration chart interval').input_value()==interval})
 results.append({'test':'No runtime errors','passed':not errors,'errors':errors});browser.close()
pathlib.Path('docs/qa/events-browser-results.json').write_text(json.dumps(results,indent=2))
for r in results: print(('PASS ' if r['passed'] else 'FAIL ')+r['test'])
assert all(r['passed'] for r in results)
