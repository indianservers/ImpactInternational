using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
namespace Clubs;

public static class IciWorkflow
{
    public static string[] SubmitKinds(string role, bool admin) => admin ? IciPolicy.RequestKinds : role switch
    {
        "Club President" => ["TitleClearance", "Subscription", "EventBid", "EventReport", "FinancialAid"],
        "Regional President" => ["EventBid", "EventReport", "FinancialAid"],
        "National President" => ["ClubCreation", "ClubMapping", "RegionMapping", "TeamHandover"],
        "Executive Board" => ["ClubCreation", "ClubMapping", "RegionMapping", "TeamHandover", "LegalAction"],
        _ => []
    };
    public static string[] Route(string kind, string ownerLevel) => kind switch
    {
        "Subscription" => ["Administration"],
        "TitleClearance" => ["National President|Executive Board"],
        "EventBid" => [ownerLevel == "Club" ? "National President" : "Executive Board"],
        "EventReport" => [ownerLevel == "Club" ? "Regional President" : "Executive Board"],
        "FinancialAid" => ["Executive Board"],
        "TeamHandover" => ["Executive Board"],
        "LegalAction" => ["Executive Board"],
        _ => ["National President|Executive Board"]
    };
    public static async Task<MembershipWorkflow?> EventWorkflow(GovernanceService s, GovernanceRequest request)
    {
        if (request.Kind != "EventPublication") return null;
        var level = await (from o in s.Db.Orgs join l in s.Db.Levels on o.LevelId equals l.Id where o.Id == request.OrgId && o.TenantId == request.TenantId select l.Name).SingleAsync();
        if (level is not "Club" and not "Region") return null;
        var kind = "IciEvent" + level;
        var workflow = await s.Db.MembershipWorkflows.SingleOrDefaultAsync(w => w.TenantId == request.TenantId && w.Kind == kind && w.Active);
        if (workflow != null) return workflow;
        workflow = new MembershipWorkflow { TenantId = request.TenantId, Kind = kind };
        var route = level == "Club" ? new[] { "Regional President", "National Director" } : new[] { "National Director", "Executive Board" };
        for (var index = 0; index < route.Length; index++)
        {
            var name = route[index];
            var roles = await s.Db.Roles.Where(r => r.TenantId == request.TenantId && (r.Name == name || r.Name == "Association Super Admin" || r.Name == "Main Admin")).Select(r => r.Id).ToListAsync();
            Check(roles.Count > 0, "Configure the " + name + " review role first.", 409);
            workflow.Stages.Add(new MembershipApprovalStage { TenantId = request.TenantId, Name = name, Sequence = index + 1, RequiredApprovals = 1, AllowDelegation = false, Roles = roles.Select(id => new MembershipStageRole { TenantId = request.TenantId, RoleId = id }).ToList() });
        }
        s.Db.MembershipWorkflows.Add(workflow); await s.Db.SaveChangesAsync(); return workflow;
    }
    public static async Task CheckGovernanceReview(Db db, Assignment w, GovernanceRequest request)
    {
        var role = await db.Roles.Where(r => r.Id == w.RoleId).Select(r => r.Name).SingleAsync();
        var sourceOfficial = IciPolicy.IsRole(role) && !await db.Users.AnyAsync(u => u.Id == w.UserId && u.PlatformAdmin);
        if (sourceOfficial)
        {
            Check(request.Kind == "EventPublication", "Use your ICI role workspace for this decision.", 403);
            var level = await (from o in db.Orgs join l in db.Levels on o.LevelId equals l.Id where o.Id == request.OrgId select l.Name).SingleAsync();
            var reviewer = (level, request.CurrentSequence) switch
            {
                ("Club", 1) => "Regional President",
                ("Club", 2) => "National Director",
                ("Region", 1) => "National Director",
                ("Region", 2) => "Executive Board",
                _ => ""
            };
            Check(role == reviewer, "This role cannot review the current event stage.", 403);
        }
        if (request.Kind != "EventPublication") return;
        var a = new Access(db, new HttpContextAccessor());
        // Access passed to this gate is only used for assignment-scoped query composition.
        var ids = IciPolicy.Events(a, w, db.Events).Select(e => e.Id);
        var reference = GovernanceService.Unpack<EventReference>(request.Payload);
        Check(await ids.ContainsAsync(reference.EventId), "Event is outside your assigned region or portfolio.", 403);
        Check(!await db.MembershipApprovals.AnyAsync(d => d.GovernanceRequestId == request.Id && d.Revision == request.Revision && d.ActorId == w.UserId && d.Decision == "Approve"), "Separate reviewers must complete each event approval stage.", 403);
    }
    public static async Task CheckTransfer(Access a, Assignment w, MembershipTransfer transfer, Guid sourceClub)
    {
        if (await IciPolicy.Administrator(a, w)) return;
        var role = await IciPolicy.Role(a, w);
        var required = transfer.Status switch { "PendingSource" or "PendingDestination" => "Regional President", "PendingVicePresidentSource" or "PendingVicePresidentDestination" => "National Vice President", "PendingAssociation" => "National President|Executive Board", _ => "" };
        Check(required.Split('|').Contains(role), "This role cannot review the current transfer stage.", 403);
        var org = transfer.Status is "PendingDestination" or "PendingVicePresidentDestination" ? transfer.DestinationClubId : sourceClub;
        Check(await a.Visible(w).AnyAsync(o => o.Id == org), "This transfer stage is outside your assigned coverage.", 403);
        if (transfer.Status == "PendingAssociation") Check(await a.Visible(w).AnyAsync(o => o.Id == transfer.DestinationClubId), "Both clubs must be within final review authority.", 403);
    }
    public static async Task<Assignment> Assign(Access a, IciAssignmentInput input)
    {
        var acting = await a.Workspace("roles.assign");
        Check(await IciPolicy.Administrator(a, acting), "Use the reviewed team-handover workflow for leadership changes.", 403);
        return await InstallAssignment(a, acting, input);
    }
    public static async Task<Assignment> InstallAssignment(Access a, Assignment acting, IciAssignmentInput input)
    {
        Check(input.Email.Length <= 200, "Invalid account email.");
        var role = await a.Db.Roles.SingleOrDefaultAsync(r => r.Id == input.RoleId && r.TenantId == acting.TenantId);
        Check(role != null && IciPolicy.IsRole(role.Name), "Choose an ICI role.");
        Check(await a.Visible(acting).AnyAsync(o => o.Id == input.OrgId), "Role assignment is outside your authority.", 403);
        var user = await a.Db.Users.SingleOrDefaultAsync(u => u.Email == input.Email.Trim().ToLowerInvariant() && u.Verified);
        Check(user != null, "A verified account is required.");
        Check(await a.Db.Assignments.AnyAsync(w => w.UserId == user!.Id && w.TenantId == acting.TenantId) || await a.Db.Memberships.AnyAsync(m => m.TenantId == acting.TenantId && m.Person.UserId == user!.Id), "Account is outside this association.", 403);
        var regions = input.RegionIds ?? [];
        await IciPolicy.ValidateAssignment(a, acting.TenantId, input.OrgId, role!.Name, input.Portfolio, input.Slot, regions);
        Check(!await a.Db.Assignments.AnyAsync(w => w.UserId == user!.Id && w.OrgId == input.OrgId && w.RoleId == role.Id && w.IciPortfolio == input.Portfolio && w.IciSlot == input.Slot), "This assignment already exists.", 409);
        var result = new Assignment { TenantId = acting.TenantId, OrgId = input.OrgId, RoleId = role.Id, UserId = user!.Id, IciPortfolio = input.Portfolio, IciSlot = input.Slot };
        a.Db.Assignments.Add(result);
        foreach (var id in regions) a.Db.IciRegionCoverages.Add(new() { TenantId = acting.TenantId, AssignmentId = result.Id, RegionId = id });
        await a.Log(acting, "ici.role.assign", result.Id.ToString(), role.Name); return result;
    }
    public static async Task Validate(Access a, Assignment w, string role, IciRequestInput input, IciRequest row, bool submit)
    {
        var admin = await IciPolicy.Administrator(a, w);
        Check(SubmitKinds(role, admin).Contains(input.Kind), "This role cannot submit this request type.", 403);
        Check(input.Title is { Length: >= 3 and <= 200 } && input.Description is { Length: <= 12000 } && input.Reference is { Length: <= 300 } && input.Code is { Length: <= 40 } && input.Email is { Length: <= 200 } && input.Country is { Length: <= 120 } && input.City is { Length: <= 120 }, "Enter valid request details.");
        Check(input.Amount is >= 0 and <= 1000000000 && input.MemberCount is >= 0 and <= 1000000 && input.VolunteerCount is >= 0 and <= 1000000 && input.Hours is >= 0 and <= 100000, "Invalid amounts, participants or hours.");
        Check(await a.Visible(w).AnyAsync(o => o.Id == input.OrgId && o.Status == "Active"), "Request is outside your organizational scope.", 403);
        var level = await (from o in a.Db.Orgs join l in a.Db.Levels on o.LevelId equals l.Id where o.Id == input.OrgId select l.Name).SingleAsync();
        if (input.Kind is "TitleClearance" or "Subscription") Check(level == "Club", "Use the requesting club as the owner.");
        if (input.EventId != null) Check(await IciPolicy.Events(a, w, a.Db.Events).AnyAsync(e => e.Id == input.EventId && e.TenantId == w.TenantId && e.OrgId == input.OrgId), "Event is outside this request's scope.", 404);
        if (input.Kind is "EventReport" or "FinancialAid") Check(input.EventId != null, "Select the related event or project activity.");
        if (input.Kind == "EventReport") Check(await a.Db.Events.AnyAsync(e => e.Id == input.EventId && e.EndsAt <= DateTimeOffset.UtcNow), "Only completed activities can be reported.");
        if (input.Kind == "FinancialAid") Check(input.Amount > 0 && input.Description.Trim().Length >= 10, "Describe the project and requested financial assistance.");
        var portfolio = input.EventId == null ? input.Portfolio : await a.Db.Events.Where(e => e.Id == input.EventId).Select(e => e.ActivityProgram != "" ? e.ActivityProgram : e.ActivityType == "Leadership" ? "Executive Leadership" : e.ActivityType).SingleAsync();
        if (input.Kind == "TitleClearance")
        {
            Check(input.TargetOrgId != null && await a.Db.Orgs.AnyAsync(o => o.Id == input.TargetOrgId && o.TenantId == w.TenantId && o.Status == "Active" && a.Db.Levels.Any(l => l.Id == o.LevelId && l.Name == "Region")), "Select the requesting club's region.");
            Check(await a.Tree(w.TenantId, input.TargetOrgId.Value).AnyAsync(c => c.Id == input.OrgId), "Select the requesting club's region.");
            Check(!await a.Db.IciRequests.AnyAsync(r => r.TenantId == w.TenantId && r.Id != row.Id && r.Kind == "TitleClearance" && r.Title.ToLower() == input.Title.Trim().ToLower() && r.Status != "Rejected" && r.Status != "Withdrawn"), "This title already has a clearance request.", 409);
            Check(!await a.Db.Orgs.AnyAsync(o => o.TenantId == w.TenantId && o.Name.ToLower() == input.Title.Trim().ToLower()), "This club title already exists.", 409);
        }
        var count = input.MemberCount; var amount = input.Amount;
        if (input.Kind == "TitleClearance") amount = 100;
        if (input.Kind == "Subscription")
        {
            var members = input.MemberIds ?? [];
            Check(members.Length is > 0 and <= 10000 && members.Distinct().Count() == members.Length, "Select distinct members for this subscription.");
            Check(await a.Db.Memberships.CountAsync(m => members.Contains(m.Id) && m.TenantId == w.TenantId && m.ClubId == input.OrgId && m.Status == "Active" && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow)) == members.Length, "All selected members must be active in the requesting club.");
            var initial = !await a.Db.IciRequests.AnyAsync(r => r.TenantId == w.TenantId && r.OrgId == input.OrgId && r.Kind == "Subscription" && r.Status == "Approved");
            Check(!initial || members.Length >= 20, "The initial subscription must cover at least 20 registered members.");
            count = members.Length; amount = 365m * count;
            if (submit) Check(input.Reference.Trim().Length >= 3, "Record a subscription payment reference for administrative verification.");
        }
        if (input.Kind is "ClubCreation" or "ClubMapping")
        {
            Check(input.TargetOrgId != null && await a.Visible(w).AnyAsync(o => o.Id == input.TargetOrgId && a.Db.Levels.Any(l => l.Id == o.LevelId && l.Name == "Region")), "Choose a region within your authority.");
            if (input.Kind == "ClubMapping") Check(level == "Club", "Choose the club being remapped.");
            else
            {
                Check(input.Code.Trim().Length > 0, "A club code is required.");
                Check(await a.Db.IciRequests.AnyAsync(r => r.Id == input.RelatedRequestId && r.TenantId == w.TenantId && r.Kind == "TitleClearance" && r.Status == "Approved" && r.Title.ToLower() == input.Title.Trim().ToLower() && r.TargetOrgId == input.TargetOrgId && r.PaymentVerifierId != null), "Approved, paid title clearance is required for this club and region.", 409);
            }
        }
        if (input.Kind == "RegionMapping")
        {
            Check(level == "Region" && input.TargetOrgId != null, "Select a region and NVP assignment.");
            var target = await a.Db.Assignments.SingleOrDefaultAsync(t => t.Id == input.TargetOrgId && t.TenantId == w.TenantId && a.Db.Roles.Any(r => r.Id == t.RoleId && r.Name == "National Vice President"));
            Check(target != null && await a.Effective(target) && await a.Tree(w.TenantId, target.OrgId).AnyAsync(o => o.Id == input.OrgId), "Select an effective NVP in this national unit.");
        }
        if (input.Kind == "TeamHandover")
        {
            Check(input.Role != "National President" || role == "Executive Board" || admin, "Only Executive Board can replace the National President.", 403);
            var targetRole = await a.Db.Roles.SingleOrDefaultAsync(r => r.TenantId == w.TenantId && r.Name == input.Role);
            Check(targetRole != null && IciPolicy.IsRole(input.Role), "Choose an ICI leadership role.");
            Check(input.Email.Trim().ToLowerInvariant() != (await a.User()).Email.ToLowerInvariant(), "You cannot appoint yourself.", 403);
            if (input.OldAssignmentId != null) Check(await a.Db.Assignments.AnyAsync(t => t.Id == input.OldAssignmentId && t.TenantId == w.TenantId && t.OrgId == input.OrgId && t.RoleId == targetRole!.Id), "Outgoing assignment must match this office.");
            await IciPolicy.ValidateAssignment(a, w.TenantId, input.OrgId, input.Role, input.Portfolio, input.Slot, input.RegionIds ?? [], input.OldAssignmentId);
        }
        row.TenantId = w.TenantId; row.OrgId = input.OrgId; row.Kind = input.Kind; row.Title = input.Title.Trim(); row.Description = input.Description;
        row.EventId = input.EventId; row.TargetOrgId = input.TargetOrgId; row.RelatedRequestId = input.RelatedRequestId; row.Portfolio = portfolio;
        row.MemberCount = count; row.VolunteerCount = input.VolunteerCount; row.Amount = amount; row.Hours = input.Hours;
        row.ReviewRoute = GovernanceService.Pack(Route(input.Kind, level));
        row.Payload = GovernanceService.Pack(input);
    }
    public static async Task Apply(Access a, Assignment w, IciRequest request, IciRequestInput input)
    {
        if (request.Kind == "ClubCreation")
        {
            var clearance = await a.Db.IciRequests.SingleAsync(r => r.Id == input.RelatedRequestId && r.TenantId == w.TenantId);
            Check(clearance.Status == "Approved" && clearance.PaymentVerifierId != null, "Title clearance is no longer valid.", 409);
            Check(!await a.Db.Orgs.AnyAsync(o => o.TenantId == w.TenantId && (o.Code == input.Code.Trim() || o.Name.ToLower() == request.Title.ToLower())), "Club name or code is already in use.", 409);
            var parent = await a.Visible(w).SingleAsync(o => o.Id == request.TargetOrgId);
            var level = await a.Db.Levels.SingleAsync(l => l.TenantId == w.TenantId && l.IsClub);
            var org = new Org { TenantId = w.TenantId, LevelId = level.Id, ParentId = parent.Id, Name = request.Title, Code = input.Code.Trim(), Country = input.Country, City = input.City, TimeZone = parent.TimeZone, Currency = parent.Currency };
            a.Db.Orgs.Add(org); request.ResultId = org.Id; clearance.ResultId = org.Id; clearance.Version++;
        }
        else if (request.Kind == "ClubMapping")
        {
            var club = await a.Visible(w).SingleAsync(o => o.Id == request.OrgId);
            Check(await a.Visible(w).AnyAsync(o => o.Id == request.TargetOrgId && a.Db.Levels.Any(l => l.Id == o.LevelId && l.Name == "Region")), "Destination region is no longer available.", 409);
            club.ParentId = request.TargetOrgId; club.Version++;
        }
        else if (request.Kind == "RegionMapping")
        {
            var target = await a.Db.Assignments.SingleOrDefaultAsync(t => t.Id == request.TargetOrgId && t.TenantId == w.TenantId && a.Db.Roles.Any(role => role.Id == t.RoleId && role.Name == "National Vice President"));
            Check(target != null && await a.Effective(target) && await a.Tree(w.TenantId, target.OrgId).AnyAsync(o => o.Id == request.OrgId), "NVP assignment is no longer effective in this national unit.", 409);
            await a.Db.IciRegionCoverages.Where(c => c.TenantId == w.TenantId && c.RegionId == request.OrgId && a.Db.Assignments.Any(t => t.Id == c.AssignmentId && a.Db.Roles.Any(r => r.Id == t.RoleId && r.Name == "National Vice President"))).ExecuteDeleteAsync();
            a.Db.IciRegionCoverages.Add(new() { TenantId = w.TenantId, AssignmentId = target!.Id, RegionId = request.OrgId });
        }
        else if (request.Kind == "TeamHandover")
        {
            if (input.OldAssignmentId != null)
            {
                var old = await a.Db.Assignments.SingleAsync(t => t.Id == input.OldAssignmentId && t.TenantId == w.TenantId);
                if (old.AppointmentId != null) { var office = await a.Db.OfficerAppointments.SingleAsync(o => o.Id == old.AppointmentId); office.Status = "Revoked"; office.Version++; }
                await a.Db.Sessions.Where(s => s.WorkspaceId == old.Id).ExecuteUpdateAsync(set => set.SetProperty(s => s.WorkspaceId, (Guid?)null));
                a.Db.Assignments.Remove(old); await a.Db.SaveChangesAsync();
            }
            var role = await a.Db.Roles.SingleAsync(r => r.TenantId == w.TenantId && r.Name == input.Role);
            var assignment = await InstallAssignment(a, w, new(input.Email, role.Id, input.OrgId, input.Portfolio, input.Slot, input.RegionIds));
            request.ResultId = assignment.Id;
        }
    }
}
