import json,os,pathlib,uuid
os.environ['CLUBS_TEST_URL']='http://127.0.0.1:5115';os.environ['CLUBS_TEST_DATABASE']='clubs_certified_people_qa_20261118'
from support import Client,PASSWORD
from playwright.sync_api import sync_playwright,expect
expect.set_options(timeout=20000)
out=pathlib.Path('docs/qa/certified-people');fixture=json.loads((out/'fixture.json').read_text());checks=[];errors=[];UI='http://127.0.0.1:1255'
def check(name,ok=True):
 checks.append({'test':name,'passed':bool(ok)});print(('PASS ' if ok else 'FAIL ')+name,flush=True);assert ok,name
with sync_playwright() as p:
 browser=p.chromium.launch()
 def page_for(email=None):
  context=browser.new_context(viewport={'width':1440,'height':1000})
  if email:
   c=Client();assert c.login(email)[0]==200
   context.add_cookies([{'name':x.name,'value':x.value,'domain':'127.0.0.1','path':'/'} for x in c.jar])
  page=context.new_page();page.on('pageerror',lambda e:errors.append(str(e)));return page
 admin=page_for('admin@example.test');admin.goto(UI+'/app/certified-people');expect(admin.get_by_role('heading',name='Certified people',exact=True)).to_be_visible();expect(admin.locator('.learning-card').first).to_be_visible();check('Platform Admin can open certified people register')
 admin.get_by_label('Search people',exact=True).fill(fixture['email']);expect(admin.locator('.learning-card')).to_have_count(1);expect(admin.locator('.learning-card').get_by_text('Independent participant',exact=True)).to_be_visible();check('Search finds independent workshop graduate')
 admin.get_by_role('button',name='Verify certificate',exact=True).click();expect(admin.locator('.learning-certificate').get_by_text('Platform verified',exact=True)).to_be_visible();check('Admin verification updates certificate status')
 admin.screenshot(path=str(out/'admin-register-desktop.png'),full_page=True)
 admin.get_by_role('button',name='Manage certifications',exact=True).click();expect(admin.get_by_role('heading',name='Training certifications',exact=True)).to_be_visible();expect(admin.get_by_text('TTT November workshop',exact=False)).to_have_count(0);check('Admin opens shared certification history')
 admin.set_viewport_size({'width':390,'height':844});check('Admin register has no mobile horizontal overflow',admin.evaluate('document.documentElement.scrollWidth<=innerWidth'));admin.screenshot(path=str(out/'admin-register-mobile.png'),full_page=True)
 learner=page_for(fixture['email']);learner.goto(UI+'/app/my-certifications');expect(learner.get_by_role('heading',name='My learning & certifications',exact=True)).to_be_visible();expect(learner.get_by_role('heading',name='Training certifications',exact=True)).to_be_visible();expect(learner.get_by_text('Platform verified',exact=True)).to_be_visible();check('Participant without workspace can open verified certifications')
 learner.screenshot(path=str(out/'independent-portal-desktop.png'),full_page=True)
 learner.get_by_role('button',name='Add certification',exact=True).click();dialog=learner.get_by_role('dialog');expect(dialog).to_be_visible();dialog.locator('select').select_option(fixture['programId']);dialog.get_by_label('Certificate ID / number',exact=True).fill('BROWSER-'+uuid.uuid4().hex[:8]);dialog.get_by_label('Batch',exact=True).fill('Browser QA workshop');dialog.get_by_role('button',name='Save',exact=True).click();expect(dialog).to_have_count(0);expect(learner.get_by_text('Self reported',exact=True)).to_be_visible();check('Independent participant adds another certification from portal')
 learner.set_viewport_size({'width':390,'height':844});check('Independent portal fits mobile',learner.evaluate('document.documentElement.scrollWidth<=innerWidth'));learner.screenshot(path=str(out/'independent-portal-mobile.png'),full_page=True)
 member=page_for('member.club1@example.test');member.goto(UI+'/app/my-certifications');expect(member.get_by_role('heading',name='My learning & certifications',exact=True)).to_be_visible();expect(member.locator('.learning-hero').get_by_text('Club member',exact=True)).to_be_visible();check('Club member uses same learning portal')
 member.goto(UI+'/app/certified-people');expect(member.get_by_text('Only Platform Admin can manage certified people.',exact=True)).to_be_visible();check('Participant cannot open Platform Admin module')
 signup=page_for();signup.goto(UI+'/signup?portal=1');expect(signup.get_by_label('How would you like to join?',exact=True)).to_have_value('portal');expect(signup.get_by_label('National',exact=True)).to_have_count(0);expect(signup.get_by_role('button',name='Create learning portal account',exact=True)).to_be_enabled();check('Public learning signup requires no club or region')
 signup.get_by_label('Full name',exact=True).fill('Browser Independent Learner');signup.get_by_label('Email address',exact=True).fill('browser-learner-'+uuid.uuid4().hex[:8]+'@example.test');signup.get_by_label('Mobile number',exact=True).fill('9999999997');signup.locator('input[name="password"]').fill(PASSWORD);signup.get_by_label('Confirm password',exact=True).fill(PASSWORD);signup.get_by_role('checkbox').check();signup.get_by_role('button',name='Create learning portal account',exact=True).click();expect(signup.get_by_role('heading',name='Verify your account',exact=True)).to_be_visible();check('Learning account signup succeeds without club membership')
 signup.set_viewport_size({'width':390,'height':844});check('Learning signup fits mobile',signup.evaluate('document.documentElement.scrollWidth<=innerWidth'));signup.screenshot(path=str(out/'learning-signup-mobile.png'),full_page=True)
 check('No browser runtime errors',not errors);browser.close()
(out/'browser-checks.json').write_text(json.dumps(checks,indent=2),encoding='utf-8');print(str(len(checks))+' browser checks passed')
