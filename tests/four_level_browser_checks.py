"""Browser regression against the isolated, migrated four-level QA database."""
import os,pathlib,subprocess,time,urllib.request,json,uuid
import support
from playwright.sync_api import sync_playwright,expect
expect.set_options(timeout=20000)
support.BASE='http://127.0.0.1:5088'
from support import Client
DB='clubs_four_level_qa_20261116'
results=[];evidence=pathlib.Path('docs/qa/four-level-certifications');evidence.mkdir(parents=True,exist_ok=True)
def check(name,ok,data=None):
 results.append({'test':name,'passed':bool(ok)});(evidence/'browser-results.json').write_text(json.dumps(results,indent=2))
 print(('PASS ' if ok else 'FAIL ')+name,flush=True);assert ok,(name,data)
env={**os.environ,'ASPNETCORE_ENVIRONMENT':'Development','ConnectionStrings__Database':'Host=127.0.0.1;Port=55432;Database='+DB+';Username=clubs','Authentication__MemberOtpEnabled':'false','Smtp__Host':'','Communications__MockBase':'http://127.0.0.1:5089','Logging__LogLevel__Default':'Warning'}
api_log=open('.infra/four-level-browser-api.log','w',encoding='utf-8');web_log=open('.infra/four-level-browser-web.log','w',encoding='utf-8')
process=subprocess.Popen(['dotnet',str(pathlib.Path('.infra/four-level-build/Clubs.Api.dll').resolve()),'--urls',support.BASE],env=env,stdout=api_log,stderr=api_log)
frontend=subprocess.Popen(['node',str(pathlib.Path('client/node_modules/vite/bin/vite.js').resolve()),'--host','127.0.0.1','--port','1248','--strictPort'],cwd='client',env={**os.environ,'CLUBS_API_TARGET':support.BASE},stdout=web_log,stderr=web_log)
try:
 for url in [support.BASE+'/health','http://127.0.0.1:1248']:
  for _ in range(100):
   try:urllib.request.urlopen(url);break
   except:time.sleep(.2)
 with sync_playwright() as playwright:
  browser=playwright.chromium.launch();errors=[]
  def page_for(email):
   client=Client();assert client.login(email)[0]==200
   context=browser.new_context(viewport={'width':1440,'height':1000});context.add_cookies([{'name':x.name,'value':x.value,'domain':'127.0.0.1','path':x.path,'httpOnly':True,'secure':False,'sameSite':'Lax'} for x in client.jar]);page=context.new_page();page.on('pageerror',lambda e:errors.append(str(e)));return page
  page=page_for('admin@example.test')
  page.goto('http://127.0.0.1:1248/app/organizations');expect(page.get_by_role('heading',level=1)).to_be_visible(timeout=20000)
  check('Organization screen has no District or Zone controls',page.get_by_role('link',name='Districts',exact=True).count()==0 and page.get_by_role('link',name='Zones',exact=True).count()==0)
  page.goto('http://127.0.0.1:1248/app/certifications');expect(page.get_by_role('button',name='Create certification program',exact=True)).to_be_visible()
  page.get_by_role('button',name='Create certification program',exact=True).click();page.get_by_label('Program name',exact=True).fill('Browser QA Program '+uuid.uuid4().hex[:8]);page.get_by_label('Description',exact=True).fill('A program created through Platform Admin.')
  page.get_by_role('button',name='Save',exact=True).click();expect(page.get_by_role('heading',name='Certification program',exact=True)).to_have_count(0)
  check('Platform Admin can create certification programs in browser',page.get_by_role('heading',name='Training certification programs',exact=True).is_visible())
  page.screenshot(path=str(evidence/'program-catalog.png'))
  page.goto('http://127.0.0.1:1248/app/members');expect(page.get_by_role('button',name='Add member',exact=True).first).to_be_visible()
  check('Member filters omit removed hierarchy levels',page.get_by_label('District',exact=True).count()==0 and page.get_by_label('Zone',exact=True).count()==0)
  page.get_by_role('button',name='Add member',exact=True).first.click()
  expect(page.get_by_role('heading',name='Member Information',exact=False)).to_be_visible()
  page.get_by_label('Full legal name',exact=False).fill('Browser Registration QA')
  page.get_by_role('dialog').get_by_role('button',name='Next',exact=True).click()
  for index in range(3):
   select=page.locator('.membership-location-field select').nth(index);expect(select).to_be_enabled();expect(select.locator('option')).not_to_have_count(1);select.select_option(index=1)
  category=page.get_by_role('combobox',name='Membership category',exact=True);expect(category.locator('option')).not_to_have_count(1);category.select_option(index=1)
  page.get_by_role('dialog').get_by_role('button',name='Next',exact=True).click();page.get_by_role('dialog').get_by_role('button',name='Next',exact=True).click()
  expect(page.get_by_role('button',name='Add training certification',exact=True)).to_be_visible()
  page.get_by_role('button',name='Add training certification',exact=True).click()
  expect(page.get_by_role('combobox',name='Training program',exact=True)).to_be_visible()
  check('Registration asks for certification program, number, batch and validity',page.get_by_label('Certificate ID / number',exact=True).count()==1 and page.get_by_label('Batch',exact=True).count()==1 and page.get_by_label('Valid till (blank for no expiry)',exact=True).count()==1)
  page.screenshot(path=str(evidence/'member-registration.png'))
  member=page_for('member.club1@example.test');member.goto('http://127.0.0.1:1248/app/profile');member.get_by_role('tab',name='Certifications',exact=True).click()
  expect(member.get_by_role('button',name='Add certification',exact=True)).to_be_visible()
  member.get_by_role('button',name='Add certification',exact=True).click()
  expect(member.get_by_role('dialog',name='Add training certificate',exact=True)).to_be_visible()
  program=member.get_by_role('combobox',name='Training program',exact=True);options=program.locator('option');expect(options).not_to_have_count(1);program.select_option(next(o.get_attribute('value') for o in options.all() if o.get_attribute('value') and 'retired' not in o.inner_text()))
  number='BROWSER-'+uuid.uuid4().hex[:8];member.get_by_label('Certificate ID / number',exact=True).fill(number);member.get_by_label('Batch',exact=True).fill('Browser batch');member.get_by_role('button',name='Save',exact=True).click()
  expect(member.get_by_text(number,exact=False).first).to_be_visible()
  check('Member can save certificate from account profile',member.get_by_text(number,exact=False).count()>0)
  member.screenshot(path=str(evidence/'member-certificates.png'))
  for email,title in [('club.president@example.test','Club'),('regional.president@example.test','Region')]:
   president=page_for(email);president.goto('http://127.0.0.1:1248/app');expect(president.get_by_role('heading',name='Your members',exact=True)).to_be_visible(timeout=20000)
   check(title+' President home displays scoped statistics and member links',president.get_by_role('heading',name='Active members',exact=True).count()==1 and president.locator('a[href^="/app/members/"]').count()>0)
   president.screenshot(path=str(evidence/(title.lower()+'-statistics.png')))
   president.goto('http://127.0.0.1:1248/app/events/new');expect(president.get_by_label('Activity name',exact=True)).to_be_visible()
   categories=president.get_by_role('combobox',name='Activity type',exact=True).locator('option').all_text_contents()
   check(title+' President activity form has exactly five categories',categories==['Service','Leadership','Business','Training','Nari Cell'])
   check(title+' activity form includes participants and coordinator details',president.get_by_label('ICI member participants',exact=True).count()==1 and president.get_by_label('Outside volunteers',exact=True).count()==1 and president.get_by_label('Meeting cost',exact=True).count()==1 and president.get_by_label('Project coordinator email',exact=True).count()==1)
   president.get_by_label('Activity name',exact=True).fill('Browser activity draft')
   if title=='Region':
    combo=president.get_by_role('combobox',name='Club name',exact=True);expect(combo.locator('option')).not_to_have_count(1);combo.select_option(index=1)
   president.get_by_role('button',name='Continue to schedule',exact=True).click();expect(president.get_by_label('Total hours',exact=True)).to_be_visible()
   check(title+' activity schedule displays financial-year restriction',president.get_by_text('Past activities and future schedules must stay within the current financial year:',exact=False).count()==1)
   president.screenshot(path=str(evidence/(title.lower()+'-activity-schedule.png')))
   president.set_viewport_size({'width':390,'height':844});president.goto('http://127.0.0.1:1248/app');expect(president.get_by_role('heading',name='Your members',exact=True)).to_be_visible();check(title+' statistics fits mobile viewport',president.evaluate('document.documentElement.scrollWidth<=innerWidth'))
  signup=browser.new_page();signup.goto('http://127.0.0.1:1248/signup');expect(signup.get_by_role('combobox',name='National',exact=True)).to_be_visible(timeout=20000)
  for label in ['National','Region']:
   combo=signup.get_by_role('combobox',name=label,exact=True);expect(combo).to_be_enabled();expect(combo.locator('option')).not_to_have_count(1);combo.select_option(index=1)
  expect(signup.get_by_role('combobox',name='Club',exact=True)).to_be_visible()
  check('Public signup uses National, Region, Club selectors',signup.get_by_role('combobox',name='Region',exact=True).count()==1 and signup.get_by_role('combobox',name='Club',exact=True).count()==1 and signup.get_by_role('combobox',name='District',exact=True).count()==0,signup.locator('select').evaluate_all('(nodes)=>nodes.map(n=>({name:n.labels?.[0]?.textContent,aria:n.getAttribute("aria-label")}))'))
  check('No browser runtime errors',not errors,errors)
  browser.close()
 print(str(len(results))+' browser checks passed.',flush=True)
finally:
 frontend.terminate();frontend.wait(timeout=15);process.terminate();process.wait(timeout=20);api_log.close();web_log.close()
