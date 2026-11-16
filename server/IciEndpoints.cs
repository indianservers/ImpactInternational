using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;
namespace Clubs;

public static class IciEndpoints
{
    static IDataProtector Protector(IDataProtectionProvider keys) => keys.CreateProtector("Impact.ICI.private.v1");
    static IciRequestInput Input(IciRequest r, IDataProtectionProvider keys) => GovernanceService.Unpack<IciRequestInput>(r.Kind == "LegalAction" ? Protector(keys).Unprotect(r.Payload) : r.Payload);
    static async Task<IQueryable<IciRequest>> Requests(Access a, Assignment w)
    {
        var q = a.Db.IciRequests.Where(r => r.TenantId == w.TenantId && a.Visible(w).Any(o => o.Id == r.OrgId));
        if (await IciPolicy.Administrator(a, w)) return q;
        var role = await IciPolicy.Role(a, w);
        if (role == "Executive Board") return q;
        q = q.Where(r => r.Kind != "LegalAction");
        if (role == "National Treasurer") return q.Where(r => r.Kind == "FinancialAid" && r.Status != "Draft");
        if (role is "Regional Director" or "National Director" or "National Coordinator")
        {
            var events = IciPolicy.Events(a, w, a.Db.Events).Select(e => e.Id);
            return q.Where(r => r.EventId != null && events.Contains(r.EventId.Value) && (r.Kind == "EventReport" || r.Kind == "EventBid"));
        }
        if (role == "National President") return q;
        if (role == "Regional President") return q.Where(r => r.Kind == "EventReport" || r.CreatorId == w.UserId);
        return q.Where(r => r.CreatorId == w.UserId);
    }
    static object Summary(IciRequest r, string role, bool admin)
    {
        var route = GovernanceService.Unpack<string[]>(r.ReviewRoute);
        var reviewer = r.Stage < route.Length ? route[r.Stage] : "Completed";
        return new
        {
            r.Id,
            r.OrgId,
            r.Kind,
            r.Title,
            description = r.Kind == "LegalAction" ? "Confidential legal record" : r.Description,
            r.EventId,
            r.TargetOrgId,
            r.RelatedRequestId,
            r.Portfolio,
            r.Amount,
            r.MemberCount,
            r.VolunteerCount,
            r.Hours,
            r.Status,
            r.Stage,
            r.Revision,
            r.Version,
            r.CreatedAt,
            r.UpdatedAt,
            r.CreatorId,
            r.CreatorRole,
            r.PaymentReference,
            paymentVerified = r.PaymentVerifierId != null,
            r.ResultId,
            reviewer,
            route,
            canReview = r.Status == "Submitted" && (admin || reviewer.Split('|').Contains(role)),
            canVerifyPayment = admin && r.Status == "Submitted" && r.Kind is "TitleClearance" or "Subscription"
        };
    }
    public static void MapIciPortal(this RouteGroupBuilder api)
    {
        api = api.MapGroup("").AddEndpointFilter(async (context, next) =>
        {
            var nullability = new System.Reflection.NullabilityInfoContext();
            foreach (var arg in context.Arguments.Where(arg => arg?.GetType().Name.StartsWith("Ici") == true && arg.GetType().Name.EndsWith("Input")))
                foreach (var property in arg!.GetType().GetProperties())
                    if (nullability.Create(property).ReadState == System.Reflection.NullabilityState.NotNull) Check(property.GetValue(arg) != null, "Missing field: " + property.Name);
            return await next(context);
        });
        api.MapGet("/ici/context", async (Access a) =>
        {
            var w = await a.Workspace("ici.view"); var role = await IciPolicy.Role(a, w); var admin = await IciPolicy.Administrator(a, w); var grants = await a.Grants(w);
            var units = await (from o in a.Visible(w) join l in a.Db.Levels on o.LevelId equals l.Id where o.Status == "Active" orderby o.Name select new { o.Id, o.Name, o.ParentId, level = l.Name }).ToListAsync();
            var ancestors = new List<object>(); Guid? id = w.OrgId;
            for (var depth = 0; id != null && depth < 8; depth++) { var node = await (from o in a.Db.Orgs join l in a.Db.Levels on o.LevelId equals l.Id where o.Id == id && o.TenantId == w.TenantId select new { o.Id, o.Name, o.ParentId, level = l.Name }).SingleOrDefaultAsync(); if (node == null) break; ancestors.Add(node); id = node.ParentId; }
            return new
            {
                role,
                portfolio = w.IciPortfolio,
                slot = w.IciSlot,
                definitions = IciPolicy.Roles,
                units,
                ancestors,
                submitKinds = IciWorkflow.SubmitKinds(role, admin),
                activityPrograms = IciPolicy.Programs,
                subscriptionPerMember = 365,
                titleClearanceFee = 100,
                initialSubscriptionMinimum = 20,
                capabilities = new { admin, team = admin || grants.Contains("ici.team"), members = grants.Contains("members.view"), bank = IciPolicy.OwnBank(role), legal = admin || role == "Executive Board", review = admin || grants.Contains("ici.review") },
                source = new { title = "ICI Portal.pdf", notebook = "https://notebook.google.com/notebook/e9b1eee2-1519-46ef-8038-cc45879bbaf0", note = "National Secretary is listed without separate duties; profile access and assigned-region reports are provided without additional approval powers. Area means assigned regions within the four-level hierarchy." }
            };
        });
        api.MapGet("/ici/reports", Reports);
        api.MapGet("/ici/requests", async (Access a, string kind = "", string status = "", int page = 1) =>
        {
            var w = await a.Workspace("ici.view"); Check(page is > 0 and <= 10000, "Invalid page."); var q = await Requests(a, w);
            if (kind != "") q = q.Where(r => r.Kind == kind); if (status != "") q = q.Where(r => r.Status == status);
            var role = await IciPolicy.Role(a, w); var admin = await IciPolicy.Administrator(a, w);
            return new { total = await q.CountAsync(), page, pageSize = 30, items = (await q.OrderByDescending(r => r.UpdatedAt).ThenBy(r => r.Id).Skip((page - 1) * 30).Take(30).ToListAsync()).Select(r => Summary(r, role, admin)) };
        });
        api.MapGet("/ici/requests/{id:guid}", async (Guid id, Access a, IDataProtectionProvider keys) =>
        {
            var w = await a.Workspace("ici.view"); var r = await (await Requests(a, w)).SingleOrDefaultAsync(r => r.Id == id); Check(r != null, "Request not found in your scope.", 404);
            return new { request = Summary(r!, await IciPolicy.Role(a, w), await IciPolicy.Administrator(a, w)), input = Input(r!, keys), decisions = await a.Db.IciDecisions.Where(d => d.RequestId == id && d.TenantId == w.TenantId).OrderBy(d => d.At).Select(d => new { d.Id, d.Stage, d.Revision, d.ActorRole, d.Decision, d.Comment, d.At }).ToListAsync() };
        });
        api.MapPost("/ici/requests", async (IciRequestInput input, Access a, IDataProtectionProvider keys) => await Save(null, input, a, keys));
        api.MapPut("/ici/requests/{id:guid}", async (Guid id, IciRequestInput input, Access a, IDataProtectionProvider keys) => await Save(id, input, a, keys));
        api.MapPost("/ici/requests/{id:guid}/actions", Action);
        api.MapGet("/ici/event-reviews", async (Access a, GovernanceService governance) =>
        {
            var w = await a.Workspace("ici.view"); if (!(await a.Grants(w)).Contains("governance.approve")) return Results.Ok(Array.Empty<object>());
            var requests = await governance.Requests(w);
            var rows = await requests.Where(r => r.Kind == "EventPublication" && (r.Status == "Submitted" || r.Status == "Under Review")).OrderBy(r => r.CreatedAt).Take(100).ToListAsync();
            var role = await IciPolicy.Role(a,w); var admin = await IciPolicy.Administrator(a,w);
            var orgIds = rows.Select(r => r.OrgId).ToArray();
            var levels = await (from o in a.Db.Orgs join l in a.Db.Levels on o.LevelId equals l.Id where orgIds.Contains(o.Id) select new {o.Id,l.Name}).ToDictionaryAsync(o => o.Id,o => o.Name);
            var ids = rows.Select(r => r.Id).ToArray();
            var prior = await a.Db.MembershipApprovals.Where(d => ids.Contains(d.GovernanceRequestId!.Value) && d.ActorId == w.UserId && d.Decision == "Approve").Select(d => new{d.GovernanceRequestId,d.Revision}).ToListAsync();
            return Results.Ok(rows.Select(r => {
                var reviewer = (levels[r.OrgId], r.CurrentSequence) switch { ("Club",1) => "Regional President", ("Club",2) => "National Director", ("Region",1) => "National Director", ("Region",2) => "Executive Board", _ => "Administration" };
                return new { r.Id, r.Title, r.OrgId, r.Version, r.Status, r.CurrentSequence, r.WorkflowId, reviewer,
                    canReview = r.CreatorId != w.UserId && (admin || role == reviewer) && !prior.Any(d => d.GovernanceRequestId == r.Id && d.Revision == r.Revision) };
            }));
        });
        api.MapGet("/ici/team", Team);
        api.MapPost("/ici/team", async (IciAssignmentInput input, Access a) =>
        {
            var w = await a.Workspace("roles.assign"); await using var tx = await a.Mutation(w); var assignment = await IciWorkflow.Assign(a, input); await a.Db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(assignment);
        });
        api.MapPut("/ici/team/{id:guid}/scope", async (Guid id, IciScopeInput input, Access a) =>
        {
            var w = await a.Workspace("roles.assign"); Check(await IciPolicy.Administrator(a, w), "Only administrators configure assignment coverage directly.", 403);
            await using var tx = await a.Mutation(w);
            var target = await a.Db.Assignments.SingleOrDefaultAsync(t => t.Id == id && t.TenantId == w.TenantId); Check(target != null && await a.Visible(w).AnyAsync(o => o.Id == target.OrgId), "Assignment outside scope.", 404);
            var role = await IciPolicy.Role(a, target!); await IciPolicy.ValidateAssignment(a, w.TenantId, target!.OrgId, role, input.Portfolio, input.Slot, input.RegionIds, id);
            target.IciPortfolio = input.Portfolio; target.IciSlot = input.Slot; await a.Db.IciRegionCoverages.Where(c => c.AssignmentId == id).ExecuteDeleteAsync();
            foreach (var region in input.RegionIds) a.Db.IciRegionCoverages.Add(new() { TenantId = w.TenantId, AssignmentId = id, RegionId = region });
            await a.Log(w, "ici.assignment.scope", id.ToString()); await a.Db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(new { target.Id, target.IciPortfolio, target.IciSlot });
        });
        api.MapGet("/ici/profile", async (Access a, IDataProtectionProvider keys) =>
        {
            var w = await a.Workspace("organization.view"); var row = await a.Db.IciPrivateProfiles.SingleOrDefaultAsync(p => p.UserId == w.UserId); var bankAllowed = IciPolicy.OwnBank(await IciPolicy.Role(a, w));
            var bank = row?.ProtectedBankDetails is { Length: > 0 } && bankAllowed ? GovernanceService.Unpack<IciBankDetails>(Protector(keys).Unprotect(row.ProtectedBankDetails)) : null;
            return new { trainingCareer = row?.TrainingCareer ?? "", leadershipCareer = row?.LeadershipCareer ?? "", version = row?.Version ?? 1, bankAllowed, bankName = bank?.BankName ?? "", accountHolder = bank?.AccountHolder ?? "", accountLast4 = bank == null ? "" : bank.AccountNumber[^Math.Min(4, bank.AccountNumber.Length)..], routingCode = bank?.RoutingCode ?? "", hasBankDetails = bank != null };
        });
        api.MapPut("/ici/profile", async (IciProfileInput input, Access a, IDataProtectionProvider keys) =>
        {
            var w = await a.Workspace("organization.view"); Check(input.TrainingCareer is { Length: <= 8000 } && input.LeadershipCareer is { Length: <= 8000 }, "Career notes must be at most 8,000 characters.");
            var row = await a.Db.IciPrivateProfiles.SingleOrDefaultAsync(p => p.UserId == w.UserId); Check(row == null || row.Version == input.Version, "Profile changed. Reload and retry.", 409);
            if (row == null) { row = new IciPrivateProfile { UserId = w.UserId }; a.Db.IciPrivateProfiles.Add(row); }
            row.TrainingCareer = input.TrainingCareer; row.LeadershipCareer = input.LeadershipCareer;
            if (input.ReplaceBankDetails)
            {
                Check(IciPolicy.OwnBank(await IciPolicy.Role(a, w)), "Bank details are available only in your Club President or Regional President profile.", 403);
                Check(input.BankName is { Length: <= 160 } && input.AccountHolder is { Length: <= 160 } && input.AccountNumber is { Length: <= 50 } && input.RoutingCode is { Length: <= 40 }, "Invalid bank details.");
                Check(input.AccountNumber == "" || System.Text.RegularExpressions.Regex.IsMatch(input.AccountNumber, @"^[A-Za-z0-9 -]{4,50}$"), "Enter a valid account number.");
                row.ProtectedBankDetails = input.AccountNumber == "" ? "" : Protector(keys).Protect(GovernanceService.Pack(new IciBankDetails(input.BankName, input.AccountHolder, input.AccountNumber, input.RoutingCode)));
            }
            row.Version++; await a.Log(w, "ici.profile.save", w.UserId.ToString()); await a.Db.SaveChangesAsync(); return Results.Ok(new { row.Version });
        });
    }
    static async Task<object> Save(Guid? id, IciRequestInput input, Access a, IDataProtectionProvider keys)
    {
        var w = await a.Workspace("ici.submit"); await using var tx = await a.Mutation(w); var role = await IciPolicy.Role(a, w);
        var row = id == null ? new IciRequest { CreatorId = w.UserId, CreatorRole = role } : await (await Requests(a, w)).SingleOrDefaultAsync(r => r.Id == id);
        Check(row != null, "Request not found.", 404); Check(id == null || row!.CreatorId == w.UserId && row.Version == input.Version && (row.Status == "Draft" || row.Status == "ChangesRequested"), "Only your editable, current draft can be changed.", 409);
        Check(id == null || row!.Kind == input.Kind, "Request type cannot change.");
        await IciWorkflow.Validate(a, w, role, input, row!, false);
        if (input.Kind == "LegalAction") { row!.Description = "Confidential legal record"; row.Payload = Protector(keys).Protect(row.Payload); }
        row!.PaymentVerifierId = null; row.PaymentReference = input.Reference; row.Version++; row.UpdatedAt = DateTimeOffset.UtcNow;
        if (id == null) a.Db.IciRequests.Add(row);
        await a.Log(w, "ici.request.save", row.Id.ToString(), row.Kind); await a.Db.SaveChangesAsync(); await tx.CommitAsync(); return Summary(row, role, await IciPolicy.Administrator(a, w));
    }
    static async Task<object> Action(Guid id, IciActionInput input, Access a, IDataProtectionProvider keys)
    {
        var w = await a.Workspace("ici.view"); Check(input.Comment is { Length: <= 2000 }, "Comment is too long.");
        await using var tx = await a.Mutation(w); var r = await (await Requests(a, w)).SingleOrDefaultAsync(r => r.Id == id); Check(r != null, "Request outside scope.", 404);
        Check(r!.Version == input.Version, "Request changed. Reload and retry.", 409); var role = await IciPolicy.Role(a, w); var admin = await IciPolicy.Administrator(a, w); var data = Input(r, keys);
        if (input.Action == "Submit")
        {
            Check(r.CreatorId == w.UserId && r.Status is "Draft" or "ChangesRequested", "Only the requester can submit an editable request.", 403);
            await IciWorkflow.Validate(a, w, role, data, r, true); if (r.Kind == "LegalAction") { r.Description = "Confidential legal record"; r.Payload = Protector(keys).Protect(r.Payload); }
            if (r.Status == "ChangesRequested") r.Revision++;
            if (r.Kind == "Subscription")
            {
                var root = await a.Db.Orgs.SingleAsync(o => o.TenantId == w.TenantId && o.ParentId == null);
                var period = ActivityPolicy.FinancialYear(root).Start.ToString("yyyy-MM-dd");
                var members = await a.Db.Memberships.Where(m => (data.MemberIds ?? Array.Empty<Guid>()).Contains(m.Id)).Select(m => new { m.Id, m.PersonId }).ToListAsync();
                Check(members.Select(m => m.PersonId).Distinct().Count() == members.Count, "Select each member only once, even if they hold multiple categories.");
                var people = members.Select(m => m.PersonId).ToArray();
                Check(!await a.Db.IciSubscriptionMembers.AnyAsync(m => m.TenantId == w.TenantId && m.OrgId == r.OrgId && m.PeriodKey == period && people.Contains(m.PersonId)), "One or more members already have a pending or paid subscription for this financial year.", 409);
                foreach (var member in members) a.Db.IciSubscriptionMembers.Add(new() { TenantId = w.TenantId, OrgId = r.OrgId, RequestId = r.Id, PersonId = member.PersonId, MembershipId = member.Id, PeriodKey = period });
            }
            r.Status = "Submitted"; r.Stage = 0;
        }
        else if (input.Action == "Withdraw")
        {
            Check(r.CreatorId == w.UserId && r.Status is "Draft" or "Submitted" or "ChangesRequested", "Only the requester can withdraw a pending request.", 403); r.Status = "Withdrawn";
        }
        else if (input.Action == "VerifyPayment")
        {
            Check(admin && r.Status == "Submitted" && r.Kind is "TitleClearance" or "Subscription", "Only administrators verify these payment records.", 403);
            Check(input.Comment.Trim().Length >= 3, "Record the verified payment evidence/reference."); r.PaymentReference = input.Comment.Trim(); r.PaymentVerifierId = w.UserId;
        }
        else
        {
            Check(new[] { "Approve", "Reject", "RequestChanges" }.Contains(input.Action) && r.Status == "Submitted", "Request is not awaiting this action.", 409);
            Check(r.CreatorId != w.UserId, "You cannot review your own request.", 403);
            var route = GovernanceService.Unpack<string[]>(r.ReviewRoute); Check(r.Stage < route.Length, "Review is complete.", 409);
            Check(admin || route[r.Stage].Split('|').Contains(role), "This role cannot review the current stage.", 403);
            Check(input.Action == "Approve" || input.Comment.Trim().Length >= 3, "Explain a rejection or change request.");
            Check(!await a.Db.IciDecisions.AnyAsync(d => d.RequestId == r.Id && d.Revision == r.Revision && d.ActorId == w.UserId), "Your review is already recorded for this revision.", 409);
            if (input.Action == "Approve" && r.Kind is "TitleClearance" or "Subscription") Check(r.PaymentVerifierId != null, "Administrative verification of the payment record is required.", 409);
            a.Db.IciDecisions.Add(new() { TenantId = w.TenantId, RequestId = r.Id, ActorId = w.UserId, ActorRole = role, Stage = r.Stage, Revision = r.Revision, Decision = input.Action, Comment = input.Comment });
            if (input.Action == "Reject") r.Status = "Rejected";
            else if (input.Action == "RequestChanges") r.Status = "ChangesRequested";
            else if (++r.Stage >= route.Length) { await IciWorkflow.Apply(a, w, r, data); r.Status = "Approved"; }
        }
        if (r.Kind == "Subscription" && r.Status is "Withdrawn" or "Rejected" or "ChangesRequested") await a.Db.IciSubscriptionMembers.Where(m => m.RequestId == r.Id).ExecuteDeleteAsync();
        r.Version++; r.UpdatedAt = DateTimeOffset.UtcNow; await a.Log(w, "ici.request." + input.Action.ToLowerInvariant(), r.Id.ToString(), r.Kind); await a.Db.SaveChangesAsync(); await tx.CommitAsync(); return Summary(r, role, admin);
    }
    static async Task<object> Team(Access a)
    {
        var w = await a.Workspace("ici.view"); var admin = await IciPolicy.Administrator(a, w); Check(admin || (await a.Grants(w)).Contains("ici.team"), "Team management is restricted to authorized leadership.", 403);
        var rows = await (from t in a.Db.Assignments
                          join u in a.Db.Users on t.UserId equals u.Id
                          join r in a.Db.Roles on t.RoleId equals r.Id
                          join o in a.Db.Orgs on t.OrgId equals o.Id
                          where t.TenantId == w.TenantId && a.Visible(w).Any(v => v.Id == o.Id) && IciPolicy.Roles.Select(d => d.Name).Contains(r.Name)
                          orderby r.Name, u.Name
                          select new { t.Id, t.UserId, u.Name, u.Email, role = r.Name, t.RoleId, t.OrgId, organization = o.Name, portfolio = t.IciPortfolio, slot = t.IciSlot }).ToListAsync();
        var coverage = await a.Db.IciRegionCoverages.Where(c => c.TenantId == w.TenantId && a.Visible(w).Any(o => o.Id == c.RegionId)).Select(c => new { c.AssignmentId, c.RegionId }).ToListAsync();
        return new { assignments = rows, coverage, roles = await a.Db.Roles.Where(r => r.TenantId == w.TenantId && IciPolicy.Roles.Select(d => d.Name).Contains(r.Name)).Select(r => new { r.Id, r.Name }).ToListAsync(), admin };
    }
    static async Task<object> Reports(Access a, Guid? orgId = null, string portfolio = "", string search = "", int page = 1, DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        var w = await a.Workspace("ici.view"); Check(page is > 0 and <= 10000 && search.Length <= 100 && portfolio.Length <= 100, "Invalid report filters.");
        var root = await a.Db.Orgs.SingleAsync(o => o.TenantId == w.TenantId && o.ParentId == null); var year = ActivityPolicy.FinancialYear(root); var start = from ?? year.Start; var end = to ?? year.End;
        Check(start < end && end <= start.AddYears(5), "Choose a report range up to five years.");
        var q = IciPolicy.Events(a, w, a.Db.Events).Where(e => e.StartsAt >= start && e.StartsAt < end);
        if (orgId != null) { Check(await a.Visible(w).AnyAsync(o => o.Id == orgId), "Report area outside your scope.", 404); q = q.Where(e => a.Tree(w.TenantId, orgId.Value).Any(o => o.Id == e.OrgId)); }
        if (portfolio != "") { var type = portfolio == "Executive Leadership" ? "Leadership" : portfolio; q = q.Where(e => e.ActivityType == type || e.ActivityProgram == portfolio); }
        if (search != "") q = q.Where(e => EF.Functions.ILike(e.Title, "%" + search + "%"));
        var requests = await Requests(a, w);
        return new
        {
            page,
            pageSize = 30,
            total = await q.CountAsync(),
            participants = await q.SumAsync(e => (long)e.MemberParticipants),
            volunteers = await q.SumAsync(e => (long)e.OutsideVolunteers),
            financialAid = requests.Where(r => r.Kind == "FinancialAid" && r.Status != "Draft").GroupBy(r => r.Status).Select(g => new { status = g.Key, count = g.Count(), amount = g.Sum(r => r.Amount) }).ToList(),
            categories = await q.GroupBy(e => e.ActivityType).Select(g => new { category = g.Key, count = g.Count() }).ToListAsync(),
            items = await q.OrderByDescending(e => e.StartsAt).ThenBy(e => e.Id).Skip((page - 1) * 30).Take(30).Select(e => new { e.Id, e.Title, e.OrgId, organization = a.Db.Orgs.Where(o => o.Id == e.OrgId).Select(o => o.Name).First(), e.ActivityType, e.ActivityProgram, e.ActivityLevel, e.StartsAt, e.EndsAt, e.MemberParticipants, e.OutsideVolunteers, e.Publication, e.Approval, e.Lifecycle }).ToListAsync()
        };
    }
}
