"""Read-only identity lookup and existing profile privacy checks."""
import json,pathlib,uuid,os
from support import Client,sql,BASE
assert BASE=='http://127.0.0.1:5095' and os.environ['CLUBS_TEST_DATABASE']=='clubs_impact_dues_upgrade_20261113'
checks=[]
def check(name,ok):
 checks.append({'test':name,'passed':bool(ok)});assert ok,name
def get(c,path):
 status,data=c.req(path);assert status==200,(path,status,data);return data
admin=Client();check('Platform Admin login',admin.login('saisatish@indianservers.com')[0]==200)
person='b06155bd-c607-44cd-918f-368599eacb95'
lookup=get(admin,'/members/by-person/'+person);check('Person resolves to authorized membership',bool(lookup['membershipId']) and lookup['directory']==False)
d=get(admin,'/members/'+lookup['membershipId']);check('Resolved member is the requested person',d['person']['id']==person)
check('Admin complete profile includes existing identity and sections',d['person']['legalName'] and all(k in d for k in ['membership','memberships','history','documents','activities','roles']))
leader=get(admin,'/leadership/people/'+person);check('Leadership profile and member profile refer to same person',leader['profile']['id']==d['person']['id'])
check('Leadership affiliation resolves to same membership',any(a['id']==lookup['membershipId'] for a in leader['affiliations']))
appointments=get(admin,'/leadership/center?status=Pending')['items'];check('Pending appointment links to this person',any(a['personId']==person for a in appointments))
a=next(a for a in appointments if a['personId']==person);record=get(admin,'/officer-appointments/'+a['id']);check('Appointment record retains person ID for profile tab',record['appointment']['personId']==person)
member=Client();check('Member login',member.login('member.club1@example.test')[0]==200)
check('Unlisted members stay unavailable to directory-only logins',member.req('/members/by-person/'+person)[0]==404)
check('Direct profile URL cannot bypass directory privacy',member.req('/members/'+lookup['membershipId']+'?directory=true')[0]==404)
own_person=sql('''SELECT p."Id" FROM "Persons" p JOIN "Users" u ON u."Id"=p."UserId" WHERE u."Email"='member.club1@example.test' ''')
public=get(member,'/members/by-person/'+own_person);check('Member lookup uses directory-safe mode',public['directory']==True)
check('Member can still open their own complete profile',get(member,'/members/'+public['membershipId']+'?directory=true')['person']['id']==own_person)
foreign=sql('''SELECT p."Id" FROM "Persons" p JOIN "Users" u ON u."Id"=p."UserId" WHERE u."Email"='member.region2@example.test' ''')
club=Client();check('Club President login',club.login('club.president@example.test')[0]==200)
check('Cross-region profile lookup is denied',club.req('/members/by-person/'+foreign)[0]==404)
check('Unknown person does not disclose information',admin.req('/members/by-person/'+str(uuid.uuid4()))[0]==404)
pathlib.Path('docs/qa/complete-member-profile/api-checks.json').write_text(json.dumps(checks,indent=2))
print(str(len(checks))+' complete profile checks passed; no data changed.')
