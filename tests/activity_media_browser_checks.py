"""Browser posting, direct uploads, playback, audience controls and responsive QA with a fake Google provider."""
import base64,json,os,pathlib,uuid
os.environ['CLUBS_TEST_URL']='http://127.0.0.1:5112'
os.environ['CLUBS_TEST_DATABASE']='clubs_activity_media_qa_20261117'
from support import Client
from playwright.sync_api import sync_playwright,expect
expect.set_options(timeout=20000)
assert os.environ['CLUBS_TEST_DATABASE'].startswith('clubs_activity_media_qa_')
out=pathlib.Path('docs/qa/activity-media');out.mkdir(parents=True,exist_ok=True)
UI='http://127.0.0.1:1252';checks=[]
def check(name,ok):
 checks.append({'test':name,'passed':bool(ok)});print(('PASS ' if ok else 'FAIL ')+name,flush=True);assert ok,name
def get(c,path):
 code,data=c.req(path);assert code==200,(path,code,data);return data
with sync_playwright() as p:
 browser=p.chromium.launch();errors=[]
 def signed_page(email):
  c=Client();assert c.login(email)[0]==200
  context=browser.new_context(viewport={'width':1440,'height':1000});context.add_cookies([{'name':x.name,'value':x.value,'domain':'127.0.0.1','path':x.path} for x in c.jar]);page=context.new_page();page.on('pageerror',lambda e:errors.append(str(e)));return page,c
 page,member=signed_page('club.president@example.test');page.goto(UI+'/app/community');expect(page.get_by_role('button',name='Post an update',exact=True)).to_be_visible()
 # Record a short real WebM entirely in memory for browser playback checks.
 encoded=page.evaluate('''async()=>{
  const canvas=document.createElement('canvas');canvas.width=480;canvas.height=270;
  const ctx=canvas.getContext('2d'),stream=canvas.captureStream(15),chunks=[];
  const recorder=new MediaRecorder(stream,{mimeType:'video/webm;codecs=vp8'});
  recorder.ondataavailable=e=>chunks.push(e.data);const done=new Promise(resolve=>recorder.onstop=resolve);recorder.start();
  for(let i=0;i<20;i++){ctx.fillStyle=i%2?'#154c60':'#245b75';ctx.fillRect(0,0,480,270);ctx.fillStyle='#fff';ctx.font='28px sans-serif';ctx.fillText('Impact activity',120,135);await new Promise(r=>setTimeout(r,70));}
  recorder.stop();await done;stream.getTracks().forEach(t=>t.stop());
  const bytes=new Uint8Array(await new Blob(chunks,{type:'video/webm'}).arrayBuffer());let str='';for(const b of bytes)str+=String.fromCharCode(b);return btoa(str);
 }''')
 page.get_by_role('button',name='Post an update',exact=True).click();dialog=page.get_by_role('dialog')
 expect(dialog.get_by_role('combobox',name='Who can see this activity?',exact=True)).to_be_visible()
 expect(dialog.get_by_role('combobox',name='Who can see this activity?',exact=True).locator('option')).to_have_count(4)
 check('Club president has four audience choices',dialog.get_by_role('combobox',name='Who can see this activity?',exact=True).locator('option').count()==4)
 dialog.get_by_role('combobox',name='Who can see this activity?',exact=True).select_option('Public')
 title='A day of community impact '+uuid.uuid4().hex[:6]
 dialog.get_by_label('Title',exact=True).fill(title);dialog.get_by_label('Activity update',exact=True).fill('Our members came together for a day of training, community service and meaningful conversations. Photos and a short video capture the highlights.')
 dialog.get_by_label('Location',exact=True).fill('Hyderabad, India')
 dialog.get_by_label('Add photos or videos',exact=True).set_input_files([{'name':'community.webp','mimeType':'image/webp','buffer':pathlib.Path('client/public/brand/impact-community.webp').read_bytes()},{'name':'activity.webm','mimeType':'video/webm','buffer':base64.b64decode(encoded)}])
 check('Composer queues both photos and videos',dialog.locator('.activity-upload-list>div').count()==2)
 page.screenshot(path=str(out/'composer-desktop.png'),full_page=True)
 # Abort one direct upload; retry should resume the saved post rather than duplicate it.
 interrupted=[False]
 def interrupt(route):
  if not interrupted[0] and route.request.method=='PUT':interrupted[0]=True;route.abort()
  else:route.continue_()
 page.route('http://127.0.0.1:5113/sessions/**',interrupt)
 dialog.get_by_role('button',name='Publish update',exact=True).click()
 expect(dialog.get_by_role('button',name='Save & retry uploads',exact=True)).to_be_enabled()
 check('Interrupted upload keeps saved activity with retry action',dialog.get_by_text('Activity saved. A media upload failed.',exact=False).is_visible())
 dialog.get_by_role('button',name='Save & retry uploads',exact=True).click();expect(page.get_by_role('dialog')).to_have_count(0,timeout=30000)
 page.get_by_label('Search community updates',exact=True).fill(title)
 card=page.locator('.cm-post').filter(has=page.get_by_role('heading',name=title,exact=True));expect(card).to_have_count(1)
 expect(page.locator('.cm-post')).to_have_count(1)
 expect(card.locator('img')).to_be_visible();page.wait_for_function("document.querySelector('.cm-post img')?.naturalWidth>0")
 check('Direct image upload displays in activity feed',card.locator('img').evaluate('(el)=>el.naturalWidth')>0)
 expect(card.locator('video')).to_have_count(1);card.locator('video').evaluate('(v)=>{v.muted=true;return v.play()}');page.wait_for_function("document.querySelector('.cm-post video')?.currentTime>0")
 check('Uploaded video plays with range streaming',card.locator('video').evaluate('(v)=>v.currentTime')>0)
 card.locator('video').evaluate('(v)=>v.pause()')
 check('Retry did not duplicate the activity post',card.count()==1)
 page.screenshot(path=str(out/'feed-desktop.png'),full_page=True)
 public_url=card.get_by_role('link',name='View public activity',exact=True).get_attribute('href')
 anonymous=browser.new_page(viewport={'width':1440,'height':1000});anonymous.goto(UI+public_url)
 expect(anonymous.get_by_role('heading',name=title,exact=True)).to_be_visible();expect(anonymous.locator('img')).to_be_visible();anonymous.wait_for_function("document.querySelector('.cm-post img')?.naturalWidth>0")
 check('Public activity opens with photos without login',anonymous.locator('.cm-post').count()==1)
 anonymous.screenshot(path=str(out/'public-desktop.png'),full_page=True)
 anonymous.set_viewport_size({'width':390,'height':844});check('Public feed mobile has no horizontal overflow',anonymous.evaluate('document.documentElement.scrollWidth<=innerWidth'))
 anonymous.screenshot(path=str(out/'public-mobile.png'),full_page=True)
 # Editing visibility must immediately remove anonymous access.
 card.get_by_role('button',name='Edit',exact=True).click();dialog=page.get_by_role('dialog');dialog.get_by_role('combobox',name='Who can see this activity?',exact=True).select_option('Club');dialog.get_by_role('button',name='Save update',exact=True).click();expect(page.get_by_role('dialog')).to_have_count(0)
 anonymous.reload();expect(anonymous.get_by_role('heading',name=title,exact=True)).to_have_count(0)
 expect(anonymous.get_by_role('heading',name='More stories coming soon',exact=True)).to_be_visible()
 check('Changing to club visibility removes the public story',anonymous.get_by_role('heading',name='More stories coming soon',exact=True).is_visible())
 # Restore a public fixture and check composer on a narrow display.
 card.get_by_role('button',name='Edit',exact=True).click();dialog=page.get_by_role('dialog');dialog.get_by_role('combobox',name='Who can see this activity?',exact=True).select_option('Public');dialog.get_by_role('button',name='Save update',exact=True).click();expect(page.get_by_role('dialog')).to_have_count(0)
 page.set_viewport_size({'width':390,'height':844});page.get_by_role('button',name='Post an update',exact=True).click();dialog=page.get_by_role('dialog')
 check('Composer mobile has no horizontal overflow',dialog.evaluate('(el)=>el.scrollWidth<=el.clientWidth'))
 page.screenshot(path=str(out/'composer-mobile.png'),full_page=True);dialog.get_by_role('button',name='Cancel',exact=True).click()
 page.evaluate("document.documentElement.dataset.theme='dark'");page.screenshot(path=str(out/'feed-dark.png'),full_page=True)
 admin,_=signed_page('admin@example.test');admin.goto(UI+'/app/community/storage');expect(admin.get_by_role('heading',name='Google media storage',exact=True)).to_be_visible()
 check('Platform Admin can manage connected Google accounts',admin.get_by_role('button',name='Connect Google account',exact=True).is_enabled())
 admin.get_by_role('button',name='Connect Google account',exact=True).click()
 expect(admin.get_by_text('Google account connected. Activities can now upload media.',exact=True)).to_be_visible()
 check('Platform Admin completes Google consent flow in browser',admin.url.endswith('storage?google=connected'))
 admin.screenshot(path=str(out/'storage-admin.png'),full_page=True)
 check('No browser runtime errors',not errors)
 browser.close()
(out/'browser-checks.json').write_text(json.dumps(checks,indent=2))
print('Passed '+str(len(checks))+' browser checks')
