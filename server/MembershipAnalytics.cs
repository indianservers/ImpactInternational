using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using ClosedXML.Excel;
using Microsoft.Extensions.Caching.Memory;
using static Clubs.Access;
namespace Clubs;

// Aggregates identities and historical affiliation snapshots in PostgreSQL; no member rows leave this service.
public static class MembershipAnalytics
{
    // Bounded short-lived aggregate cache. Authorization and current visible IDs are rechecked before every lookup.
    static readonly MemoryCache Cache=new(new MemoryCacheOptions{SizeLimit=64*1024*1024});
    record CachedAnalytics(Dictionary<string,JsonElement> Values,DateTimeOffset End,DateTimeOffset Expires);

    public static void MapMembershipAnalytics(this RouteGroupBuilder api)
    {
        api.MapGet("/membership/analytics", async (HttpRequest request, MembershipService s) => await Build(request, s));
        api.MapGet("/membership/analytics/export", async (HttpRequest request, MembershipService s) =>
        {
            var w=await s.Access.Workspace("members.export");
            var result=await Build(request,s);
            var json=JsonSerializer.SerializeToElement(result,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
            var format=request.Query["format"].ToString();if(format=="")format="csv";Check(format is "csv" or "xlsx","Choose CSV or Excel analytical export.");
            var cells=new List<(string Key,string Value)>();
            var lines=new List<string>{"dataset,key,value"};
            void Flatten(JsonElement el,string path){if(el.ValueKind==JsonValueKind.Object)foreach(var p in el.EnumerateObject())Flatten(p.Value,path+"."+p.Name);else if(el.ValueKind==JsonValueKind.Array){var i=0;foreach(var v in el.EnumerateArray())Flatten(v,path+"["+i+++ "]");}else {cells.Add((path,el.ToString()));lines.Add("analytics,"+Csv(path)+","+Csv(el.ToString()));}}
            Flatten(json, "membership");
            await s.Access.Log(w,"membership.analytics.export","aggregates");
            if(format=="xlsx"){
                using var book=new XLWorkbook();var sheet=book.AddWorksheet("Membership Analytics");sheet.Cell(1,1).Value="Metric / path";sheet.Cell(1,2).Value="Value";
                for(var i=0;i<cells.Count;i++){sheet.Cell(i+2,1).Value=MembershipImport.Safe(cells[i].Key);sheet.Cell(i+2,2).Value=MembershipImport.Safe(cells[i].Value);}
                sheet.SheetView.FreezeRows(1);sheet.Column(1).Width=55;sheet.Column(2).Width=60;using var stream=new MemoryStream();book.SaveAs(stream);
                return Results.File(stream.ToArray(),"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet","impact-membership-analytics.xlsx");
            }
            return Results.File(System.Text.Encoding.UTF8.GetBytes("\uFEFF"+string.Join("\r\n",lines)),"text/csv","impact-membership-analytics.csv");
        });
    }
    static string Csv(string value)=>"\""+MembershipImport.Safe(value).Replace("\"","\"\"")+"\"";
    public static async Task<object> Build(HttpRequest request,MembershipService s)
    {
        var watch=System.Diagnostics.Stopwatch.StartNew();
        var w=await s.Access.Workspace("members.view");var grants=await s.Access.Grants(w);var db=s.Db;
        string Read(string key)=>request.Query[key].ToString();
        Guid Id(string key){var text=Read(key);Check(text==""||Guid.TryParse(text,out _),"Invalid "+key+".");return text==""?Guid.Empty:Guid.Parse(text);}
        DateOnly? Date(string key){var text=Read(key);Check(text==""||DateOnly.TryParseExact(text,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _),"Invalid "+key+".");return text==""?null:DateOnly.ParseExact(text,"yyyy-MM-dd",CultureInfo.InvariantCulture);}
        var tz=TimeZoneInfo.FindSystemTimeZoneById(await db.Tenants.Where(t=>t.Id==w.TenantId).Select(t=>t.TimeZone).SingleAsync());
        var today=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,tz).DateTime);var firstMonth=new DateOnly(today.Year,today.Month,1);
        var period=Read("period");if(period=="")period="year";var bucket=Read("bucket");if(bucket=="")bucket="month";
        Check(new[]{"7d","30d","90d","quarter","previous-quarter","year","previous-year","all","custom"}.Contains(period),"Unsupported analytics period.");
        Check(new[]{"day","week","month","quarter","year"}.Contains(bucket),"Unsupported aggregation interval.");
        var mode=Read("workspace");if(mode=="")mode="overview";
        Check(new[]{"overview","growth","organization","cohorts","lifecycle","renewals","categories","geography","engagement","forecasting","actions","network"}.Contains(mode),"Unknown analytical workspace.");
        var visible=await s.Access.Visible(w).AsNoTracking().Select(o=>new AnalyticsOrg{Id=o.Id,ParentId=o.ParentId,Name=o.Name,LevelId=o.LevelId,Status=o.Status,Country=o.Country,State=o.State,Latitude=o.Latitude,Longitude=o.Longitude}).ToListAsync();
        Check(visible.Count<=10000,"Narrow your workspace before exploring more than 10,000 organizational units.");
        var allowed=visible.Select(o=>o.Id).ToArray();
        foreach(var (key,kind) in new[]{("nationalId","National"),("regionId","Region"),("districtId","District"),("zoneId","Zone"),("clubId","Club")})
        {
            var id=Id(key);if(id==Guid.Empty)continue;
            Check(await db.Orgs.AnyAsync(o=>o.Id==id&&o.TenantId==w.TenantId&&db.Levels.Any(l=>l.Id==o.LevelId&&l.Name==kind)) && (allowed.Contains(id)||await s.Access.Tree(w.TenantId,id).AnyAsync(o=>o.Id==w.OrgId)),kind+" outside authorized scope.",404);
            var branch=await s.Access.Tree(w.TenantId,id).Select(o=>o.Id).ToArrayAsync();allowed=allowed.Intersect(branch).ToArray();
        }
        var scoped=await s.ScopedList(w);var oldest=await scoped.MinAsync(m=>(DateTimeOffset?)m.JoinedAt);
        var quarter=new DateOnly(today.Year,((today.Month-1)/3)*3+1,1);
        var from=period switch{"7d"=>today.AddDays(-6),"30d"=>today.AddDays(-29),"90d"=>today.AddDays(-89),"quarter"=>quarter,"previous-quarter"=>quarter.AddMonths(-3),"previous-year"=>new DateOnly(today.Year-1,1,1),"all"=>oldest==null?today:DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(oldest.Value,tz).DateTime),"custom"=>Date("from")??today.AddDays(-29),_=>new DateOnly(today.Year,1,1)};
        var to=period switch{"previous-quarter"=>quarter.AddDays(-1),"previous-year"=>new DateOnly(today.Year,1,1).AddDays(-1),"custom"=>Date("to")??today,_=>today};
        Check(from>=new DateOnly(1900,1,1)&&from<=to&&to<=today,"Choose a valid range from 1900 through today.");
        var days=to.DayNumber-from.DayNumber+1;Check(days<=(bucket=="day"?366:bucket=="week"?1820:36600),"Choose a coarser aggregation interval for this date range.");
        foreach(var (a,b) in new[]{("joinedFrom","joinedTo"),("renewalFrom","renewalTo")})Check(Date(a)==null||Date(b)==null||Date(a)<=Date(b),"Reversed "+a+" range.");
        DateTimeOffset Bound(DateOnly d)=>new(TimeZoneInfo.ConvertTimeToUtc(d.ToDateTime(TimeOnly.MinValue,DateTimeKind.Unspecified),tz));
        var start=Bound(from);var end=to==today?DateTimeOffset.UtcNow:Bound(to.AddDays(1));var prior=Bound(from.AddDays(-days));var category=Id("categoryId");
        if(category!=Guid.Empty)Check(await db.MembershipCategories.AnyAsync(c=>c.Id==category&&c.TenantId==w.TenantId),"Category outside scope.",404);
        var status=Read("status");Check(status==""||new[]{"Active","Inactive","Suspended","Transferred","Expired","Resigned","Terminated","Deceased"}.Contains(status),"Unsupported status.");
        var country=Read("country");var state=Read("state");var type=Read("membershipType");Check(new[]{country,state,type}.All(x=>x.Length<=120),"Filter is too long.");
        var queryKey=string.Join("&",request.Query.Where(p=>p.Key is not "workspace" and not "format" and not "refresh").OrderBy(p=>p.Key).Select(p=>p.Key+"="+p.Value));
        var cacheKey=Hash(w.TenantId+"|"+w.UserId+"|"+w.Id+"|"+from+"|"+to+"|"+string.Join(',',allowed.Order())+"|"+string.Join(',',grants.Order())+"|"+queryKey);
        Cache.TryGetValue<CachedAnalytics>(cacheKey,out var cached);
        if(Read("refresh")!="")cached=null;
        if(cached!=null)end=cached.End;
        // Dates are half-open local-day boundaries. All filter values are parameters, never SQL fragments.
        var args=new List<NpgsqlParameter>();
        void Add(string name,NpgsqlDbType t,object? value)=>args.Add(new NpgsqlParameter(name,t){Value=value??DBNull.Value});
        Add("tenant",NpgsqlDbType.Uuid,w.TenantId);Add("allowed",NpgsqlDbType.Array|NpgsqlDbType.Uuid,allowed);Add("start",NpgsqlDbType.TimestampTz,start);Add("end",NpgsqlDbType.TimestampTz,end);Add("dueStart",NpgsqlDbType.TimestampTz,end);Add("prior",NpgsqlDbType.TimestampTz,prior);Add("tz",NpgsqlDbType.Text,tz.Id);Add("bucket",NpgsqlDbType.Text,bucket);Add("interval",NpgsqlDbType.Text,bucket=="quarter"?"3 months":"1 "+bucket);Add("category",NpgsqlDbType.Uuid,category);Add("status",NpgsqlDbType.Text,status);Add("country",NpgsqlDbType.Text,country);Add("state",NpgsqlDbType.Text,state);Add("type",NpgsqlDbType.Text,type);Add("committeesAllowed",NpgsqlDbType.Boolean,grants.Contains("committees.view"));Add("leadershipAllowed",NpgsqlDbType.Boolean,grants.Contains("leadership.view"));
        Add("isCurrent",NpgsqlDbType.Boolean,to==today);Add("pendingStates",NpgsqlDbType.Array|NpgsqlDbType.Text,MembershipStatistics.PendingApplicationStates);
        foreach(var key in new[]{"joinedFrom","joinedTo","renewalFrom","renewalTo"})Add(key,NpgsqlDbType.TimestampTz,Date(key) is {} d?Bound(key.EndsWith("To")?d.AddDays(1):d):null);
        const string Base="""
        WITH RECURSIVE f AS MATERIALIZED (
          SELECT m."Id",m."PersonId",m."ClubId",m."CategoryId",m."JoinedAt",m."EndsAt",m."Status",o."Country",o."State" FROM "Memberships" m JOIN "Orgs" o ON o."Id"=m."ClubId" AND o."TenantId"=@tenant
          WHERE m."TenantId"=@tenant AND m."ClubId"=ANY(@allowed) AND m."JoinedAt"<@end
          AND (@category='00000000-0000-0000-0000-000000000000' OR m."CategoryId"=@category)
          AND (@status='' OR m."Status"=@status) AND (@country='' OR o."Country"=@country) AND (@state='' OR o."State"=@state) AND (@type='' OR o."ClubType"=@type)
          AND (@joinedFrom IS NULL OR m."JoinedAt">=@joinedFrom) AND (@joinedTo IS NULL OR m."JoinedAt"<@joinedTo)
          AND (@renewalFrom IS NULL OR m."EndsAt">=@renewalFrom) AND (@renewalTo IS NULL OR m."EndsAt"<@renewalTo)
        ), history AS MATERIALIZED (
          SELECT h.*,lead(h."EffectiveAt") OVER(PARTITION BY h."MembershipId" ORDER BY h."EffectiveAt") AS next_at FROM(
          SELECT DISTINCT ON(hh."MembershipId",hh."EffectiveAt") hh."MembershipId",hh."EffectiveAt",hh."NewStatus",hh."EndsAt" FROM "MembershipHistory" hh JOIN f ON f."Id"=hh."MembershipId" WHERE hh."TenantId"=@tenant AND hh."EffectiveAt"<@end ORDER BY hh."MembershipId",hh."EffectiveAt",hh."RecordedAt" DESC,hh."Id" DESC) h
        ), snap AS MATERIALIZED (
          SELECT f.*,CASE WHEN @isCurrent THEN f."Status" ELSE h."NewStatus" END AS state,CASE WHEN @isCurrent THEN f."EndsAt" ELSE h."EndsAt" END AS valid_end FROM f LEFT JOIN history h ON h."MembershipId"=f."Id" AND h."EffectiveAt"<@end AND(h.next_at IS NULL OR h.next_at>=@end)
        ), people AS MATERIALIZED (SELECT "PersonId",min("JoinedAt") AS joined,bool_or(state='Active' AND(valid_end IS NULL OR valid_end>=@end)) AS active FROM snap GROUP BY "PersonId"),
        opening AS MATERIALIZED (SELECT DISTINCT f."PersonId" FROM f JOIN history h ON h."MembershipId"=f."Id" AND h."EffectiveAt"<@start AND(h.next_at IS NULL OR h.next_at>=@start) WHERE f."JoinedAt"<@start AND h."NewStatus"='Active' AND(h."EndsAt" IS NULL OR h."EndsAt">=@start))
        """;
        // Both SQL fragments are fixed source constants. All request values use typed Npgsql parameters.
