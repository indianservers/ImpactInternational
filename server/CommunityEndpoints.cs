using System.Globalization;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
namespace Clubs;

public record CommunityPostInput(Guid OrgId, string Title, string Body, string Category, DateOnly ActivityOn,
    int Participants = 0, decimal VolunteerHours = 0, string Location = "", string LinkUrl = "", bool Pinned = false, string RequestKey = "", long Version = 1, string? Visibility = null, Guid? SourceEventId = null, Guid[]? ParticipantIds = null, double? Latitude = null, double? Longitude = null);
public record CommunityCommentInput(string Body);
public record CommunityReactionInput(bool Liked);
public record CommunityArchiveInput(long Version, string Reason);

public static class CommunityEndpoints
{
    public static readonly string[] Categories = ["Club activity", "Training", "Business", "Community service", "Leadership", "Public speaking", "Social", "Announcement"];
    public sealed record Scope(Assignment W, Guid[] ReadIds, Guid[] ReportIds, Guid[] AudienceIds, bool Platform, bool Manage, bool Reports);
    public static async Task<Scope> GetScope(Access a)
    {
        var w = await NetworkingEndpoints.Workspace(a); return await ForWorkspace(a,w);
    }
    public static async Task<Scope> ForWorkspace(Access a,Assignment w)
    {
        var grants = await a.Grants(w);
        var platform = await a.Db.Users.Where(u=>u.Id==w.UserId).Select(u=>u.PlatformAdmin).SingleAsync();
        var descendants = await a.VisibleIds(w).ToArrayAsync();
        var parents = await a.Db.Database.SqlQuery<Guid>($"WITH RECURSIVE parents AS(SELECT \"Id\",\"ParentId\" FROM \"Orgs\" WHERE \"TenantId\"={w.TenantId} AND \"Id\"={w.OrgId} UNION ALL SELECT o.\"Id\",o.\"ParentId\" FROM \"Orgs\" o JOIN parents p ON o.\"Id\"=p.\"ParentId\" WHERE o.\"TenantId\"={w.TenantId}) SELECT \"Id\" AS \"Value\" FROM parents").ToArrayAsync();
        var role = await a.Db.Roles.Where(r => r.Id == w.RoleId).Select(r => r.Name).SingleAsync();
        var president = LeaderCommunications.PresidentLevels.ContainsKey(role);
        return new(w, descendants.Concat(parents).Distinct().ToArray(), descendants, parents, platform, president || grants.Contains("communications.manage"),
            platform || president || grants.Any(p => p is "communications.manage" or "governance.reports" or "reports.view"));
    }
    static async Task<DateOnly> Today(Access a) {
        var user = await a.User();
        try { return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(user.TimeZone)).DateTime); }
        catch (TimeZoneNotFoundException) { return DateOnly.FromDateTime(DateTime.UtcNow); }
    }
    public static bool CanPublish(Scope s, Guid org) => s.Manage && (s.Platform || org == s.W.OrgId);
    public static bool CanModerate(Scope s, Guid org) => s.Manage && s.ReportIds.Contains(org);
    public static IQueryable<CommunityPost> Posts(Db db, Scope s) => db.CommunityPosts.Where(p => p.TenantId == s.W.TenantId && p.Status == "Published"
        && (p.Visibility == "Public" || p.Visibility == "Hierarchy" && s.ReadIds.Contains(p.OrgId)
            || p.AudienceOrgId != null && s.AudienceIds.Contains(p.AudienceOrgId.Value)
            || s.Reports && s.ReportIds.Contains(p.OrgId)));
    public static async Task SetVisibility(Access a, CommunityPost p, string? visibility)
    {
        if (visibility == "Hierarchy" && p.Visibility == "Hierarchy") return;
        if (visibility == null) return; // Compatibility with old clients: don't widen or clear an existing audience.
        Check(new[] { "Club", "Region", "National", "Public" }.Contains(visibility), "Choose Club, Region, National or Public visibility.");
        if (visibility == "Public") { p.Visibility = visibility; p.AudienceOrgId = null; return; }
        var path = await a.Db.Database.SqlQuery<Guid>($"""
            WITH RECURSIVE parents AS (
              SELECT "Id","ParentId" FROM "Orgs" WHERE "TenantId"={p.TenantId} AND "Id"={p.OrgId}
              UNION ALL SELECT o."Id",o."ParentId" FROM "Orgs" o JOIN parents parent ON o."Id"=parent."ParentId" WHERE o."TenantId"={p.TenantId}
            ) SELECT "Id" AS "Value" FROM parents
            """).ToArrayAsync();
        var audience = await a.Db.Orgs.Where(o => o.TenantId == p.TenantId && path.Contains(o.Id) && a.Db.Levels.Any(l => l.Id == o.LevelId && l.Name == visibility)).Select(o => (Guid?)o.Id).SingleOrDefaultAsync();
        Check(audience != null, "This unit has no " + visibility + " audience. Choose its own level or a broader audience.");
        p.Visibility = visibility; p.AudienceOrgId = audience;
    }
    public static async Task<CommunityPost> Post(Guid id, Db db, Scope s) => await Posts(db, s).SingleOrDefaultAsync(p => p.Id == id) ?? throw new ApiError(404, "Community update is outside your spaces or unavailable.");
    static void Validate(CommunityPostInput i)
    {
        Check(i.Title?.Trim().Length is >= 3 and <= 180 && i.Body?.Trim().Length is >= 5 and <= 10000 && Categories.Contains(i.Category)
            && i.ActivityOn.Year is >= 1900 and <= 2200 && i.Participants is >= 0 and <= 1000000 && i.VolunteerHours is >= 0 and <= 1000000
            && decimal.Round(i.VolunteerHours, 2) == i.VolunteerHours && i.Location is { Length: <= 200 } && i.LinkUrl is { Length: <= 1000 }, "Supply a title, update, supported category and valid activity details.");
        Check((i.Latitude == null) == (i.Longitude == null) && (i.Latitude == null || double.IsFinite(i.Latitude.Value) && i.Latitude is >= -90 and <= 90 && double.IsFinite(i.Longitude!.Value) && i.Longitude is >= -180 and <= 180), "Choose valid map coordinates.");
        Check(i.LinkUrl == "" || Uri.TryCreate(i.LinkUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && uri.UserInfo == "", "Use an HTTP or HTTPS link.");
    }
    public static IQueryable<object> Rows(IQueryable<CommunityPost> q, Db db, Scope s) => q.Select(p => (object)new {
        p.Id, p.OrgId, p.Visibility, p.Status, p.SourceEventId, p.ReviewNote, p.Latitude, p.Longitude, p.Title, p.Body, p.Category, p.ActivityOn, p.Participants, p.VolunteerHours, p.Location, p.LinkUrl, p.Pinned, p.CreatedAt, p.UpdatedAt, p.Version,
        participantIds = db.ActivityParticipations.Where(t => t.PostId == p.Id && t.Consent != "Removed" && s.Manage && s.ReportIds.Contains(p.OrgId)).Select(t => t.PersonId).ToArray(),
        taggedMembers = db.ActivityParticipations.Where(t => t.PostId == p.Id && t.Consent == "Accepted" && db.Persons.Any(person => person.Id == t.PersonId && person.Privacy.DirectoryVisible) && (p.Visibility != "Public" || t.PublicName)).Select(t => new { t.PersonId, name = db.Persons.Where(person => person.Id == t.PersonId).Select(person => person.DisplayName).First(), t.Role, t.Verified }).ToArray(),
        media = db.CommunityMedia.Where(m => m.PostId == p.Id && m.Status == "Ready").OrderBy(m => m.CreatedAt).Select(m => new { m.Id, m.FileName, m.MimeType, m.Size }).ToArray(),
        author = db.Users.Where(u => u.Id == p.AuthorId).Select(u => u.Name).First(), organization = db.Orgs.Where(o => o.Id == p.OrgId).Select(o => o.Name).First(),
        level = db.Orgs.Where(o => o.Id == p.OrgId).Select(o => db.Levels.Where(l => l.Id == o.LevelId).Select(l => l.Name).First()).First(),
        likes = db.CommunityReactions.Count(r => r.PostId == p.Id), liked = db.CommunityReactions.Any(r => r.PostId == p.Id && r.UserId == s.W.UserId),
        comments = db.CommunityComments.Count(c => c.PostId == p.Id && !c.Hidden), canManage = s.Manage && s.ReportIds.Contains(p.OrgId) });

    public static void MapCommunity(this RouteGroupBuilder api)
    {
        api.MapGet("/community/posts/{id:guid}",async(Guid id,Access a)=>{var s=await GetScope(a);var p=await Rows(Posts(a.Db,s).Where(p=>p.Id==id),a.Db,s).SingleOrDefaultAsync();Check(p!=null,"Activity unavailable.",404);return p;});
        api.MapGet("/community/spaces", async (Access a) => {
            var s = await GetScope(a); var units = await a.Db.Orgs.Where(o => o.TenantId == s.W.TenantId && (s.ReadIds.Contains(o.Id) || Posts(a.Db, s).Any(p => p.OrgId == o.Id)) && o.Status == "Active")
                .OrderBy(o => o.Name).Select(o => new { o.Id, o.Name, o.ParentId, level = a.Db.Levels.Where(l => l.Id == o.LevelId).Select(l => l.Name).First(),
                    canPublish = s.Manage && (s.Platform || o.Id == s.W.OrgId), canReport = s.Reports && s.ReportIds.Contains(o.Id) }).ToListAsync();
            return new { today = await Today(a), workspaceOrgId = s.W.OrgId, canReport = s.Reports, categories = Categories, visibilityOptions = new[] { "Club", "Region", "National", "Public" }, spaces = units };
        });
        api.MapGet("/community/posts", async (Access a, Guid? orgId = null, string search = "", string category = "", int page = 1) => {
            var s = await GetScope(a); Check(page is >= 1 and <= 10000 && search.Length <= 100 && (category == "" || Categories.Contains(category)), "Invalid community filters.");
            if (orgId != null) Check(s.ReadIds.Contains(orgId.Value) || await Posts(a.Db, s).AnyAsync(p => p.OrgId == orgId), "Community space outside your role.", 404);
            var q = Posts(a.Db, s).Union(CommunityGrowthService.Managed(a.Db, s).Where(p => p.Status == "Pending" || p.Status == "Rejected")); if (orgId != null) q = q.Where(p => p.OrgId == orgId);
            if (category != "") q = q.Where(p => p.Category == category);
            if (search.Trim() != "") { var term = search.Trim().ToLower(); q = q.Where(p => p.Title.ToLower().Contains(term) || p.Body.ToLower().Contains(term)); }
            return new { total = await q.CountAsync(), page, pageSize = 15, items = await Rows(q.OrderByDescending(p => p.Pinned).ThenByDescending(p => p.CreatedAt).ThenBy(p => p.Id).Skip((page - 1) * 15).Take(15), a.Db, s).ToListAsync() };
        });
        api.MapPost("/community/posts", async (CommunityPostInput input, Access a) => {
            var s = await GetScope(a); Validate(input); Check(input.RequestKey is { Length: >= 8 and <= 100 }, "An update submission key is required.");
            Check(CanPublish(s, input.OrgId), "Only the respective unit's authorized administrator can publish in this space.", 403);
            await using var tx = await a.Mutation(s.W);
            Check(await a.Db.Orgs.AnyAsync(o => o.Id == input.OrgId && o.TenantId == s.W.TenantId && o.Status == "Active"), "Choose an active organization.");
            var hash = Hash(GovernanceService.Pack(input with { Version = 1 }));
            var existing = await a.Db.CommunityPosts.SingleOrDefaultAsync(p => p.TenantId == s.W.TenantId && p.AuthorId == s.W.UserId && p.RequestKey == input.RequestKey);
            if (existing != null) { Check(existing.RequestHash == hash, "This submission key belongs to another update.", 409); return Results.Ok(new { existing.Id, existing.Version }); }
            var post = new CommunityPost { TenantId = s.W.TenantId, OrgId = input.OrgId, AuthorId = s.W.UserId, Title = input.Title.Trim(), Body = input.Body.Trim(),
                Category = input.Category, ActivityOn = input.ActivityOn, Participants = input.Participants, VolunteerHours = input.VolunteerHours, Location = input.Location.Trim(),
                SourceEventId = input.SourceEventId, Latitude = input.Latitude, Longitude = input.Longitude, LinkUrl = input.LinkUrl.Trim(), Pinned = input.Pinned, RequestKey = input.RequestKey, RequestHash = hash };
            if (input.SourceEventId != null) {
                await CommunityGrowthService.SourceEvent(input.SourceEventId.Value, a, s, input.OrgId);
                Check(!await a.Db.CommunityPosts.AnyAsync(p => p.TenantId == s.W.TenantId && p.SourceEventId == input.SourceEventId), "This event already has an activity update.", 409);
            }
            await SetVisibility(a, post, input.Visibility);
            await CommunityGrowthService.ApplyReview(a, s, post);
            a.Db.CommunityPosts.Add(post); await CommunityGrowthService.SetTags(a, s, post, input.ParticipantIds); await a.Log(s.W, "community.post.published", post.Id.ToString(), GovernanceService.Pack(new { post.OrgId, post.Category }));
            await tx.CommitAsync(); return Results.Ok(new { post.Id, post.Version, post.Status });
        });
        api.MapPut("/community/posts/{id:guid}", async (Guid id, CommunityPostInput input, Access a) => {
            var s = await GetScope(a); Validate(input); await using var tx = await a.Mutation(s.W); var p = await CommunityGrowthService.Editable(id, a, s);
            Check(CanModerate(s, p.OrgId), "An authorized administrator is required to edit this unit's update.", 403);
            Check(p.Version == input.Version, "Update changed. Refresh before saving.", 409); Check(p.OrgId == input.OrgId, "An update cannot be moved to another unit.");
            Check(input.SourceEventId == null || p.SourceEventId == input.SourceEventId, "The source event cannot be changed.");
            await SetVisibility(a, p, input.Visibility);
            await CommunityGrowthService.ApplyReview(a, s, p);
            await CommunityGrowthService.SetTags(a, s, p, input.ParticipantIds);
            p.Latitude = input.Latitude; p.Longitude = input.Longitude;
            p.Title = input.Title.Trim(); p.Body = input.Body.Trim(); p.Category = input.Category; p.ActivityOn = input.ActivityOn; p.Participants = input.Participants;
            p.VolunteerHours = input.VolunteerHours; p.Location = input.Location.Trim(); p.LinkUrl = input.LinkUrl.Trim(); p.Pinned = input.Pinned; p.Version++; p.UpdatedAt = DateTimeOffset.UtcNow;
            await a.Log(s.W, "community.post.updated", id.ToString(), GovernanceService.Pack(new { p.OrgId, p.Version })); await tx.CommitAsync(); return Results.Ok(new { p.Id, p.Version, p.Status });
        });
        api.MapPost("/community/posts/{id:guid}/archive", async (Guid id, CommunityArchiveInput input, Access a) => {
            var s = await GetScope(a); await using var tx = await a.Mutation(s.W); var p = await CommunityGrowthService.Editable(id, a, s);
            Check(CanModerate(s, p.OrgId), "Administrator authority is required to archive this update.", 403);
            Check(p.Version == input.Version, "Update changed. Refresh.", 409); Check(input.Reason?.Trim().Length is >= 5 and <= 500, "Explain why the update is being archived.");
            p.Status = "Archived"; p.Version++; p.UpdatedAt = DateTimeOffset.UtcNow;
            await a.Log(s.W, "community.post.archived", id.ToString(), input.Reason.Trim()); await tx.CommitAsync(); return Results.NoContent();
        });
        api.MapGet("/community/posts/{id:guid}/comments", async (Guid id, Access a, int page = 1) => {
            var s = await GetScope(a); var post = await Post(id, a.Db, s); Check(page is >= 1 and <= 10000, "Invalid page.");
            var q = a.Db.CommunityComments.Where(c => c.PostId == id && !c.Hidden);
            return new { total = await q.CountAsync(), page, pageSize = 30, items = await q.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id).Skip((page - 1) * 30).Take(30)
                .Select(c => new { c.Id, c.Body, c.CreatedAt, canHide = c.AuthorId == s.W.UserId || s.Manage && s.ReportIds.Contains(post.OrgId), author = a.Db.Users.Where(u => u.Id == c.AuthorId).Select(u => u.Name).First() }).ToListAsync() };
        });
        api.MapPost("/community/posts/{id:guid}/comments", async (Guid id, CommunityCommentInput input, Access a) => {
            var s = await GetScope(a); Check(input.Body?.Trim().Length is >= 2 and <= 2000, "Comments require 2–2,000 characters.");
            await using var tx = await a.Mutation(s.W); var p = await Post(id, a.Db, s);
            Check(await a.Db.CommunityComments.CountAsync(c => c.AuthorId == s.W.UserId && c.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-1)) < 10, "Please wait before adding more comments.", 429);
            var c = new CommunityComment { TenantId = s.W.TenantId, PostId = p.Id, AuthorId = s.W.UserId, Body = input.Body!.Trim() }; a.Db.CommunityComments.Add(c);
            await a.Log(s.W, "community.comment.added", c.Id.ToString(), p.Id.ToString()); await tx.CommitAsync(); return Results.Ok(new { c.Id });
        });
        api.MapPost("/community/comments/{id:guid}/hide", async (Guid id, Access a) => {
            var s = await GetScope(a); await using var tx = await a.Mutation(s.W);
            var comment = await a.Db.CommunityComments.SingleOrDefaultAsync(c => c.Id == id && c.TenantId == s.W.TenantId);
            Check(comment != null, "Comment unavailable.", 404); var post = await Post(comment.PostId, a.Db, s);
            Check(comment.AuthorId == s.W.UserId || CanModerate(s, post.OrgId), "Only the author or an authorized moderator can hide this comment.", 403);
            comment.Hidden = true; await a.Log(s.W, "community.comment.hidden", id.ToString(), post.Id.ToString()); await tx.CommitAsync(); return Results.NoContent();
        });
        api.MapPut("/community/posts/{id:guid}/reaction", async (Guid id, CommunityReactionInput input, Access a) => {
            var s = await GetScope(a); await using var tx = await a.Mutation(s.W); await Post(id, a.Db, s);
            var reaction = await a.Db.CommunityReactions.SingleOrDefaultAsync(r => r.PostId == id && r.UserId == s.W.UserId);
            if (input.Liked && reaction == null) a.Db.CommunityReactions.Add(new() { TenantId = s.W.TenantId, PostId = id, UserId = s.W.UserId });
            if (!input.Liked && reaction != null) a.Db.CommunityReactions.Remove(reaction);
            await a.Db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(new { liked = input.Liked });
        });
        api.MapGet("/community/report", Report);
        api.MapGet("/public/activities/{tenantId:guid}", async (Guid tenantId, Access a, int page = 1, Guid? postId = null) => {
            Check(a.BoundTenant == null || a.BoundTenant == tenantId, "Activities unavailable on this domain.", 404);
            Check(page is >= 1 and <= 10000, "Invalid page.");
            var q = a.Db.CommunityPosts.Where(p => p.TenantId == tenantId && p.Status == "Published" && p.Visibility == "Public");
            if (postId != null) q = q.Where(p => p.Id == postId);
            return new { page, pageSize = 15, total = await q.CountAsync(), items = await q.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id).Skip((page-1)*15).Take(15).Select(p => new {
                p.Id, p.Title, p.Body, p.Category, p.ActivityOn, p.Participants, p.VolunteerHours, p.Location, p.CreatedAt,
                taggedMembers = a.Db.ActivityParticipations.Where(t=>t.PostId==p.Id&&t.Consent=="Accepted"&&t.PublicName&&a.Db.Persons.Any(person=>person.Id==t.PersonId&&person.Privacy.DirectoryVisible)).Select(t=>new{name=a.Db.Persons.Where(person=>person.Id==t.PersonId).Select(person=>person.DisplayName).First()}).ToArray(),
                organization = a.Db.Orgs.Where(o => o.Id == p.OrgId).Select(o => o.Name).First(),
                media = a.Db.CommunityMedia.Where(m => m.PostId == p.Id && m.Status == "Ready").OrderBy(m => m.CreatedAt).Select(m => new { m.Id, m.FileName, m.MimeType, m.Size }).ToArray()
            }).ToListAsync() };
        });
    }

    static async Task<IResult> Report(Access a, Guid? orgId = null, DateOnly? from = null, DateOnly? to = null, string format = "")
    {
        var s = await GetScope(a); Check(s.Reports, "Activity reports require an authorized reporting role.", 403);
        var root = orgId ?? s.W.OrgId; Check(s.ReportIds.Contains(root), "Report unit is outside your organizational responsibility.", 404);
        var today = await Today(a); var start = from ?? today.AddDays(-30); var end = to ?? today;
        Check(start <= end && end.DayNumber - start.DayNumber <= 366 && format is "" or "csv", "Choose a reporting period of up to 366 days.");
        var units = a.Tree(s.W.TenantId, root).Select(o => o.Id);
        var q = a.Db.CommunityPosts.Where(p => p.TenantId == s.W.TenantId && p.Status == "Published" && units.Contains(p.OrgId) && p.ActivityOn >= start && p.ActivityOn <= end);
        var byUnit = await q.GroupBy(p => p.OrgId).Select(g => new { orgId = g.Key, updates = g.Count(), participants = g.Sum(p => p.Participants), volunteerHours = g.Sum(p => p.VolunteerHours), latest = g.Max(p => p.ActivityOn) }).ToListAsync();
        var orgs = await a.Db.Orgs.Where(o => o.TenantId == s.W.TenantId && units.Contains(o.Id)).Select(o => new { o.Id, o.Name, level = a.Db.Levels.Where(l => l.Id == o.LevelId).Select(l => l.Name).First() }).ToListAsync();
        var rows = orgs.Select(o => new { orgId = o.Id, organization = o.Name, o.level, updates = byUnit.FirstOrDefault(r => r.orgId == o.Id)?.updates ?? 0,
            participants = byUnit.FirstOrDefault(r => r.orgId == o.Id)?.participants ?? 0, volunteerHours = byUnit.FirstOrDefault(r => r.orgId == o.Id)?.volunteerHours ?? 0,
            latest = byUnit.FirstOrDefault(r => r.orgId == o.Id)?.latest }).OrderByDescending(r => r.updates).ThenBy(r => r.organization).ToList();
        if (format == "csv") { await a.Log(s.W, "community.report.export", root.ToString(), start + " / " + end); return BulkImportEndpoints.Csv(["Organization", "Level", "Updates", "Recorded participants", "Volunteer hours", "Latest activity"], rows.Select(r => new[] { r.organization, r.level, r.updates.ToString(), r.participants.ToString(), r.volunteerHours.ToString(CultureInfo.InvariantCulture), r.latest?.ToString("yyyy-MM-dd") ?? "" }), "impact-activity-report.csv"); }
        return Results.Ok(new { from = start, to = end, rootId = root, root = orgs.Single(o => o.Id == root).Name,
            summary = new { updates = byUnit.Sum(r => r.updates), reportingUnits = byUnit.Count, totalUnits = rows.Count, participants = byUnit.Sum(r => r.participants), volunteerHours = byUnit.Sum(r => r.volunteerHours) },
            units = rows, categories = await q.GroupBy(p => p.Category).Select(g => new { category = g.Key, updates = g.Count() }).ToListAsync(),
            recent = await Rows(q.OrderByDescending(p => p.ActivityOn).ThenByDescending(p => p.CreatedAt).Take(100), a.Db, s).ToListAsync() });
    }
}
