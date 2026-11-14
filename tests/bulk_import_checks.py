"""Real HTTP/PostgreSQL import regressions. Run only against the isolated approval QA clone."""
import csv, datetime, io, json, os, pathlib, sys, time, uuid, zipfile
import xml.etree.ElementTree as ET
from decimal import Decimal
os.environ['CLUBS_TEST_URL'] = 'http://127.0.0.1:5099'
os.environ['CLUBS_TEST_DATABASE'] = 'clubs_approval_scope_20261114'
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from support import Client, sql, BASE
assert BASE == 'http://127.0.0.1:5099'
checks = []
out = pathlib.Path('docs/qa/bulk-import'); out.mkdir(parents=True, exist_ok=True)

def check(name, valid, detail=None):
    checks.append(dict(name=name, passed=bool(valid), detail=detail))
    (out/'results.json').write_text(json.dumps(dict(total=len(checks),passed=sum(c['passed'] for c in checks),checks=checks),indent=2),encoding='utf-8')
    assert valid, (name,detail)

def call(c,path,method='GET',data=None,expected=200):
    status,result=c.req(path,method,data)
    check(method+' '+path,status==expected,None if status==expected else dict(status=status,error=result))
    return result

def login(email):
    c=Client();check(email+' login',c.login(email)[0]==200);return c

def content(headers,rows):
    stream=io.StringIO();w=csv.writer(stream);w.writerow(headers);w.writerows(rows);return stream.getvalue().encode('utf-8')

def poll(c,id,states):
    until=time.monotonic()+45
    while time.monotonic()<until:
        s,d=c.req('/membership/imports/'+id);assert s==200,(s,d)
        if d['status'] in states:return d
        assert d['status']!='Failed',d
        time.sleep(.3)
    raise AssertionError(('timeout',d))

admin=login('saisatish@indianservers.com');prefix=uuid.uuid4().hex[:8]
club=json.loads(sql('SELECT row_to_json(o) FROM "Orgs" o WHERE "Code"=\'DEMO-R01-D01-Z01-C01\''))
foreign=json.loads(sql('SELECT row_to_json(o) FROM "Orgs" o WHERE "Code"=\'DEMO-R02-D01-Z01-C01\''))
category=sql('SELECT "Id" FROM "MembershipCategories" WHERE "TenantId"=\''+club['TenantId']+'\' AND "Active" LIMIT 1')

def upload(c,rows,key=None,headers=None,target=None):
    payload=content(headers or ['name','email','clubCode'],rows);key=key or uuid.uuid4().hex
    s,d=c.upload('/membership/imports','members.csv',payload,dict(clubId=(target or club)['Id'],categoryId=category,requestKey=key))
    check('Stage member import',s==200,d if s!=200 else None)
    return d,key,payload

def mapping(c,id,fields=None):
    call(c,'/membership/imports/'+id+'/mapping','POST',dict(fields=fields or {'name':'name','email':'email','clubCode':'clubCode'}),202)

# Generated blank and sample files must be readable and match supported schemas.
for kind in ['members','dues']:
    for format in ['csv','xlsx']:
        for sample in ['true','false']:
            status,body,headers=admin.request('/membership/import-template?kind='+kind+'&format='+format+'&sample='+sample)
            check(kind+' '+format+' '+sample+' template downloads',status==200)
            if format=='csv':
                data=list(csv.reader(io.StringIO(body.decode('utf-8-sig'))));check('CSV sample count',len(data)==(2 if sample=='true' else 1))
            else:
                with zipfile.ZipFile(io.BytesIO(body)) as archive:
                    check('Excel template has instructions',b'Instructions' in archive.read('xl/workbook.xml'))
            (out/f'{kind}-{sample}.{format}').write_bytes(body)

# Valid import, stable upload key, completed retry, and full audit/report evidence.
name='Bulk QA '+prefix
j,key,payload=upload(admin,[[name,prefix+'@example.test','']]);s,reused=admin.upload('/membership/imports','members.csv',payload,dict(clubId=club['Id'],categoryId=category,requestKey=key))
check('Repeated upload key reuses job',s==200 and reused['id']==j['id'])
s,_=admin.upload('/membership/imports','changed.csv',payload+b'bad',dict(clubId=club['Id'],categoryId=category,requestKey=key));check('Changed upload cannot reuse key',s==409)
mapping(admin,j['id']);poll(admin,j['id'],['Ready']);call(admin,'/membership/imports/'+j['id']+'/commit','POST',{},202);done=poll(admin,j['id'],['Completed'])
check('One member imported',done['importedRows']==1)
check('Member job shows uploader and fingerprint',bool(done['creator']) and len(done['hash'])==64)
call(admin,'/membership/imports/'+j['id']+'/commit','POST',{},200)
s,body,_=admin.request('/membership/imports/'+j['id']+'/report');report=list(csv.DictReader(io.StringIO(body.decode('utf-8-sig'))));check('Report includes assigned membership number',s==200 and bool(report[0]['membershipNumber']))

