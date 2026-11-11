from support import Client,sql
import pathlib,json,pymupdf,zxingcpp
from PIL import Image
c=Client();c.login('member@example.test');club=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'GC-006\'');c.activate(next(w for w in c.req('/workspaces')[1] if w['role']=='Club Member' and w['orgId']==club));source=json.loads(sql('''SELECT row_to_json(x) FROM(SELECT r."Id" rid,e."Id" eid,r."Name" name,e."Title" title,e."Venue" venue FROM "EventRegistrations" r JOIN "Events" e ON e."Id"=r."EventId" JOIN "Persons" p ON p."Id"=r."PersonId" JOIN "Users" u ON u."Id"=p."UserId" WHERE u."Email"='member@example.test' AND r."Status"='Confirmed' AND e."Lifecycle"='Completed' ORDER BY e."CreatedAt" DESC LIMIT 1)x'''))
def Q(x):return "convert_from(decode('"+x.encode('utf-8').hex()+"','hex'),'UTF8')"
root=pathlib.Path('test-results/phase4-pdf');title='Jönköping leadership · '+'Meaningful connections '*7;name='Māyā Sén · Γειά';venue='Community learning centre · '+('Long address details '*22)
try:
 sql(f'''UPDATE "EventRegistrations" SET "Name"={Q(name)} WHERE "Id"='{source['rid']}';UPDATE "Events" SET "Title"={Q(title)},"Venue"={Q(venue)} WHERE "Id"='{source['eid']}';''')
 code,b,h=c.request('/events/'+source['eid']+'/passes/'+source['rid']+'/pdf','POST',b'{}',{'Content-Type':'application/json'});assert code==200,(code,b[:150]);(root/'unicode-long-pass.pdf').write_bytes(b);d=pymupdf.open(stream=b,filetype='pdf');text=''.join(p.get_text() for p in d);assert name in text and 'Jönköping' in text;decoded=[]
 for i,p in enumerate(d):
  pix=p.get_pixmap(matrix=pymupdf.Matrix(1.5,1.5));out=root/f'unicode-long-pass-{i+1}.png';pix.save(out);decoded+=zxingcpp.read_barcodes(Image.open(out))
  for block in p.get_text('dict')['blocks']:
   if block['type']==0:
    for line in block['lines']:
     for span in line['spans']:
      x0,y0,x1,y1=span['bbox'];assert x0>=40 and x1<=p.rect.width-38 and y0>=30 and y1<p.rect.height-25,(span['text'],span['bbox'])
 assert len(decoded)==1 and len(decoded[0].text)==64
 result={'unicode_text_preserved':True,'long_content_wrapped':True,'all_text_inside_page':True,'pages':len(d),'qr_independently_decoded':True};pathlib.Path('test-results/phase4-pdf-unicode.json').write_text(json.dumps(result,indent=2));print(result)
finally:
 sql(f'''UPDATE "EventRegistrations" SET "Name"={Q(source['name'])} WHERE "Id"='{source['rid']}';UPDATE "Events" SET "Title"={Q(source['title'])},"Venue"={Q(source['venue'])} WHERE "Id"='{source['eid']}';''')
