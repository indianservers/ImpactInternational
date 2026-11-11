"""Visual QA inputs from actual secured API PDF endpoints; no fabricated document output."""
from support import Client,sql
import pathlib,json
c=Client();c.login('member@example.test');club=sql('SELECT "Id" FROM "Orgs" WHERE "Code"=\'GC-006\'');c.activate(next(w for w in c.req('/workspaces')[1] if w['role']=='Club Member' and w['orgId']==club));person=sql('SELECT "Id" FROM "Persons" WHERE "UserId"=(SELECT "Id" FROM "Users" WHERE "Email"=\'member@example.test\')');row=sql(f'''SELECT r."Id"||','||r."EventId" FROM "EventRegistrations" r JOIN "Events" e ON e."Id"=r."EventId" WHERE r."PersonId"='{person}' AND r."Status"='Confirmed' AND e."Lifecycle"='Completed' AND EXISTS(SELECT 1 FROM "EventCheckins" a WHERE a."RegistrationId"=r."Id" AND a."SessionId" IS NULL AND NOT a."Voided") ORDER BY e."CreatedAt" DESC LIMIT 1''').split(',');rid,eid=row
root=pathlib.Path('test-results/phase4-pdf');root.mkdir(exist_ok=True)
for fmt in ['pdf','badge','png']:
 code,b,h=c.request('/events/'+eid+'/passes/'+rid+'/'+fmt,'POST',b'{}',{'Content-Type':'application/json'});assert code==200,(fmt,code,b[:200]);(root/({'pdf':'event-pass.pdf','badge':'event-badge.pdf','png':'event-qr.png'}[fmt])).write_bytes(b)
code,b,h=c.request('/events/'+eid+'/certificates/'+rid,'POST',b'{}',{'Content-Type':'application/json'});assert code==200;(root/'participation-certificate.pdf').write_bytes(b)
print('Downloaded actual pass, badge, QR and certificate for an already completed fictional event.')

import pymupdf,zxingcpp
from PIL import Image
results={}
for name in ['event-pass.pdf','event-badge.pdf','participation-certificate.pdf']:
 doc=pymupdf.open(root/name);text=''.join(page.get_text() for page in doc);assert 'CLUBSPHERE' in text.upper()
 for i,page in enumerate(doc):page.get_pixmap(matrix=pymupdf.Matrix(1.5,1.5)).save(str(root/(name+f'.{i+1}.png')))
 results[name]={'pages':len(doc),'text_characters':len(text),'valid':True,'size_bytes':(root/name).stat().st_size}
qr=zxingcpp.read_barcodes(Image.open(root/'event-qr.png'));pdfqr=zxingcpp.read_barcodes(Image.open(root/'event-pass.pdf.1.png'));assert len(qr)==len(pdfqr)==1 and qr[0].text==pdfqr[0].text and len(qr[0].text)==64 and all(c in '0123456789ABCDEF' for c in qr[0].text)
pathlib.Path('test-results/phase4-pdf-verification.json').write_text(json.dumps(results,indent=2));pathlib.Path('test-results/phase4-qr-verification.json').write_text(json.dumps({'png_decoded':True,'rendered_pdf_decoded':True,'same_opaque_payload':True,'payload_characters':64,'pii_in_payload':False},indent=2));print('Verified valid PDF outputs and independently decoded PNG and rendered-PDF QR payloads.')
