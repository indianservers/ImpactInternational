from support import Client
c=Client();assert c.login('admin@example.test')[0]==200;w=next(x for x in c.req('/workspaces')[1] if x['role']=='Association Super Admin' and x['association'].startswith('Global'));c.activate(w)
for p in ['/event-types','/events','/events-dashboard']:
 code,b=c.req(p);print(p,code,str(b)[:150])
