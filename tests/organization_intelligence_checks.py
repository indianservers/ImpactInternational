"""Organization hierarchy contracts, actual DB totals, CRUD and scope isolation. QA database only."""
import json,os,pathlib,sys,time,uuid,urllib.parse
from support import Client,sql,BASE
assert BASE=='http://127.0.0.1:5095' and os.environ['CLUBS_TEST_DATABASE']=='clubs_impact_dues_upgrade_20261113'
checks=[]
def check(name,ok,detail=None):
 checks.append(dict(scenario=name,passed=bool(ok),detail=detail))
 if not ok:print('FAIL',name,str(detail)[:200])
def fetch(c,query='',expected=200):
 s,d=c.req('/structure/intelligence?'+query);check('Endpoint '+query[:90],s==expected,dict(status=s,error=d if s>=400 else None));return d
c=Client();check('Admin login',c.login('saisatish@indianservers.com')[0]==200)
t=time.perf_counter();d=fetch(c,'depth=5&refresh=1');cold=round((time.perf_counter()-t)*1000,1)
root=d['root'];flat=[]
def walk(n):
 flat.append(n)
 for child in n['children']:walk(child)
walk(root);index={n['id']:n for n in flat};levels=['International','National','Region','District','Zone','Club']
check('Exactly one international root',root['parentId'] is None and sum(n['level']=='International' for n in flat)==1)
check('Every nested child uses actual parent and next level',all(ch['parentId']==n['id'] and levels.index(ch['level'])==levels.index(n['level'])+1 for n in flat for ch in n['children']))
check('No duplicate node identities',len(index)==len(flat))
check('No cycles',all(n['id']!=ch['id'] for n in flat for ch in n['children']))
for kind in levels:
 count=int(sql("SELECT count(*) FROM \"Orgs\" o JOIN \"Levels\" l ON l.\"Id\"=o.\"LevelId\" WHERE "+("o.\"ParentId\" IS NULL" if kind=="International" else "l.\"Name\"='"+kind+"'")))
 check(kind+' count matches database',d['counts'][kind]['total']==count)
 check(kind+' active and other reconcile',d['counts'][kind]['total']==d['counts'][kind]['active']+d['counts'][kind]['inactive'])
for n in flat[:36]:
 check('Immediate child and descendant club counts '+n['code'],n['immediateChildren']==len(n['children']) and n['clubs']==(1 if n['level']=='Club' else sum(ch['clubs'] for ch in n['children'])))
check('Members match distinct database persons',root['members']==int(sql('SELECT count(DISTINCT "PersonId") FROM "Memberships"')))
check('Active people bounded by total',root['activeMembers']<=root['members'])
check('Payload contains no contact fields',not any(k in json.dumps(d).lower() for k in ['primaryemail','password','primarymobile']))
check('Default progressive load omits deeper clubs',fetch(c)['meta']['emitted']<len(flat))
check('Depth one only immediate children',all(not n['children'] for n in fetch(c,'depth=1')['root']['children']))
for kind in levels[1:]:
 n=next(x for x in flat if x['level']==kind);b=fetch(c,'depth=5&unitId='+n['id']);check(kind+' scope root selection',b['root']['id']==n['id']);check(kind+' breadcrumb preserves recorded path',b['breadcrumbs'][-1]['id']==n['id'])
fetch(c,'unitId=invalid',400);fetch(c,'unitId='+str(uuid.uuid4()),404);fetch(c,'from=2030-01-01&to=2020-01-01',400);fetch(c,'from=no-date',400);fetch(c,'depth=no',400)
search=fetch(c,'search='+urllib.parse.quote('Region 01'));check('Search responds after cached unfiltered request',bool(search['matches']) and all('Region 01' in n['name'] or 'Region 01' in n['code'] for n in search['matches']))
check('Empty search meaningful',fetch(c,'search=zz-never-exists')['matches']==[])
regions=[n for n in flat if n['level']=='Region'];zone=next(n for n in flat if n['level']=='Zone');club=next(n for n in flat if n['level']=='Club');region=regions[0]
for role in ['international','national','regional','district','zonal','club']:
 r=Client();check(role+' login',r.login(role+'.president@example.test')[0]==200);b=fetch(r,'depth=5');ids=set()
 def scan(n):
  ids.add(n['id'])
  for ch in n['children']:scan(ch)
 scan(b['root']);check(role+' no ancestor leakage',b['root']['parentId'] is None or b['root']['parentId'] not in ids)
 if role in ['regional','district','zonal','club']:
  fetch(r,'unitId='+regions[-1]['id'],404)
  s,_=r.req('/structure/intelligence/'+regions[-1]['id']+'/details');check(role+' foreign detail blocked',s==404)
 for pin in b['pins']:check(role+' coordinate comes from scoped node',pin['id'] in ids)
