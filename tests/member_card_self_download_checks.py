"""Real member-session card downloads and negative authorization checks in the QA clone."""
import io,json,os,pathlib,sys,uuid,urllib.parse
os.environ['CLUBS_TEST_URL']='http://127.0.0.1:5100'
os.environ['CLUBS_TEST_DATABASE']='clubs_approval_scope_20261114'
sys.path.insert(0,str(pathlib.Path(__file__).parent))
from support import Client,sql,BASE,PASSWORD
assert BASE=='http://127.0.0.1:5100'
out=pathlib.Path('docs/qa/member-card-self-download');out.mkdir(parents=True,exist_ok=True)
checks=[]
def check(name,ok,detail=None):
    checks.append(dict(name=name,passed=bool(ok),detail=detail));(out/'results.json').write_text(json.dumps(dict(total=len(checks),passed=sum(c['passed'] for c in checks),checks=checks),indent=2),encoding='utf-8');assert ok,(name,detail)
def call(c,path,method='GET',body=None,expected=200):
    status,data=c.req(path,method,body);check(method+' '+path,status==expected,None if status==expected else dict(status=status,error=data));return data
def login(email):
    c=Client();check('Login '+email,c.login(email)[0]==200);return c
email=sql('SELECT u."Email" FROM "Users" u JOIN "MembershipApplications" a ON a."ApplicantUserId"=u."Id" WHERE u."Verified" AND u."Email" LIKE \'signup.%@example.test\' AND a."Status"=\'Approved\' ORDER BY a."SubmittedAt" DESC LIMIT 1')
assert email,'Run public_signup_checks.py against this clone first.'
member=login(email);other=login('member.club2@example.test');admin=login('saisatish@indianservers.com')
cards=call(member,'/my/id-card');card=next(c for c in cards if c['state']=='Legacy' and c['canDownload']);mid=card['membershipId'];cid=card['id']
check('Newly approved standard card is downloadable without custom issuance',card['canDownload'] and card['templateVersionId'] is None)
for format in ['pdf','front','back']:
    status,data,headers=member.request('/my/id-card/download?id='+cid+'&format='+format)
    check('Own '+format+' downloads successfully',status==200 and data.startswith(b'%PDF' if format=='pdf' else b'\x89PNG'),status)
    check(format+' is a named attachment','attachment' in headers.get('Content-Disposition','').lower() and card['memberId'] in headers.get('Content-Disposition',''))
    (out/('approved-member.'+('pdf' if format=='pdf' else format+'.png'))).write_bytes(data)
status,data,_=member.request('/my/id-card/download-latest');check('One-click latest card PDF works',status==200 and data.startswith(b'%PDF'))
status,data,_=member.request('/my/id-card/download-latest?membershipId='+mid);check('Member portal chosen-membership PDF works',status==200 and data.startswith(b'%PDF'))
import pymupdf
pdf=pymupdf.open(out/'approved-member.pdf');text=''.join(p.get_text() for p in pdf)
check('PDF includes both sides, member name and assigned number',len(pdf)==2 and card['memberId'] in text and card['memberName'] in text)
from PIL import Image
check('Printable front image has expected card dimensions',Image.open(out/'approved-member.front.png').size==(1012,638))
status,_,_=other.request('/my/id-card/download?id='+cid);check('Other member cannot download this card',status in [403,404])
call(other,'/my/id-card/download-latest?membershipId='+mid,expected=409)
anonymous=Client();call(anonymous,'/my/id-card/download-latest',expected=401)

club=json.loads(sql('SELECT row_to_json(o) FROM "Orgs" o WHERE o."Id"=(SELECT "ClubId" FROM "Memberships" WHERE "Id"=\''+mid+'\')'))
zone=json.loads(sql('SELECT row_to_json(o) FROM "Orgs" o WHERE "Id"=\''+club['ParentId']+'\''))
public=Client();pending_email='pending.card.'+uuid.uuid4().hex[:8]+'@example.test'
call(public,'/public/membership-signup','POST',dict(name='QA Pending Card',email=pending_email,phone='+919876543210',password=PASSWORD,confirmation=PASSWORD,agreed=True,districtId=zone['ParentId'],zoneId=zone['Id'],clubId=club['Id']),202)
uid=sql('SELECT "Id" FROM "Users" WHERE "Email"=\''+pending_email+'\'')
link=sql('SELECT "Link" FROM "Mails" WHERE "UserId"=\''+uid+'\' AND "Kind"=\'verify\' ORDER BY "ExpiresAt" DESC LIMIT 1')
call(public,'/auth/complete','POST',dict(token=urllib.parse.parse_qs(urllib.parse.urlsplit(link).query)['token'][0],password=''))
pending=login(pending_email);check('Pending applicant has no cards',call(pending,'/my/id-card')==[])
call(pending,'/my/id-card/download-latest',expected=409)
status,_,_=pending.request('/my/id-card/download?id='+cid);check('Pending applicant cannot download another member card',status in [403,404])

# Temporarily alter only the isolated fixture and always restore its current state.
for status in ['Suspended','Expired','Inactive']:
    sql('UPDATE "Memberships" SET "Status"=\''+status+'\' WHERE "Id"=\''+mid+'\'')
    try:
        check(status+' card metadata disables download',not next(c for c in call(member,'/my/id-card') if c['id']==cid)['canDownload'])
        call(member,'/my/id-card/download-latest',expected=409)
        call(member,'/my/id-card/download?id='+cid,expected=409)
    finally:sql('UPDATE "Memberships" SET "Status"=\'Active\' WHERE "Id"=\''+mid+'\'')
sql('UPDATE "DigitalCards" SET "RevokedAt"=now() WHERE "Id"=\''+cid+'\'')
try:
    call(member,'/my/id-card/download-latest',expected=409)
    call(member,'/my/id-card/download?id='+cid,expected=409)
finally:sql('UPDATE "DigitalCards" SET "RevokedAt"=NULL WHERE "Id"=\''+cid+'\'')
policy=call(admin,'/id-cards/policy')
disabled=call(admin,'/id-cards/policy','PUT',dict(allowMemberDownload=False,allowClubDesign=policy['allowClubDesign'],validityMonths=policy['validityMonths'],version=policy['version'],defaultTemplateVersionId=policy['defaultTemplateVersionId']))
try:
    call(member,'/my/id-card/download?id='+cid,expected=403)
finally:call(admin,'/id-cards/policy','PUT',dict(allowMemberDownload=policy['allowMemberDownload'],allowClubDesign=policy['allowClubDesign'],validityMonths=policy['validityMonths'],version=disabled['version'],defaultTemplateVersionId=policy['defaultTemplateVersionId']))
check('Download audit records owner and card',int(sql('SELECT count(*) FROM "Audits" WHERE "Action"=\'idcard.download\' AND "Resource"=\''+cid+'\''))>=5)
print(json.dumps(dict(total=len(checks),passed=sum(c['passed'] for c in checks)),indent=2))