# Round-trip an actual generated Excel template and reject formula fixtures.
source=(out/'members-true.xlsx').read_bytes()
def workbook_fixture(data,replacements=None,formula=False):
    output=io.BytesIO()
    with zipfile.ZipFile(io.BytesIO(data)) as source,zipfile.ZipFile(output,'w',zipfile.ZIP_DEFLATED) as dest:
        for item in source.infolist():
            payload=source.read(item.filename)
            if item.filename.endswith('.xml'):
                text=payload.decode('utf-8')
                for old,new in (replacements or {}).items():text=text.replace(old,new)
                payload=text.encode('utf-8')
                if formula and item.filename=='xl/worksheets/sheet1.xml':
                    ns='{http://schemas.openxmlformats.org/spreadsheetml/2006/main}'
                    root=ET.fromstring(payload);cell=root.find('.//'+ns+'c[@r="A2"]')
                    cell.attrib.pop('t',None)
                    for child in list(cell):cell.remove(child)
                    ET.SubElement(cell,ns+'f').text='1+1';ET.SubElement(cell,ns+'v').text='2'
                    payload=ET.tostring(root,encoding='utf-8',xml_declaration=True)
            dest.writestr(item,payload)
    return output.getvalue()
excel=workbook_fixture(source,{'Sample Member (replace this row)':'Excel QA '+prefix,'sample.member@example.test':'excel.'+prefix+'@example.test'})
s,x=admin.upload('/membership/imports','members.xlsx',excel,dict(clubId=club['Id'],categoryId=category,requestKey=uuid.uuid4().hex));check('Generated Excel template accepted',s==200)
mapping(admin,x['id']);poll(admin,x['id'],['Ready']);call(admin,'/membership/imports/'+x['id']+'/commit','POST',{},202);check('Excel membership imported',poll(admin,x['id'],['Completed'])['importedRows']==1)
s,_=admin.upload('/membership/imports','formulas.xlsx',workbook_fixture(source,formula=True),dict(clubId=club['Id'],categoryId=category,requestKey=uuid.uuid4().hex));check('Excel formula rejected without execution',s==400)

# Existing duplicate: block blind commits, reject redundant affiliations, skip safely.
j,_,_=upload(admin,[[name,prefix+'@example.test','']]);mapping(admin,j['id']);d=poll(admin,j['id'],['NeedsReview'])
check('Existing member detected',d['totalReview']==1);call(admin,'/membership/imports/'+j['id']+'/commit','POST',{},409)
person=sql('SELECT c."PersonId" FROM "PersonContacts" c WHERE c."Value"=\''+prefix+'@example.test\' LIMIT 1')
call(admin,'/membership/imports/'+j['id']+'/resolve','POST',{'rows':[{'rowId':d['rows'][0]['id'],'existingPersonId':person}]},409)
call(admin,'/membership/imports/'+j['id']+'/resolve','POST',{'rows':[{'rowId':d['rows'][0]['id'],'skip':True,'createSeparate':True}]},400)
before=sql('SELECT count(*) FROM "Memberships"')
call(admin,'/membership/imports/'+j['id']+'/resolve','POST',{'rows':[{'rowId':d['rows'][0]['id'],'skip':True}]})
call(admin,'/membership/imports/'+j['id']+'/commit','POST',{},202);d=poll(admin,j['id'],['Completed'])
check('All-skipped job completes without duplicate memberships',d['importedRows']==0 and d['skippedRows']==1 and sql('SELECT count(*) FROM "Memberships"')==before)

# Two staged files race: the second must fail atomically at commit, not duplicate the member.
rows=[['Race '+prefix,'race.'+prefix+'@example.test','']]
first,_,_=upload(admin,rows);second,_,_=upload(admin,rows)
for j in [first,second]:mapping(admin,j['id']);poll(admin,j['id'],['Ready'])
call(admin,'/membership/imports/'+first['id']+'/commit','POST',{},202);poll(admin,first['id'],['Completed'])
call(admin,'/membership/imports/'+second['id']+'/commit','POST',{},202);d=poll(admin,second['id'],['Failed'])
check('Commit-time duplicate race rolls back',d['importedRows']==0 and 'duplicate' in d['error'])
call(admin,'/membership/imports/'+second['id']+'/retry','POST',{},202);d=poll(admin,second['id'],['NeedsReview']);check('Retry returns new duplicate for review',d['totalReview']==1)