#pragma warning disable EF1003
        async Task<JsonElement> Json(string sql){var clone=args.Select(p=>new NpgsqlParameter(p.ParameterName,p.NpgsqlDbType){Value=p.Value}).ToArray();var raw=await db.Database.SqlQueryRaw<AnalyticsJson>(Base+sql,clone).SingleAsync();using var doc=JsonDocument.Parse(raw.Json);return doc.RootElement.Clone();}
#pragma warning restore EF1003
        var summarySql="""
        SELECT jsonb_build_object('total',(SELECT count(*) FROM people),'affiliations',(SELECT count(*) FROM f),'active',(SELECT count(*) FROM people WHERE active),
        'new',(SELECT count(*) FROM people WHERE joined>=@start),'previousNew',(SELECT count(*) FROM people WHERE joined>=@prior AND joined<@start),
        'monthNew',(SELECT count(*) FROM people WHERE joined>=GREATEST(@start,date_trunc('month',@end-interval '1 microsecond'))),
        'yearNew',(SELECT count(*) FROM people WHERE joined>=GREATEST(@start,date_trunc('year',@end-interval '1 microsecond'))),
        'opening',(SELECT count(*) FROM opening),'retained',(SELECT count(*) FROM opening o JOIN people p ON p."PersonId"=o."PersonId" WHERE p.active),
        'tenureYears',(SELECT round(avg(extract(epoch FROM(@end-joined))/31557600)::numeric,2) FROM people),
        'unknownHistory',(SELECT count(*) FROM snap WHERE state IS NULL),
        'pendingApplications',(SELECT count(*) FROM "MembershipApplications" a JOIN "Orgs" ao ON ao."Id"=a."ClubId" AND ao."TenantId"=@tenant WHERE(@country='' OR ao."Country"=@country) AND(@state='' OR ao."State"=@state) AND(@type='' OR ao."ClubType"=@type) AND a."TenantId"=@tenant AND a."ClubId"=ANY(@allowed) AND a."Status"=ANY(@pendingStates) AND(@category='00000000-0000-0000-0000-000000000000' OR a."CategoryId"=@category)),
        'oldestHistory',(SELECT min(h."EffectiveAt") FROM "MembershipHistory" h JOIN f ON h."MembershipId"=f."Id" WHERE h."TenantId"=@tenant),
        'earliestJoin',(SELECT min(joined) FROM people))::text AS "Json"
        """;
        var trendSql="""
        , buckets AS (SELECT d AS left_at,LEAST(d+@interval::interval,@end) AS right_at FROM generate_series(date_trunc(@bucket,@start AT TIME ZONE @tz) AT TIME ZONE @tz,@end-interval '1 microsecond',@interval::interval) d),
        points AS(SELECT b.left_at,b.left_at>=@start AND b.right_at>=b.left_at+@interval::interval AS complete,to_char(b.left_at AT TIME ZONE @tz,'YYYY-MM-DD') AS date,
        count(DISTINCT f."PersonId")::int AS total,
        count(DISTINCT f."PersonId") FILTER(WHERE (CASE WHEN @isCurrent AND b.right_at=@end THEN f."Status" ELSE h."NewStatus" END)='Active' AND((CASE WHEN @isCurrent AND b.right_at=@end THEN f."EndsAt" ELSE h."EndsAt" END) IS NULL OR (CASE WHEN @isCurrent AND b.right_at=@end THEN f."EndsAt" ELSE h."EndsAt" END)>=b.right_at))::int AS active,
        count(DISTINCT f."PersonId") FILTER(WHERE h."NewStatus"='Inactive')::int AS inactive,
        count(DISTINCT f."PersonId") FILTER(WHERE h."NewStatus"='Expired' OR(h."NewStatus"='Active' AND h."EndsAt"<b.right_at))::int AS expired,
        (SELECT count(*)::int FROM people p WHERE p.joined>=GREATEST(b.left_at,@start) AND p.joined<b.right_at) AS new,
        (SELECT count(*)::int FROM "MembershipRenewals" r JOIN f rm ON rm."Id"=r."MembershipId" WHERE r."TenantId"=@tenant AND r."At">=GREATEST(b.left_at,@start) AND r."At"<b.right_at) AS renewals,
        (SELECT count(*)::int FROM "MembershipHistory" t JOIN f tm ON tm."Id"=t."MembershipId" WHERE t."TenantId"=@tenant AND t."NewStatus"='Transferred' AND t."EffectiveAt">=GREATEST(b.left_at,@start) AND t."EffectiveAt"<b.right_at) AS transfers
        FROM buckets b LEFT JOIN f ON f."JoinedAt"<b.right_at LEFT JOIN history h ON h."MembershipId"=f."Id" AND h."EffectiveAt"<b.right_at AND(h.next_at IS NULL OR h.next_at>=b.right_at) GROUP BY b.left_at,b.right_at ORDER BY b.left_at)
        SELECT coalesce(jsonb_agg(to_jsonb(points)-'left_at' ORDER BY left_at),'[]')::text AS "Json" FROM points
        """;
        var distributionsSql="""
        , groups AS(SELECT 'category' AS dimension,s."CategoryId"::text AS id,c."Name" AS name,count(*)::int AS count,count(*) FILTER(WHERE state='Active' AND(valid_end IS NULL OR valid_end>=@end))::int AS active FROM snap s JOIN "MembershipCategories" c ON c."Id"=s."CategoryId" GROUP BY s."CategoryId",c."Name"
        UNION ALL SELECT 'status',coalesce(state,'Unknown'),coalesce(state,'Unknown'),count(*)::int,count(*) FILTER(WHERE state='Active' AND(valid_end IS NULL OR valid_end>=@end))::int FROM snap GROUP BY state
        UNION ALL SELECT 'country',"Country",CASE WHEN "Country"='' THEN 'Not recorded' ELSE "Country" END,count(*)::int,count(*) FILTER(WHERE state='Active' AND(valid_end IS NULL OR valid_end>=@end))::int FROM snap GROUP BY "Country"
        UNION ALL SELECT 'state',"State",CASE WHEN "State"='' THEN 'Not recorded' ELSE "State" END,count(*)::int,count(*) FILTER(WHERE state='Active' AND(valid_end IS NULL OR valid_end>=@end))::int FROM snap GROUP BY "State")
        SELECT coalesce(jsonb_agg(to_jsonb(groups) ORDER BY count DESC),'[]')::text AS "Json" FROM groups
        """;
        var renewalsSql="""
        SELECT jsonb_build_object('from',to_char(@dueStart AT TIME ZONE @tz,'YYYY-MM-DD'),'windows',(SELECT jsonb_agg(jsonb_build_object('days',n,'count',(SELECT count(*) FROM snap WHERE state='Active' AND valid_end>@end AND valid_end<=@dueStart+n*interval '1 day'))) FROM unnest(ARRAY[7,30,60,90]) n),
        'overdue',(SELECT count(*) FROM snap WHERE state IN('Active','Expired') AND valid_end<@end),
        'renewed',(SELECT count(*) FROM "MembershipRenewals" r JOIN f ON f."Id"=r."MembershipId" WHERE r."TenantId"=@tenant AND r."At">=@start AND r."At"<@end),
        'converted',(SELECT count(DISTINCT r."MembershipId") FROM "MembershipRenewals" r JOIN f ON f."Id"=r."MembershipId" WHERE r."TenantId"=@tenant AND r."At">=@start AND r."At"<@end AND r."PreviousEnd">=@start AND r."PreviousEnd"<@end),'eligible',(SELECT count(DISTINCT f."Id") FROM f LEFT JOIN "MembershipRenewals" r ON r."MembershipId"=f."Id" AND r."TenantId"=@tenant WHERE f."EndsAt">=@start AND f."EndsAt"<@end OR r."PreviousEnd">=@start AND r."PreviousEnd"<@end),
        'timeline',(SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY t.date),'[]') FROM(SELECT to_char(valid_end AT TIME ZONE @tz,'YYYY-MM-DD') AS date,count(*)::int AS count FROM snap WHERE state='Active' AND valid_end>@end AND valid_end<=@dueStart+interval '90 days' GROUP BY 1 ORDER BY 1) t))::text AS "Json"
        """;
        var orgsSql="""
        , orgtree AS(SELECT o."Id" AS leaf,o."Id" AS node,o."ParentId" FROM "Orgs" o WHERE o."TenantId"=@tenant AND o."Id"=ANY(@allowed) UNION ALL SELECT t.leaf,o."Id",o."ParentId" FROM orgtree t JOIN "Orgs" o ON o."Id"=t."ParentId" AND o."TenantId"=@tenant),
        open_people AS (SELECT DISTINCT t.node,f."PersonId" FROM orgtree t JOIN f ON f."ClubId"=t.leaf JOIN history h ON h."MembershipId"=f."Id" AND h."EffectiveAt"<@start AND(h.next_at IS NULL OR h.next_at>=@start) WHERE f."JoinedAt"<@start AND h."NewStatus"='Active' AND(h."EndsAt" IS NULL OR h."EndsAt">=@start)),
        open_counts AS(SELECT op.node,count(DISTINCT op."PersonId")::int AS opening,count(DISTINCT op."PersonId") FILTER(WHERE EXISTS(SELECT 1 FROM orgtree t JOIN snap s ON s."ClubId"=t.leaf WHERE t.node=op.node AND s."PersonId"=op."PersonId" AND s.state='Active' AND(s.valid_end IS NULL OR s.valid_end>=@end)))::int AS retained FROM open_people op GROUP BY op.node),
        counts AS(SELECT t.node,count(DISTINCT f."PersonId")::int AS count,count(DISTINCT f."PersonId") FILTER(WHERE f.state='Active' AND(f.valid_end IS NULL OR f.valid_end>=@end))::int AS active,count(DISTINCT f."PersonId") FILTER(WHERE f."JoinedAt">=@start)::int AS joined,count(DISTINCT f."PersonId") FILTER(WHERE f."JoinedAt">=@prior AND f."JoinedAt"<@start)::int AS previous FROM orgtree t JOIN snap f ON f."ClubId"=t.leaf GROUP BY t.node),
        unit_renewals AS(SELECT t.node,count(DISTINCT r."MembershipId") FILTER(WHERE r."At">=@start AND r."At"<@end AND r."PreviousEnd">=@start AND r."PreviousEnd"<@end)::int AS renewed,count(DISTINCT f."Id") FILTER(WHERE (f."EndsAt">=@start AND f."EndsAt"<@end) OR(r."PreviousEnd">=@start AND r."PreviousEnd"<@end))::int AS eligible FROM orgtree t JOIN f ON f."ClubId"=t.leaf LEFT JOIN "MembershipRenewals" r ON r."MembershipId"=f."Id" AND r."TenantId"=@tenant GROUP BY t.node),
        rows AS(SELECT o."Id" AS id,o."ParentId" AS "parentId",o."Name" AS name,l."Name" AS level,o."Status" AS status,o."Country" AS country,o."State" AS state,o."Latitude" AS latitude,o."Longitude" AS longitude,coalesce(c.count,0) AS count,coalesce(c.active,0) AS active,coalesce(c.joined,0) AS joined,coalesce(c.previous,0) AS previous,coalesce(oc.opening,0) AS opening,coalesce(oc.retained,0) AS retained,coalesce(ur.renewed,0) AS renewed,coalesce(ur.eligible,0) AS eligible FROM "Orgs" o JOIN "Levels" l ON l."Id"=o."LevelId" LEFT JOIN counts c ON c.node=o."Id" LEFT JOIN open_counts oc ON oc.node=o."Id" LEFT JOIN unit_renewals ur ON ur.node=o."Id" WHERE o."TenantId"=@tenant AND o."Id" IN(SELECT node FROM orgtree) ORDER BY l."Name",o."Name")
        SELECT coalesce(jsonb_agg(to_jsonb(rows)),'[]')::text AS "Json" FROM rows
        """;
        string? cohortsSql=null,flowsSql=null,engagementSql=null,officersSql=null;
        if(mode is "overview" or "cohorts")cohortsSql="""
        , cohorts AS(SELECT date_trunc('month',joined AT TIME ZONE @tz) AS month,count(*)::int AS size FROM people WHERE joined>=@start GROUP BY 1 ORDER BY 1 DESC LIMIT 60),
        targets AS(SELECT c.*,n AS months,(c.month+((n+1)||' months')::interval) AT TIME ZONE @tz AS boundary FROM cohorts c CROSS JOIN unnest(ARRAY[1,3,6,9,12,18,24]) n),
        evaluation AS(SELECT t.month,t.months,p."PersonId",bool_or(h."NewStatus"='Active' AND(h."EndsAt" IS NULL OR h."EndsAt">=t.boundary)) AS active,bool_or(h."NewStatus" IS NOT NULL) AS observed
        FROM targets t JOIN people p ON date_trunc('month',p.joined AT TIME ZONE @tz)=t.month LEFT JOIN f ON f."PersonId"=p."PersonId" AND f."JoinedAt"<t.boundary LEFT JOIN history h ON h."MembershipId"=f."Id" AND h."EffectiveAt"<t.boundary AND(h.next_at IS NULL OR h.next_at>=t.boundary) WHERE t.boundary<=@end GROUP BY t.month,t.months,p."PersonId"),
        cells AS(SELECT to_char(t.month,'YYYY-MM') AS cohort,t.size,t.months,CASE WHEN t.boundary>@end OR count(e."PersonId") FILTER(WHERE e.observed)<t.size THEN NULL ELSE count(e."PersonId") FILTER(WHERE e.active)::int END AS retained,
        CASE WHEN t.boundary>@end THEN NULL ELSE count(e."PersonId") FILTER(WHERE e.observed)::int END AS observed
        FROM targets t LEFT JOIN evaluation e ON e.month=t.month AND e.months=t.months GROUP BY t.month,t.months,t.size,t.boundary)
        SELECT coalesce(jsonb_agg(to_jsonb(cells) ORDER BY cohort DESC,months),'[]')::text AS "Json" FROM cells
        """;
        if(mode is "overview" or "lifecycle")flowsSql="""
        , edges AS(SELECT CASE WHEN h."OldStatus"='' THEN 'Activated' ELSE h."OldStatus" END AS source,h."NewStatus" AS target,count(*)::int AS count FROM "MembershipHistory" h JOIN f ON f."Id"=h."MembershipId" WHERE h."TenantId"=@tenant AND h."EffectiveAt">=@start AND h."EffectiveAt"<@end GROUP BY 1,2)
        SELECT coalesce(jsonb_agg(to_jsonb(edges) ORDER BY count DESC),'[]')::text AS "Json" FROM edges
        """;
        if(grants.Contains("events.reports")&&mode is "overview" or "engagement")engagementSql="""
        , attendance AS(SELECT DISTINCT e."Id" AS event,r."PersonId" AS person,e."OrgId" AS org,e."StartsAt" AS at FROM "Events" e JOIN "EventRegistrations" r ON r."EventId"=e."Id" AND r."TenantId"=@tenant JOIN "EventCheckins" c ON c."RegistrationId"=r."Id" AND c."TenantId"=@tenant AND NOT c."Voided" WHERE e."TenantId"=@tenant AND e."OrgId"=ANY(@allowed) AND e."StartsAt">=@start AND e."StartsAt"<@end AND r."PersonId" IN(SELECT "PersonId" FROM people)),
        timeline AS(SELECT to_char(date_trunc('month',at AT TIME ZONE @tz),'YYYY-MM-DD') AS date,count(*)::int AS attendance,count(DISTINCT person)::int AS participants FROM attendance GROUP BY 1),clubs AS(SELECT org AS id,count(DISTINCT person)::int AS participants,count(DISTINCT event)::int AS events FROM attendance GROUP BY org)
        SELECT jsonb_build_object('participants',(SELECT count(DISTINCT person) FROM attendance),'attendance',(SELECT count(*) FROM attendance),'timeline',(SELECT coalesce(jsonb_agg(to_jsonb(timeline) ORDER BY date),'[]') FROM timeline),'clubs',(SELECT coalesce(jsonb_agg(to_jsonb(clubs)),'[]') FROM clubs))::text AS "Json"
        """;
        var tenureSql="""
        , tenure AS(SELECT CASE WHEN @end-joined<interval '1 month' THEN 'Under 1 month' WHEN @end-joined<interval '3 months' THEN '1-3 months' WHEN @end-joined<interval '6 months' THEN '3-6 months' WHEN @end-joined<interval '1 year' THEN '6-12 months' WHEN @end-joined<interval '3 years' THEN '1-3 years' ELSE '3+ years' END AS name,count(*)::int AS count FROM people GROUP BY 1)
        SELECT coalesce(jsonb_agg(to_jsonb(tenure)),'[]')::text AS "Json" FROM tenure
        """;
        string? categoryRetentionSql=mode=="cohorts"?"""
        , initial AS(SELECT DISTINCT f."PersonId",f."CategoryId" FROM f JOIN history h ON h."MembershipId"=f."Id" AND h."EffectiveAt"<@start AND(h.next_at IS NULL OR h.next_at>=@start) WHERE f."JoinedAt"<@start AND h."NewStatus"='Active' AND(h."EndsAt" IS NULL OR h."EndsAt">=@start)),
        retention AS(SELECT c."Name" AS name,count(*)::int AS opening,count(*) FILTER(WHERE EXISTS(SELECT 1 FROM snap s WHERE s."PersonId"=i."PersonId" AND s."CategoryId"=i."CategoryId" AND s.state='Active' AND(s.valid_end IS NULL OR s.valid_end>=@end)))::int AS retained FROM initial i JOIN "MembershipCategories" c ON c."Id"=i."CategoryId" GROUP BY c."Name")
        SELECT coalesce(jsonb_agg(to_jsonb(retention)),'[]')::text AS "Json" FROM retention
        """:null;
        var singleClub=mode=="network"&&await db.Orgs.CountAsync(o=>o.TenantId==w.TenantId&&allowed.Contains(o.Id)&&db.Levels.Any(l=>l.Id==o.LevelId&&l.IsClub))==1;
        string? networkSql=singleClub?"""
        , network_people AS(SELECT p."Id" AS id,p."DisplayName" AS name,s."ClubId" AS parent,bool_or(s.state='Active' AND(s.valid_end IS NULL OR s.valid_end>=@end)) AS active FROM snap s JOIN "Persons" p ON p."Id"=s."PersonId" GROUP BY p."Id",p."DisplayName",s."ClubId" ORDER BY p."DisplayName",p."Id" LIMIT 50),
        network_committees AS(SELECT c."Id" AS id,c."Name" AS name,c."OrgId" AS parent FROM "Committees" c WHERE @committeesAllowed AND c."TenantId"=@tenant AND c."OrgId"=ANY(@allowed) AND c."Status"='Active' ORDER BY c."Name" LIMIT 20),
        network_officers AS(SELECT a."Id" AS id,p."Title" AS name,a."PersonId" AS parent FROM "OfficerAppointments" a JOIN "OfficerPositions" p ON p."Id"=a."PositionId" WHERE @leadershipAllowed AND a."TenantId"=@tenant AND a."OrgId"=ANY(@allowed) AND a."PersonId" IN(SELECT id FROM network_people) AND a."Status"='Active' AND a."StartsAt"<@end AND a."EndsAt">=@end ORDER BY a."StartsAt" DESC LIMIT 30),
        node_rows AS(SELECT id,name,parent AS "parentId",'Member' AS level,1 AS count,CASE WHEN active THEN 1 ELSE 0 END AS active FROM network_people UNION ALL SELECT id,name,parent,'Committee',0,0 FROM network_committees UNION ALL SELECT id,name,parent,'Officer',1,1 FROM network_officers),
        edge_rows AS(SELECT cm."CommitteeId" AS source,cm."PersonId" AS target FROM "CommitteeMemberships" cm WHERE @committeesAllowed AND cm."TenantId"=@tenant AND cm."PersonId" IN(SELECT id FROM network_people) AND cm."CommitteeId" IN(SELECT id FROM network_committees) AND cm."Status"='Active' AND cm."StartsAt"<@end AND cm."EndsAt">=@end LIMIT 100)
        SELECT jsonb_build_object('nodes',(SELECT coalesce(jsonb_agg(to_jsonb(node_rows)),'[]') FROM node_rows),'edges',(SELECT coalesce(jsonb_agg(to_jsonb(edge_rows)),'[]') FROM edge_rows),'memberLimit',50)::text AS "Json"
        """:null;
        // One statement materializes the authorized cohort and historical snapshot once for all datasets.
        var datasets=new (string Name,string? Sql)[]{("summary",summarySql),("trend",trendSql),("distributions",distributionsSql),("renewals",renewalsSql),("orgs",orgsSql),("cohorts",cohortsSql),("flows",flowsSql),("engagement",engagementSql),("officers",officersSql),("tenure",tenureSql),("categoryRetention",categoryRetentionSql),("network",networkSql)};
        string Nested(string sql)=>sql.TrimStart().StartsWith(',')?"WITH RECURSIVE "+sql.TrimStart()[1..]:sql;
        var values=cached==null?new Dictionary<string,JsonElement>():new Dictionary<string,JsonElement>(cached.Values);
        var missing=datasets.Where(x=>x.Sql!=null&&!values.ContainsKey(x.Name)).ToArray();
        if(missing.Length>0){
            var combined=" SELECT jsonb_build_object("+string.Join(",",missing.Select(x=>"'"+x.Name+"',(SELECT nested.\"Json\"::jsonb FROM ("+Nested(x.Sql!)+") nested)"))+")::text AS \"Json\"";
            await using var analyticsTx=await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlRawAsync("SET LOCAL work_mem='32MB'");
            var fresh=await Json(combined);await analyticsTx.CommitAsync();
            foreach(var property in fresh.EnumerateObject())values[property.Name]=property.Value.Clone();
            Cache.Set(cacheKey,new CachedAnalytics(values,end,cached?.Expires??DateTimeOffset.UtcNow.AddSeconds(20)),new MemoryCacheEntryOptions{AbsoluteExpiration=cached?.Expires??DateTimeOffset.UtcNow.AddSeconds(20),Size=values.Sum(x=>x.Value.GetRawText().Length*4L)});
        }
        var dataset=JsonSerializer.SerializeToElement(datasets.ToDictionary(x=>x.Name,x=>x.Sql==null?JsonSerializer.SerializeToElement<object?>(null):values[x.Name]));
        var summary=dataset.GetProperty("summary");var trend=dataset.GetProperty("trend");var distributions=dataset.GetProperty("distributions");var renewals=dataset.GetProperty("renewals");var orgs=dataset.GetProperty("orgs");var cohorts=dataset.GetProperty("cohorts");var flows=dataset.GetProperty("flows");var engagement=dataset.GetProperty("engagement");var officers=dataset.GetProperty("officers");
        if(grants.Contains("leadership.view") && mode is "overview" or "organization" or "actions")
        {
            var currentOffices=GovernanceService.Current(db).Where(a=>a.TenantId==w.TenantId);
            var requiredOffices=db.OfficerPositions.Where(p=>p.TenantId==w.TenantId&&p.Active&&p.Mandatory);
            officers=JsonSerializer.SerializeToElement(await db.Orgs.Where(o=>o.TenantId==w.TenantId&&o.Status=="Active"&&allowed.Contains(o.Id)).Select(o=>new{id=o.Id,required=requiredOffices.Count(p=>p.LevelId==o.LevelId),filled=requiredOffices.Count(p=>p.LevelId==o.LevelId&&currentOffices.Any(a=>a.OrgId==o.Id&&a.PositionId==p.Id))}).ToListAsync());
        }
        return new{workspace=mode,period,bucket,from,to,timeZone=tz.Id,asOf=end.AddTicks(-1),summary,trend,distributions,renewals,network=dataset.GetProperty("network"),tenure=dataset.GetProperty("tenure"),categoryRetention=dataset.GetProperty("categoryRetention"),organizations=orgs,cohorts,flows,engagement,officers,
            capabilities=new{export=grants.Contains("members.export"),events=grants.Contains("events.reports"),governance=grants.Contains("leadership.view")},elapsedMs=watch.ElapsedMilliseconds,cached=cached!=null,cacheSeconds=20,
            definitions=new[]{"Leadership coverage is current effective coverage for active units, using the same eligibility rules as Leadership. It is not reconstructed historical coverage.","People = distinct person IDs within filtered authorized affiliations, joined before the period end. Affiliations = membership records, including transfer history; never add person counts across overlapping units.","Stock metrics ending today use live membership status and validity. Earlier end dates use recorded history; missing historical snapshots remain Unknown. Pending applications are the current scoped backlog (Submitted, Under Review, Changes Requested), regardless of application creation date or chart period; drafts and terminal outcomes are excluded.","New = first join in this filtered scope and category; transfer-in and returning affiliations may affect scope-first registrations. Prior comparison uses an equal number of local calendar days.","Retention = opening active people who remain active at period end / opening active people. No opening cohort means unavailable, not 100%.","Cohort N-month retention is measured at the end of the Nth following calendar month. Future observation boundaries are null, not churn. Cohorts use first scoped join dates.","Geography uses the club's recorded country/state, not a member's private home address. Map pins require stored coordinates. Unknown places are counted explicitly.","Lifecycle edges count actual recorded status transitions; people may contribute repeatedly and to multiple edges. This is an event flow, not a mutually exclusive person funnel.","Renewal windows count active affiliations whose membership validity ends after the as-of instant and within N days, inclusive. These are membership expiries, not unpaid financial dues. Renewed counts renewal events; eligible counts distinct affiliations with expiry or previous expiry in the period.","Engagement counts distinct checked-in person/event pairs at scoped events. Session duplicates and voided check-ins are excluded. Attendance inactivity is distinct from membership status.","Health scores are exploratory weighted composites of available growth, retention, engagement, renewal and officer data. Missing dimensions are excluded, with coverage shown separately. Forecasts require at least six completed monthly observations and are labelled projections."}};
    }
}
public class AnalyticsJson{public string Json{get;set;}="{}";}
public class AnalyticsOrg{public Guid Id{get;set;}public Guid? ParentId{get;set;}public Guid LevelId{get;set;}public string Name{get;set;}="";public string Status{get;set;}="";public string Country{get;set;}="";public string State{get;set;}="";public double? Latitude{get;set;}public double? Longitude{get;set;}}