s,detail=c.req('/structure/intelligence/'+club['id']+'/details');check('Unit details reuse original identity',s==200 and detail['unit']['id']==club['id']);check('Authorized officer dataset present',isinstance(detail['officers'],list));check('Authorized history dataset present',isinstance(detail['history'],list))
# Reversible edits and new fixtures remain only in the named QA database.
levelsByName={l['name']:l['id'] for l in d['levels']};code='ORG-QA-'+uuid.uuid4().hex[:10].upper()
payload=dict(name='Organization intelligence QA club',code=code,levelId=levelsByName['Club'],parentId=zone['id'],country='India',status='Active',version=1)
s,created=c.req('/organizations','POST',payload);check('Create club under actual Zone',s==200,created)
if s==200:
 cid=created['id'];check('Creation keeps parent',created['parentId']==zone['id']);edited={**created,'name':'Organization intelligence QA club edited'};s,value=c.req('/organizations/'+cid,'PUT',edited);check('Edit unit supported',s==200 and value['name']==edited['name']);s,_=c.req('/organizations/'+cid,'PUT',edited);check('Stale version rejected',s==409)
 s,_=c.req('/organizations','POST',payload);check('Duplicate code rejected',s in [400,409])
 if s!=200:
  s,current=c.req('/organizations/'+cid);s,value=c.req('/organizations/'+cid,'PUT',{**current,'status':'Archived'});check('QA club archive supported',s==200)
fetch(c,'refresh=1&depth=5')
s,_=c.req('/organizations','POST',{**payload,'code':'BAD-'+uuid.uuid4().hex[:8],'parentId':region['id']});check('Wrong parent level rejected',s==400)
s,_=c.req('/organizations','POST',{**payload,'code':'BAD-'+uuid.uuid4().hex[:8],'parentId':str(uuid.uuid4())});check('Invalid parent reference rejected',s==403)
s,_=c.req('/organizations','POST',{**payload,'code':'ROOT-'+uuid.uuid4().hex[:8],'levelId':root['levelId'],'parentId':None});check('Second root rejected',s==400)
# A second independent National branch demonstrates containment without name inference.
parent=root['id'];newBranch=[]
for kind in levels[1:]:
 s,row=c.req('/organizations','POST',dict(name='Organization QA shared display name',code='ORG-BRANCH-'+uuid.uuid4().hex[:12],levelId=levelsByName[kind],parentId=parent,country='India',status='Active',version=1))
 check('Independent branch create '+kind,s==200,row if s!=200 else None)
 if s!=200:break
 newBranch.append(row);parent=row['id']
if len(newBranch)==5:
 b=fetch(c,'unitId='+newBranch[0]['id']+'&depth=5&refresh=1');n=b['root']
 check('Second National branch has one actual descendant club',n['clubs']==1 and n['immediateChildren']==1)
 for expected in newBranch:
  check('Identical names do not infer parents '+expected['code'],n['id']==expected['id'] and n['parentId']==expected['parentId'])
  if n['children']:n=n['children'][0]
 check('Empty leaf has no child and no members',n['immediateChildren']==0 and not n['children'] and n['members']==0)
 s,_=c.req('/organizations/'+newBranch[0]['id'],'PUT',{**newBranch[0],'parentId':newBranch[-1]['id']});check('Cyclic or wrong-level reparent rejected',s==400)
 s,listing=c.req('/organizations?scopeId='+newBranch[0]['id']+'&clubs=true');check('Scoped existing club listing preserves branch',s==200 and listing['total']==1 and listing['items'][0]['id']==newBranch[-1]['id'])
 r=Client();r.login('regional.president@example.test');s,_=r.req('/organizations?scopeId='+newBranch[0]['id']);check('Existing listing cannot bypass scope filter',s==404)
fetch(c,'depth=5&refresh=1')
u=Client();s,_=u.req('/structure/intelligence');check('Anonymous endpoint blocked',s==401)
r=Client();r.login('member.club1@example.test');s,own=r.req('/structure/intelligence');check('Ordinary member sees only own permitted club',s==200 and own['root']['level']=='Club' and own['meta']['totalUnits']==1);check('Ordinary member private aggregates restricted',not own['capabilities']['members'] and not own['capabilities']['leadership'] and own['root']['members']==0)
t=time.perf_counter();fetch(c,'depth=5');warm=round((time.perf_counter()-t)*1000,1)
out=dict(total=len(checks),passed=sum(x['passed'] for x in checks),failed=[x for x in checks if not x['passed']],timing=dict(coldMs=cold,warmMs=warm),checks=checks)
pathlib.Path('docs/qa/organization-intelligence-results.json').write_text(json.dumps(out,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in out.items() if k!='checks'},indent=2));sys.exit(bool(out['failed']))
