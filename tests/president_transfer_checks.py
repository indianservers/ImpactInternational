"""Run against an isolated clone with the API configured for that database."""
import os,json
from support import Client,sql
assert 'qa' in os.environ['CLUBS_TEST_DATABASE']
def call(c,p,m='GET',b=None,status=200):
 code,data=c.req(p,m,b);assert code==status,(p,code,data);return data
def actor(email,role):
 c=Client();assert c.login(email)[0]==200
 aid=sql("SELECT a.\"Id\" FROM \"Assignments\" a JOIN \"Users\" u ON u.\"Id\"=a.\"UserId\" JOIN \"Roles\" r ON r.\"Id\"=a.\"RoleId\" WHERE u.\"Email\"='%s' AND r.\"Name\"='%s' LIMIT 1"%(email,role))
 call(c,'/workspaces/'+aid+'/activate','POST',{},204);return c
rp=actor('member.zone2@example.test','Regional President');np=actor('member.district2@example.test','National President');cp=actor('club.president@example.test','Club President')
def club(code):return sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\''+code+'\'')
a=club('DEMO-R01-D01-Z01-C01');b=club('DEMO-R01-D01-Z01-C02');other=club('DEMO-R02-D01-Z01-C01')
m=json.loads(sql('SELECT row_to_json(m) FROM "Memberships" m JOIN "Persons" p ON p."Id"=m."PersonId" JOIN "Users" u ON u."Id"=p."UserId" WHERE u."Email"=\'member.club1@example.test\' AND m."Status"=\'Active\' LIMIT 1'))
# Fixture changes are restricted to the disposable database.
sql('UPDATE "MembershipPolicies" SET "FinancialClearanceRequired"=false');sql('UPDATE "MembershipTransfers" SET "Status"=\'Cancelled\' WHERE "MembershipId"=\''+m['Id']+'\' AND "Status"<>\'Completed\'')
body={'destinationClubId':other,'categoryId':m['CategoryId'],'reason':'Presidential transfer QA','version':m['Version']}
path='/members/'+m['Id']+'/president-transfer'
call(cp,path,'POST',body,403);call(rp,path,'POST',body,403)
body['destinationClubId']=b;body['version']-=1;call(rp,path,'POST',body,409);body['version']=m['Version']
result=call(rp,path,'POST',body);assert result['status']=='Completed'
assert sql('SELECT "Status" FROM "Memberships" WHERE "Id"=\''+m['Id']+'\'')=='Transferred'
nextid=result['destinationMembershipId'];nextm=json.loads(sql('SELECT row_to_json(m) FROM "Memberships" m WHERE "Id"=\''+nextid+'\''));assert nextm['ClubId']==b and nextm['PersonId']==m['PersonId']
body.update(destinationClubId=other,version=nextm['Version']);result=call(np,'/members/'+nextid+'/president-transfer','POST',body);assert result['status']=='Completed'
assert sql('SELECT "ClubId" FROM "Memberships" WHERE "Id"=\''+result['destinationMembershipId']+'\'')==other
print('PASS: role denial, regional boundary, stale version, regional transfer, national cross-region transfer, identity and history preservation')
