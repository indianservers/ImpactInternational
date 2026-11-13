using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using static Clubs.Access;
namespace Clubs;

public static class OrganizationIntelligence
{
    static readonly MemoryCache Cache = new(new MemoryCacheOptions { SizeLimit = 32 });
    public sealed class Node
    {
        public Guid Id { get; set; } public Guid? ParentId { get; set; } public Guid LevelId { get; set; }
        public string Name { get; set; } = ""; public string Code { get; set; } = ""; public string Level { get; set; } = ""; public string Status { get; set; } = "";
        public string Country { get; set; } = ""; public string State { get; set; } = ""; public string City { get; set; } = "";
        public double? Latitude { get; set; } public double? Longitude { get; set; } public DateTimeOffset CreatedAt { get; set; }
        public int ImmediateChildren { get; set; } public int Clubs { get; set; } public int Members { get; set; } public int ActiveMembers { get; set; } public int Joined { get; set; } public int UnitsCreated { get; set; } public int PreviousCreated { get; set; } public int Filled { get; set; } public int Mandatory { get; set; }
        public bool Truncated { get; set; } public List<Node> Children { get; set; } = [];
    }
    public static void MapOrganizationIntelligence(this RouteGroupBuilder api)
    {
        api.MapGet("/structure/intelligence", Build);
        api.MapGet("/structure/intelligence/{id:guid}/details", async(Guid id, Access a, GovernanceService governance) => {
            var w=await a.Workspace("organization.view"); var grants=await a.Grants(w); var db=a.Db;
            var unit=await a.Visible(w).AsNoTracking().SingleOrDefaultAsync(o=>o.Id==id) ?? throw new ApiError(404,"Unit not found.");
            object? officers=null,history=null,decisions=null;
            if(grants.Contains("leadership.view")) officers=await db.OfficerAppointments.Where(x=>x.TenantId==w.TenantId&&x.OrgId==id).OrderByDescending(x=>x.StartsAt).Take(50).Select(x=>new {x.Id,x.Status,x.StartsAt,x.EndsAt,title=db.OfficerPositions.Where(p=>p.Id==x.PositionId).Select(p=>p.Title).FirstOrDefault(),name=db.Persons.Where(p=>p.Id==x.PersonId).Select(p=>p.DisplayName).FirstOrDefault()}).ToListAsync();
            if(grants.Contains("audit.view")) history=await db.OrganizationHistory.Where(x=>x.TenantId==w.TenantId&&x.OrgId==id).OrderByDescending(x=>x.At).Take(30).Select(x=>new{x.Id,x.At,action="Organization details updated"}).ToListAsync();
            if(grants.Contains("governance.approve")) decisions=await (await governance.Requests(w)).Where(x=>x.TenantId==w.TenantId&&x.OrgId==id).OrderByDescending(x=>x.CreatedAt).Take(30).Select(x=>new{x.Id,x.Title,x.Status,x.CreatedAt}).ToListAsync();
            return Results.Ok(new{unit,officers,history,decisions});
        });
    }
    public static async Task<IResult> Build(HttpRequest request,Access a)
    {
        var timer=System.Diagnostics.Stopwatch.StartNew();var w=await a.Workspace("organization.view");var grants=await a.Grants(w);var db=a.Db;
        var visible=await a.Visible(w).AsNoTracking().OrderBy(o=>o.Name).Select(o=>new Node{Id=o.Id,ParentId=o.ParentId,LevelId=o.LevelId,Name=o.Name,Code=o.Code,Status=o.Status,Country=o.Country,State=o.State,City=o.City,Latitude=o.Latitude,Longitude=o.Longitude,CreatedAt=o.CreatedAt}).ToListAsync();
        Check(visible.Count<=25000,"Narrow your organizational workspace.");
        var ids=visible.Select(o=>o.Id).ToArray();Guid? selected=null;var raw=request.Query["unitId"].ToString();if(raw!=""){Check(Guid.TryParse(raw,out _),"Invalid unit ID.");selected=Guid.Parse(raw);Check(ids.Contains(selected.Value),"Unit not found.",404);}
        DateOnly ReadDate(string key,DateOnly fallback){var value=request.Query[key].ToString();if(value=="")return fallback;Check(DateOnly.TryParseExact(value,"yyyy-MM-dd",out _),"Invalid date.");return DateOnly.ParseExact(value,"yyyy-MM-dd");}
        var today=DateOnly.FromDateTime(DateTime.UtcNow);var from=ReadDate("from",new DateOnly(today.Year,1,1));var to=ReadDate("to",today);Check(from<=to&&from.Year>=1900&&to<=today,"Invalid date range.");
        var start=new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue),TimeSpan.Zero);var end=new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue),TimeSpan.Zero);var prior=start-(end-start);
        var depthRaw=request.Query["depth"].ToString();Check(depthRaw==""||int.TryParse(depthRaw,out _),"Invalid depth.");var depth=depthRaw==""?3:Math.Clamp(int.Parse(depthRaw),1,5);
        var key=w.TenantId+"|"+w.UserId+"|"+selected+"|"+from+"|"+to+"|"+depth+"|"+string.Join(',',ids.Order())+"|"+string.Join(',',grants.Order());
        if(request.Query["search"].ToString()==""&&request.Query["refresh"]!="1"&&Cache.TryGetValue<object>(key,out var cached))return Results.Ok(cached);
        var levels=await db.Levels.Where(l=>l.TenantId==w.TenantId).ToDictionaryAsync(l=>l.Id,l=>l);
        var index=visible.ToDictionary(n=>n.Id);foreach(var n in visible){n.Level=levels[n.LevelId].ParentLevelId==null?"International":levels[n.LevelId].Name;if(n.ParentId!=null&&index.TryGetValue(n.ParentId.Value,out var parent))parent.Children.Add(n);}
        var root=selected!=null?index[selected.Value]:visible.FirstOrDefault(n=>n.ParentId==null||!index.ContainsKey(n.ParentId.Value));Check(root!=null,"No authorized organization root.",404);
        var now=DateTimeOffset.UtcNow;
        // Materialize affiliation identities only on the server; use sets to avoid double-counting people across clubs.
        var members=grants.Contains("members.view")?await db.Memberships.Where(m=>m.TenantId==w.TenantId&&ids.Contains(m.ClubId)&&m.JoinedAt<end).Select(m=>new{m.ClubId,m.PersonId,m.Status,m.EndsAt,m.JoinedAt}).ToListAsync():null;
        var positions=grants.Contains("leadership.view")?await db.OfficerPositions.Where(p=>p.TenantId==w.TenantId&&p.Active&&p.Mandatory).ToListAsync():[];
        var holders=grants.Contains("leadership.view")?await db.OfficerAppointments.Where(x=>x.TenantId==w.TenantId&&ids.Contains(x.OrgId)&&x.Status=="Active"&&x.StartsAt<=now&&x.EndsAt>=now).Select(x=>new{x.OrgId,x.PositionId}).Distinct().ToListAsync():null;
        var grouped=members?.GroupBy(m=>m.ClubId).ToDictionary(g=>g.Key,g=>g.ToList());
        (HashSet<Guid> People,HashSet<Guid> Active,HashSet<Guid> Joined) Aggregate(Node n){
            var people=new HashSet<Guid>();var active=new HashSet<Guid>();var joined=new HashSet<Guid>();
            if(grouped!=null&&grouped.TryGetValue(n.Id,out var rows))foreach(var m in rows){people.Add(m.PersonId);if(m.Status=="Active"&&(m.EndsAt==null||m.EndsAt>=now))active.Add(m.PersonId);if(m.JoinedAt>=start)joined.Add(m.PersonId);}
            n.ImmediateChildren=n.Children.Count;n.Clubs=n.Level=="Club"?1:0;n.UnitsCreated=n.CreatedAt>=start&&n.CreatedAt<end?1:0;n.PreviousCreated=n.CreatedAt>=prior&&n.CreatedAt<start?1:0;
            n.Mandatory=positions.Count(p=>p.LevelId==n.LevelId);n.Filled=positions.Count(p=>p.LevelId==n.LevelId&&holders!=null&&holders.Any(h=>h.OrgId==n.Id&&h.PositionId==p.Id));
            foreach(var child in n.Children){var sub=Aggregate(child);people.UnionWith(sub.People);active.UnionWith(sub.Active);joined.UnionWith(sub.Joined);n.Clubs+=child.Clubs;n.UnitsCreated+=child.UnitsCreated;n.PreviousCreated+=child.PreviousCreated;n.Mandatory+=child.Mandatory;n.Filled+=child.Filled;}
            n.Members=people.Count;n.ActiveMembers=active.Count;n.Joined=joined.Count;return(people,active,joined);
        }
        Aggregate(root!);
        var branch=new List<Node>();void Collect(Node n){branch.Add(n);foreach(var c in n.Children)Collect(c);}Collect(root!);
        var counts=branch.GroupBy(n=>n.Level).ToDictionary(g=>g.Key,g=>new{total=g.Count(),active=g.Count(n=>n.Status=="Active"),inactive=g.Count(n=>n.Status!="Active"),created=g.Count(n=>n.CreatedAt>=start&&n.CreatedAt<end),previous=g.Count(n=>n.CreatedAt>=prior&&n.CreatedAt<start)});
        var countries=branch.Where(n=>n.Level=="Club").GroupBy(n=>string.IsNullOrEmpty(n.Country)?"Not recorded":n.Country).Select(g=>new{name=g.Key,count=g.Count()}).ToList();
        var states=branch.Where(n=>n.Level=="Club").GroupBy(n=>string.IsNullOrEmpty(n.State)?"Not recorded":n.State).Select(g=>new{name=g.Key,count=g.Count()}).OrderByDescending(g=>g.count).Take(20).ToList();
        var pins=branch.Where(n=>n.Level=="Club"&&n.Latitude!=null&&n.Longitude!=null).Take(1000).Select(n=>new{n.Id,n.Name,n.Latitude,n.Longitude}).ToList();
        var crumbs=new List<object>();var cursor=root!;while(true){crumbs.Insert(0,new{cursor.Id,cursor.Name,cursor.Level});if(cursor.ParentId==null||!index.TryGetValue(cursor.ParentId.Value,out var p))break;cursor=p;}
        var search=request.Query["search"].ToString();Check(search.Length<=120,"Search is too long.");
        var matches=search==""?null:branch.Where(n=>n.Name.Contains(search,StringComparison.OrdinalIgnoreCase)||n.Code.Contains(search,StringComparison.OrdinalIgnoreCase)||n.Country.Equals(search,StringComparison.OrdinalIgnoreCase)||n.State.Equals(search,StringComparison.OrdinalIgnoreCase)).Take(50).Select(n=>new{n.Id,n.Name,n.Code,n.Level,n.ParentId,n.Members,n.Clubs}).ToList();
        var emitted=1;Node Trim(Node n,int d){
            var queue=new Queue<(Node Node,List<Node> Children,int Depth)>();queue.Enqueue((n,n.Children,d));n.Children=[];
            while(queue.Count>0){var (current,children,currentDepth)=queue.Dequeue();if(currentDepth<depth)foreach(var child in children){if(emitted>=1000)break;var next=child.Children;child.Children=[];current.Children.Add(child);emitted++;queue.Enqueue((child,next,currentDepth+1));}current.Truncated=current.ImmediateChildren>current.Children.Count;}
            return n;
        }
        var result=new{root=Trim(root!,0),counts,countries,states,pins,breadcrumbs=crumbs,matches,levels=levels.Values.Select(l=>new{l.Id,l.Name,l.ParentLevelId,l.IsClub}),capabilities=new{members=grants.Contains("members.view"),leadership=grants.Contains("leadership.view"),create=grants.Contains("organization.create"),edit=grants.Contains("organization.update"),events=grants.Contains("events.view"),decisions=grants.Contains("governance.approve"),audit=grants.Contains("audit.view")},meta=new{from,to,asOf=now,depth,emitted,totalUnits=branch.Count,elapsedMs=timer.ElapsedMilliseconds,cacheSeconds=20,note="Structure and active membership are current; dates filter recorded unit creation and joins. UTC date boundaries. No historical structure is inferred."}};
        // Search responses are not cached: the cache key deliberately describes only the aggregate snapshot.
        if(search=="")Cache.Set(key,(object)result,new MemoryCacheEntryOptions{AbsoluteExpirationRelativeToNow=TimeSpan.FromSeconds(20),Size=1});
        return Results.Ok(result);
    }
}