# Error report spans more than one UI page.
j,_,_=upload(admin,[['',f'bad{i}.{prefix}@example.test',''] for i in range(55)]);mapping(admin,j['id']);d=poll(admin,j['id'],['NeedsReview']);check('All invalid rows counted',d['totalErrors']==55)
s,body,_=admin.request('/membership/imports/'+j['id']+'/report');check('Full report includes every page',s==200 and len(list(csv.reader(io.StringIO(body.decode('utf-8-sig')))))==56)

president=login('club.president@example.test')
j,_,_=upload(president,[['Forbidden '+prefix,'foreign.'+prefix+'@example.test',foreign['Code']]]);mapping(president,j['id']);d=poll(president,j['id'],['NeedsReview']);check('Cross-scope club code rejected',d['totalErrors']==1)
foreignjob,_,_=upload(admin,[['Foreign QA '+prefix,'far.'+prefix+'@example.test','']],target=foreign)
call(president,'/membership/imports/'+foreignjob['id'],expected=404)
call(president,'/membership/imports/'+foreignjob['id']+'/report',expected=404)
member=login('member.club1@example.test');call(member,'/membership/imports',expected=403);call(member,'/membership/import-audit',expected=403)

# Dues preview/commit with member numbers, exact precision, duplicate keys and periods.
entities=call(admin,'/membership/dues-imports/entities');check('Billing entities available',len(entities)>0)
entity=next((e for e in entities if sql('SELECT count(*) FROM "Memberships" m WHERE m."ClubId"=\''+club['Id']+'\' AND (m."ClubId"=(SELECT "OrgId" FROM "FinanceEntitys" WHERE "Id"=\''+e['id']+'\') OR EXISTS(SELECT 1 FROM "FinanceOrganizations" o WHERE o."EntityId"=\''+e['id']+'\' AND o."ServedOrgId"=m."ClubId"))')!='0'),None)
assert entity
membernum=sql('SELECT "Number" FROM "Memberships" WHERE "ClubId"=\''+club['Id']+'\' ORDER BY "JoinedAt" DESC LIMIT 1')
duesheaders=['key','memberNumber','amount','date','dueOn','purpose','description','periodStart','periodEnd','notes']
def preview(rows,c=admin,expected=200):
    s,d=c.upload('/membership/dues-imports/preview','dues.csv',content(duesheaders,rows),{'entityId':entity['id']})
    check('Dues file preview',s==expected,d if s!=expected else None);return d

charge=['DUES-'+prefix,membernum,'1200.00','2026-11-14','2026-12-14','ClubDues','Imported annual dues','2035-01-01','2035-12-31','Opening dues test']
data=preview([charge]);check('Dues preview ready without financial writes',data['ready']==1 and bool(data['token']))
token=data['token'];data=call(admin,'/membership/dues-imports/commit','POST',{'token':token});check('Dues created as drafts',data['created']==1 and data['state']=='Draft');invoice=data['rows'][0]['invoiceId']
stored=sql("SELECT \"Total\"::text||'|'||\"Paid\"::text||'|'||\"State\" FROM \"FinanceInvoices\" WHERE \"Id\"='"+invoice+"'").split('|')
check('Imported invoice has exact amount and no paid balance',Decimal(stored[0])==Decimal('1200') and Decimal(stored[1])==0 and stored[2]=='Draft',stored)
data=call(admin,'/membership/dues-imports/commit','POST',{'token':token});check('Dues commit retry safely reuses invoice',data['created']==0 and data['reused']==1)
data=preview([charge]);check('Reupload identifies imported dues',data['duplicates']==1 and data['ready']==0)
for label,changed in [('changed amount',{2:'1300.00'}),('different key same period',{0:'OTHER-'+prefix}),('invalid precision',{0:'PREC-'+prefix,2:'1.001'}),('unknown member',{0:'UNKNOWN-'+prefix,1:'NOT-A-MEMBER'}),('backward due date',{4:'2026-01-01'})]:
    row=charge.copy()
    for i,v in changed.items():row[i]=v
    data=preview([row]);check(label+' blocked',data['errors']==1 and not data.get('token'))
data=preview([charge,charge]);check('Repeated file keys blocked',data['errors']>0 and not data.get('token'))
newcharge=charge.copy();newcharge[0]='NEW-'+prefix;newcharge[7]='2036-01-01';newcharge[8]='2036-12-31'
periodcopy=newcharge.copy();periodcopy[0]='PERIOD-COPY-'+prefix;periodcopy[3]='2026-11-15'
data=preview([newcharge,periodcopy]);check('Same period with different issue dates blocked within file',data['errors']>0 and not data.get('token'))
data=preview([newcharge]);assert data['token']
other=login('admin@example.test');call(other,'/membership/dues-imports/commit','POST',{'token':data['token']},403)
call(admin,'/membership/dues-imports/commit','POST',{'token':'tampered'},400)
preview([newcharge],president,403)
call(member,'/membership/dues-imports/entities',expected=403)

