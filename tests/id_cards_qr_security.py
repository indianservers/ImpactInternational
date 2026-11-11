import sys,json,pathlib,copy
sys.path.insert(0,'tests')
from support import Client
c=Client();assert c.login('admin@example.test')[0]==200
ws=c.req('/workspaces')[1];w=next(x for x in ws if x['role']=='Association Super Admin' and x['association'].startswith('Global'));c.activate(w)
d=c.req('/id-cards/options')[1]['design'];results=[]
for name,kind in [('QR absent from enabled side','absent'),('QR too small','small'),('QR obscured by later layer','overlay')]:
 bad=copy.deepcopy(d)
 if kind=='absent':bad['doubleSided']=False
 elif kind=='small':
  q=next(e for e in bad['back']['elements'] if e['kind']=='Qr');q['width']=q['height']=10
 else:
  q=next(e for e in bad['back']['elements'] if e['kind']=='Qr');e=copy.deepcopy(q);e.update(id='overlay-security',kind='Rect',binding='');bad['back']['elements'].append(e)
 code,t=c.req('/id-cards/templates','POST',{'orgId':w['orgId'],'name':name,'design':bad});assert code==200,(code,t)
 code,_=c.req('/id-cards/templates/'+t['id']+'/publish','POST',{});assert code==400,(name,code);results.append({'test':name,'passed':True});print('PASS',name)
pathlib.Path('docs/idcard-qr-security.json').write_text(json.dumps(results,indent=2))
