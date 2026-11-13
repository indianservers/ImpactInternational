"""Verify the explicitly enabled local password-testing mode without changing role grants."""
import json, uuid
from support import Client

accounts = ['saisatish@indianservers.com','admin@example.test'] + [role+'.president@example.test' for role in ['international','national','regional','district','zonal','club']] + ['member.'+name+'@example.test' for name in ['club1','club2','zone2','district2','region2']]
results=[]
for email in accounts:
 c=Client()
 assert c.req('/auth/methods')[1]['memberOtpEnabled'] is False
 status,result=c.login(email);assert status==200,(email,status,result)
 status,me=c.req('/auth/me');assert status==200
 status,workspaces=c.req('/workspaces');assert status==200 and workspaces
 if email in accounts[:2]:
  assert me['platformAdmin'] is True and any('roles.manage' in w['permissions'] for w in workspaces)
 else:assert me['platformAdmin'] is False
 if email.startswith('member.'):
  assert all('communications.send' not in w['permissions'] and 'roles.manage' not in w['permissions'] for w in workspaces)
 assert c.req('/communications/announcements')[0]==200
 results.append(dict(email=email,login='passed',platformAdmin=me['platformAdmin']))
c=Client()
assert c.req('/auth/otp/request','POST',dict(identifier='member.club1@example.test'))[0]==503
assert c.req('/auth/otp/verify','POST',dict(challengeId=str(uuid.uuid4()),code='123456'))[0]==503
print(json.dumps(dict(accounts=results,otpRequest='paused',otpVerification='paused',memberScope='preserved'),indent=2))
