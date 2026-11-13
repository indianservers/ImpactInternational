"""Invitation acceptance and expiry checks against QA-only invitations."""
import json,os,pathlib,uuid
from support import Client,sql,BASE
assert BASE=='http://127.0.0.1:5095' and os.environ['CLUBS_TEST_DATABASE'].startswith('clubs_impact_dues_upgrade_')
a=Client();assert a.login('admin@example.test')[0]==200
m=Client();assert m.login('member.club2@example.test')[0]==200
wrong=Client();assert wrong.login('member.zone2@example.test')[0]==200
club=sql('''SELECT "Id" FROM "Orgs" o WHERE o."Code" LIKE 'DEMO-%C%' AND NOT EXISTS(SELECT 1 FROM "MembershipApplications" ma JOIN "Users" u ON u."Id"=ma."ApplicantUserId" WHERE ma."ClubId"=o."Id" AND u."Email"='member.club2@example.test') ORDER BY o."Code" LIMIT 1 ''');category=a.req('/membership/options')[1]['categories'][0]['id']
results=[]
def check(name,status,expected):
 results.append(dict(scenario=name,passed=status==expected,status=status));assert status==expected,(name,status,expected)
s,i=a.req('/membership/invitations','POST',dict(clubId=club,categoryId=category,email='member.club2@example.test',message='QA invitation acceptance regression'))
check('Intended-recipient invitation created',s,200);token=i['link'].split('token=')[1]
check('Another account cannot accept intended invitation',wrong.req('/membership/invitations/accept','POST',dict(token=token))[0],403)
s,accepted=m.req('/membership/invitations/accept','POST',dict(token=token));check('Verified intended recipient accepts invitation',s,200)
check('Same invitation cannot be consumed twice',m.req('/membership/invitations/accept','POST',dict(token=token))[0],409)
tracked=next(x for x in a.req('/membership/invitations')[1] if x['id']==i['id']);check('Acceptance tracked using real usage count',200 if tracked['uses']==1 else 500,200)
for state in ['expired','revoked']:
 s,i=a.req('/membership/invitations','POST',dict(clubId=club,categoryId=category));assert s==200
 if state=='expired':sql(f'''UPDATE "MembershipInvitations" SET "CreatedAt"=now()-interval '2 days',"ExpiresAt"=now()-interval '1 minute' WHERE "Id"='{i['id']}' ''')
 else:a.req('/membership/invitations/'+i['id'],'DELETE')
 check(state+' invitation cannot be accepted',m.req('/membership/invitations/accept','POST',dict(token=i['link'].split('token=')[1]))[0],409)
check('Oversized personal message rejected',a.req('/membership/invitations','POST',dict(clubId=club,categoryId=category,message='x'*2001))[0],400)
pathlib.Path('docs/qa/members-invitation-results.json').write_text(json.dumps(dict(total=len(results),passed=len(results),scenarios=results),indent=2));print(f'{len(results)} invitation checks passed')
