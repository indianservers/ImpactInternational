"""Business profile persistence, discovery, privacy and validation on an isolated QA database."""
import os, json
from support import Client, BASE
assert os.environ.get('CLUBS_TEST_DATABASE','').startswith('clubs_business_profile_qa_'), 'Use an isolated business QA database'
checks=[]
def check(name, condition):
    assert condition, name
    checks.append(name)
def get(c,path):
    status,data=c.req(path); assert status==200,(path,status,data); return data
member=Client(); check('member login',member.login('member.club1@example.test')[0]==200)
viewer=Client(); check('viewer login',viewer.login('member.club2@example.test')[0]==200)
p=get(member,'/networking/me'); pid=p['personId']
p.update(company='Aurora Digital Studio',businessCategory='Technology',businessSubcategory='Software development',businessDescription='Custom software and thoughtful digital experiences.',businessTags=['Web apps',' web apps ','Automation'],visible=True)
check('save business profile',member.req('/networking/me','PUT',p)[0]==200)
p=get(member,'/networking/me')
check('business fields round trip',p['businessSubcategory']=='Software development' and p['businessDescription'].startswith('Custom software'))
check('business tags normalized and deduplicated',set(p['businessTags'])=={'Web apps','Automation'})
check('stale update rejected',member.req('/networking/me','PUT',{**p,'version':p['version']-1})[0]==409)
check('oversized description rejected',member.req('/networking/me','PUT',{**p,'businessDescription':'a'*2001})[0]==400)
check('too many business tags rejected',member.req('/networking/me','PUT',{**p,'businessTags':[str(i) for i in range(31)]})[0]==400)
for query in ['category=Technology','subcategory=Software%20development','tag=Automation','search=Automation','search=thoughtful']:
    check('discovery '+query,any(x['id']==pid for x in get(viewer,'/networking/people?'+query)['items']))
options=get(viewer,'/networking/options');check('business filter options populated','Technology' in options['categories'] and 'Software development' in options['subcategories'] and 'Automation' in options['businessTags'])
detail=get(viewer,'/networking/people/'+pid);check('visible business detail fields',detail['businessTags']==p['businessTags'] and detail['businessSubcategory']==p['businessSubcategory'])
legacy={k:v for k,v in p.items() if k not in ['businessTags','businessSubcategory','businessDescription']}
check('legacy networking editor accepted',member.req('/networking/me','PUT',legacy)[0]==200)
p=get(member,'/networking/me');check('legacy networking editor preserves new business fields',set(p['businessTags'])=={'Web apps','Automation'} and p['businessSubcategory']=='Software development')
p['visible']=False;check('hide business profile',member.req('/networking/me','PUT',p)[0]==200)
check('hidden business detail unavailable',viewer.req('/networking/people/'+pid)[0]==404)
check('hidden business search excluded',all(x['id']!=pid for x in get(viewer,'/networking/people?tag=Automation')['items']))
check('hidden profile omitted from options','Automation' not in get(viewer,'/networking/options')['businessTags'])
p=get(member,'/networking/me');p['visible']=True;check('restore published QA profile',member.req('/networking/me','PUT',p)[0]==200)
own_status,own=member.req('/persons/me','POST',{})
check('own member profile exposes business fields',own_status==200 and set(own['professional']['businessTags'])=={'Web apps','Automation'})
payload={k:v for k,v in own.items() if k not in ['id','reference','name','professional','privacy','hasPhoto','contactAllowed','roles']}
payload.update(own['professional'])
payload['businessDescription']='Software built for growing businesses.'
check('member profile editor saves business fields',member.req('/persons/me','PUT',payload)[0]==200)
legacy={k:v for k,v in payload.items() if k not in ['businessSubcategory','businessDescription','businessTags']}
legacy['version']+=1
check('legacy member editor accepted',member.req('/persons/me','PUT',legacy)[0]==200)
current=get(member,'/networking/me')
check('legacy member editor preserves business fields',current['businessDescription']=='Software built for growing businesses.' and set(current['businessTags'])=={'Web apps','Automation'})
clear={**payload,'version':current['version'],'businessTags':[],'businessDescription':'','businessSubcategory':''}
check('member editor clears business fields explicitly',member.req('/persons/me','PUT',clear)[0]==200)
cleared=get(member,'/networking/me')
check('explicit clears persist',cleared['businessTags']==[] and cleared['businessDescription']=='' and cleared['businessSubcategory']=='')
payload['version']=cleared['version']
check('restore QA business fixture',member.req('/persons/me','PUT',payload)[0]==200)
print(json.dumps({'passed':len(checks),'checks':checks},indent=2))