# Force an intervening record after preview; final revalidation must stop the whole import.
call(admin,'/finance/entities/'+entity['id']+'/invoices','POST',dict(key='INTERVENE-'+prefix,customer='QA intervening invoice',purpose='ClubDues',date='2026-11-14',dueOn='2026-12-14',membershipId=sql('SELECT "Id" FROM "Memberships" WHERE "Number"=\''+membernum+'\''),lines=[dict(description='Intervening dues',quantity=1,rate=1200)]))
# The existing unperiodized invoice should not override a separately specified period; use a direct fixture update in QA only.
sql('UPDATE "FinanceInvoices" SET "PeriodStart"=\'2036-01-01\',"PeriodEnd"=\'2036-12-31\' WHERE "IdempotencyKey"=\'INTERVENE-'+prefix+'\'')
call(admin,'/membership/dues-imports/commit','POST',{'token':data['token']},409)

# Scope and revocation checks for a real non-platform billing officer in the isolated clone.
roleid=sql("SELECT a.\"RoleId\" FROM \"Assignments\" a JOIN \"Users\" u ON u.\"Id\"=a.\"UserId\" WHERE u.\"Email\"='club.president@example.test' LIMIT 1")
userid=sql("SELECT \"Id\" FROM \"Users\" WHERE \"Email\"='club.president@example.test'")
financegrant=str(uuid.uuid4())
sql("INSERT INTO \"RolePermissions\" (\"Id\",\"RoleId\",\"PermissionId\") VALUES (gen_random_uuid(),'"+roleid+"','finance.manage') ON CONFLICT DO NOTHING")
sql("INSERT INTO \"FinanceGrants\" SELECT (jsonb_populate_record(NULL::\"FinanceGrants\",to_jsonb(g)||jsonb_build_object('Id','"+financegrant+"','UserId','"+userid+"','Role','Billing'))).* FROM \"FinanceGrants\" g WHERE g.\"EntityId\"='"+entity['id']+"' LIMIT 1")
# Ensure the finance entity serves the foreign club too, so organizational scope is tested independently.
sql("INSERT INTO \"FinanceOrganizations\" SELECT (jsonb_populate_record(NULL::\"FinanceOrganizations\",to_jsonb(o)||jsonb_build_object('Id','"+str(uuid.uuid4())+"','ServedOrgId','"+foreign['Id']+"'))).* FROM \"FinanceOrganizations\" o WHERE o.\"EntityId\"='"+entity['id']+"' AND NOT EXISTS (SELECT 1 FROM \"FinanceOrganizations\" f WHERE f.\"EntityId\"=o.\"EntityId\" AND f.\"ServedOrgId\"='"+foreign['Id']+"') LIMIT 1")
scoped=charge.copy();scoped[0]='SCOPED-'+prefix;scoped[7]='2040-01-01';scoped[8]='2040-12-31'
pdata=preview([scoped],president);check('Scoped billing officer can preview own club',bool(pdata['token']))
far=scoped.copy();far[0]='FOREIGN-'+prefix;far[1]=sql("SELECT \"Number\" FROM \"Memberships\" WHERE \"ClubId\"='"+foreign['Id']+"' LIMIT 1")
fdata=preview([far],president);check('Billing entity grant cannot bypass club scope',fdata['errors']==1 and not fdata.get('token'))
sql("DELETE FROM \"FinanceGrants\" WHERE \"Id\"='"+financegrant+"'")
call(president,'/membership/dues-imports/commit','POST',{'token':pdata['token']},403)
check('Revoked billing authority produces no invoice',sql("SELECT count(*) FROM \"FinanceInvoices\" WHERE \"IdempotencyKey\"='bulk-dues:SCOPED-"+prefix+"'")=='0')
sql("DELETE FROM \"RolePermissions\" WHERE \"RoleId\"='"+roleid+"' AND \"PermissionId\"='finance.manage'")

audit=call(admin,'/membership/import-audit')
actions={r['action'] for r in audit};check('Audit covers upload, mapping, validation, review, failure and dues commit',{'member.import.upload','member.import.mapping','member.import.validated','member.import.row.reviewed','member.import.failed','dues.import.preview','dues.import.completed'} <= actions)
check('Financial audit ties imported invoice to row and file hash',int(sql('SELECT count(*) FROM "FinanceAudits" WHERE "Action"=\'dues.import.row\' AND "Detail" LIKE \'%'+invoice+'%\''))==1)
print(json.dumps(dict(total=len(checks),passed=sum(c['passed'] for c in checks)),indent=2))
