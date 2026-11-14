using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public record ManualMember(
    PersonInput Person,
    Guid ClubId,
    Guid CategoryId,
    Guid? PersonId = null,
    DateTimeOffset? JoinedAt = null,
    string DuplicateDecision = "",
    string? PhotoBase64 = null, string Status = "Active", string RequestKey = ""
);

public record StatusInput(
    string Status,
    string Reason,
    long Version,
    DateTimeOffset? EffectiveAt = null
);

public record PrivacyInput(
    bool DirectoryVisible,
    bool DisplayEmail,
    bool DisplayPhone,
    bool ProfessionalVisible,
    bool BirthdayAnnouncements,
    bool Marketing,
    bool PhotoVisible,
    bool AllowContactRequests,
    bool CardNameVisible,
    bool TransactionalEmail,
    long Version
);

public static class MembershipEndpoints
{
    public static void MapMembership(this RouteGroupBuilder api)
    {
        api.MapMembershipHierarchy();
        api.MapGet(
            "/membership/catalog",
            async (MembershipService s, Guid? tenantId, string? search, int page = 1) =>
            {
                await s.Access.User();
                var tenants = await s
                    .Db.Tenants.OrderBy(x => x.Name)
                    .Select(x => new { x.Id, x.Name })
                    .Take(200)
                    .ToListAsync();
                var clubs = s.Db.Orgs.Where(x =>
                    x.Status == "Active" && s.Db.Levels.Any(l => l.Id == x.LevelId && l.IsClub)
                );
                if (tenantId != null)
                    clubs = clubs.Where(x => x.TenantId == tenantId);
                if (!string.IsNullOrWhiteSpace(search))
                    clubs = clubs.Where(x => EF.Functions.ILike(x.Name, "%" + search + "%"));
                return new
                {
                    associations = tenants,
                    clubs = await clubs
                        .OrderBy(x => x.Name)
                        .ThenBy(x => x.Id)
                        .Skip((Math.Max(1, page) - 1) * 50)
                        .Take(50)
                        .Select(x => new
                        {
                            x.Id,
                            x.TenantId,
                            x.Name,
                            x.City,
                            x.Country,
                        })
                        .ToListAsync(),
                    categories = await s
                        .Db.MembershipCategories.Where(x =>
                            x.Active && (tenantId == null || x.TenantId == tenantId)
                        )
                        .Select(x => new
                        {
                            x.Id,
                            x.TenantId,
                            x.Name,
                            x.Code,
                            x.ValidityMonths,
                        })
                        .Take(500)
                        .ToListAsync(),
                };
            }
        );
        api.MapGet(
            "/membership/options",
            async (MembershipService s, string? search, Guid? regionId, Guid? districtId, Guid? zoneId) =>
            {
                var w = await s.Access.WorkspaceAny(
                    "members.view",
                    "members.manage",
                    "members.import",
                    "members.invite",
                    "members.transfer.approve",
                    "members.workflow.manage",
                    "directory.view"
                );
                var clubs = s
                    .Access.Visible(w)
                    .Where(x =>
                        x.Status == "Active" && s.Db.Levels.Any(l => l.Id == x.LevelId && l.IsClub)
                    );
                if (regionId != null)
                {
                    await RegionalDirectory.Filter(s, w, s.Scoped(w), regionId);
                    clubs = clubs.Where(c => s.Access.Tree(w.TenantId, regionId.Value).Any(o => o.Id == c.Id));
                }
                await MembershipPresentation.Filter(s,w,s.Scoped(w),districtId,zoneId,null,null,null,null,null);
                foreach(var branchId in new[]{districtId,zoneId}) if(branchId!=null){var branch=branchId.Value;clubs=clubs.Where(c=>s.Access.Tree(w.TenantId,branch).Any(o=>o.Id==c.Id));}
                if (!string.IsNullOrWhiteSpace(search))
                    clubs = clubs.Where(x => EF.Functions.ILike(x.Name, "%" + search + "%"));
                return new
                {
                    clubs = await clubs
                        .OrderBy(x => x.Name)
                        .Take(1000)
                        .Select(x => new
                        {
                            x.Id,
                            x.Name,
                            x.TenantId, x.ClubType,
                        })
                        .ToListAsync(),
                    membershipTypes = await s.Access.Visible(w).Where(o=>s.Db.Levels.Any(l=>l.Id==o.LevelId&&l.IsClub)).Select(o=>o.ClubType).Distinct().OrderBy(x=>x).ToListAsync(),
                    categories = await s
                        .Db.MembershipCategories.Where(x => x.TenantId == w.TenantId && x.Active)
                        .Select(x => new { x.Id, x.Name })
                        .ToListAsync(),
                };
            }
        );
        api.MapGet(
            "/members",
            async (
                MembershipService s,
                string? search,
                string? statuses,
                Guid? clubId,
                Guid? categoryId,
                string? city,
                string? profession,
                string? industry,
                string? expertise,
                string? office,
                string sort = "name",
                int page = 1,
                int size = 20,
                string? after = null,
                Guid? regionId = null, Guid? districtId = null, Guid? zoneId = null, string? membershipType = null, DateOnly? joinedFrom = null, DateOnly? joinedTo = null, DateOnly? renewalFrom = null, DateOnly? renewalTo = null, Guid? nationalId=null,string? country=null,string? state=null
            ) =>
                await List(
                    s,
                    false,
                    search,
                    statuses,
                    clubId,
                    categoryId,
                    city,
                    profession,
                    industry,
                    expertise,
                    office,
                    sort,
                    page,
                    size,
                    after,
                    regionId, districtId, zoneId, membershipType, joinedFrom, joinedTo, renewalFrom, renewalTo, nationalId,country,state
                )
        );
        api.MapGet(
            "/directory",
            async (
                MembershipService s,
                string? search,
                string? statuses,
                Guid? clubId,
                Guid? categoryId,
                string? city,
                string? profession,
                string? industry,
                string? expertise,
                string? office,
                string sort = "name",
                int page = 1,
                int size = 20,
                string? after = null,
                Guid? regionId = null, Guid? districtId = null, Guid? zoneId = null, string? membershipType = null, DateOnly? joinedFrom = null, DateOnly? joinedTo = null, DateOnly? renewalFrom = null, DateOnly? renewalTo = null, Guid? nationalId=null,string? country=null,string? state=null
            ) =>
                await List(
                    s,
                    true,
                    search,
                    statuses,
                    clubId,
                    categoryId,
                    city,
                    profession,
                    industry,
                    expertise,
                    office,
                    sort,
                    page,
                    size,
                    after,
                    regionId, districtId, zoneId, membershipType, joinedFrom, joinedTo, renewalFrom, renewalTo, nationalId,country,state
                )
        );
        api.MapGet(
            "/members/{id:guid}",
            async (Guid id, MembershipService s, bool directory = false) =>
            {
                var u = await s.Access.User();
                var m = await s.Find(id, directory ? "directory.view" : "members.view");
                var own = m.Person.UserId == u.Id;
                Assignment? w = own
                    ? null
                    : await s.Access.Workspace(directory ? "directory.view" : "members.view");
                Check(
                    own || !directory || m.Person.Privacy.DirectoryVisible,
                    "Member is not listed in the directory.",
                    404
                );
                var grants = w == null ? [] : await s.Access.Grants(w);
                var p = await s.People.SingleAsync(x => x.Id == m.PersonId);
                var visible = own ? s.Own(u.Id) : s.Scoped(w!);
                var membershipIds = visible.Where(x => x.PersonId == p.Id).Select(x => x.Id);
                var privateProfile = own || !directory && grants.Contains("members.personal.view");
                var contacts = own || directory || grants.Contains("members.contacts.view");
                var roles =
                    p.UserId == null
                        ? []
                        : await (
                            from a in s.Db.Assignments
                            join r in s.Db.Roles on a.RoleId equals r.Id
                            join o in s.Db.Orgs on a.OrgId equals o.Id
                            where
                                a.UserId == p.UserId
                                && (
                                    own
                                    || a.TenantId == m.TenantId
                                        && s.Access.Visible(w!).Any(v => v.Id == a.OrgId)
                                )
                            select new
                            {
                                r.Name,
                                Organization = o.Name,
                                a.OrgId,
                            }
                        ).ToListAsync();
                if ((privateProfile || contacts && !directory) && !own)
                    s.Db.Audits.Add(
                        new()
                        {
                            TenantId = m.TenantId,
                            OrgId = m.ClubId,
                            ActorId = u.Id,
                            Action = "member.private.view",
                            Resource = m.Id.ToString(),
                        }
                    );
                await s.Db.SaveChangesAsync();
                return Results.Ok(
                    new
                    {
                        person = await s.Profile(
                            p,
                            privateProfile,
                            contacts,
                            own || p.Privacy.ProfessionalVisible,
                            own,
                            directory
                        ),
                        membership = await s.Rows(
                                s.Db.Memberships.Where(x => x.Id == id),
                                directory,
                                contacts,
                                true,
                                u.Id
                            )
                            .SingleAsync(),
                        memberships = await s.Rows(
                                visible.Where(x => x.PersonId == p.Id),
                                directory,
                                contacts,
                                true,
                                u.Id
                            )
                            .ToListAsync(),
                        roles,
                        history = directory && !own
                            ? null
                            : await s
                                .Db.MembershipHistory.Where(x =>
                                    membershipIds.Contains(x.MembershipId)
                                )
                                .OrderByDescending(x => x.EffectiveAt)
                                .Take(200)
                                .ToListAsync(),
                        applications = directory && !own
                            ? null
                            : await s
                                .Db.MembershipApplications.Where(x =>
                                    x.PersonId == p.Id
                                    && (
                                        own
                                        || x.TenantId == w!.TenantId
                                            && s.Access.Visible(w).Any(o => o.Id == x.ClubId)
                                    )
                                )
                                .OrderByDescending(x => x.CreatedAt)
                                .Select(x => new
                                {
                                    x.Id,
                                    x.Status,
                                    x.CreatedAt,
                                    x.SubmittedAt,
                                    x.ClubId,
                                    x.CurrentSequence,
                                })
                                .Take(100)
                                .ToListAsync(),
                        activities = directory && !own
                            ? null
                            : await s
                                .Db.MemberActivities.Where(x =>
                                    x.PersonId == p.Id
                                    && (
                                        own
                                        || x.TenantId == w!.TenantId
                                            && s.Access.Visible(w).Any(o => o.Id == x.ClubId)
                                    )
                                )
                                .OrderByDescending(x => x.At)
                                .Take(100)
                                .ToListAsync(),
                        documents = own || grants.Contains("members.documents")
                            ? await s
                                .Db.MembershipDocuments.Where(x =>
                                    membershipIds.Contains(x.MembershipId)
                                )
                                .Select(x => new
                                {
                                    x.Id,
                                    x.MembershipId,
                                    x.FileName,
                                    x.Kind,
                                    x.Status,
                                    x.At,
                                    x.ContentType,
                                    x.RetainUntil,
                                })
                                .Take(100)
                                .ToListAsync()
                            : null,
                        renewals = directory && !own
                            ? null
                            : await s
                                .Db.MembershipRenewals.Where(x =>
                                    membershipIds.Contains(x.MembershipId)
                                )
                                .OrderByDescending(x => x.At)
                                .Take(100)
                                .ToListAsync(),
                        transfers = directory && !own
                            ? null
                            : await s
                                .Db.MembershipTransfers.Where(x =>
                                    membershipIds.Contains(x.MembershipId)
                                    || membershipIds.Contains(
                                        x.DestinationMembershipId ?? Guid.Empty
                                    )
                                )
                                .OrderByDescending(x => x.CreatedAt)
                                .Take(100)
                                .ToListAsync(),
                        roleHistory = p.UserId == null
                            ? null
                            : await s
                                .Db.RoleAssignmentHistory.Where(h =>
                                    h.UserId == p.UserId
                                    && (
                                        own
                                        || h.TenantId == w!.TenantId
                                            && s.Access.Visible(w).Any(o => o.Id == h.OrgId)
                                    )
                                )
                                .OrderByDescending(h => h.At)
                                .Take(100)
                                .Select(h => new
                                {
                                    h.Id,
                                    Role = h.RoleName,
                                    Organization = h.OrganizationName,
                                    h.Action,
                                    h.At,
                                })
                                .ToListAsync(),
                        canManage = grants.Contains("members.manage"),
                        canEditPerson = own
                            || grants.Contains("members.manage") && p.UserId == null,
                        hasCurrentCard = await s.Db.DigitalCards.AnyAsync(x =>
                            x.MembershipId == id && x.RevokedAt == null
                        )
                            && m.Status == "Active"
                            && (m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow),
                        canCard = own || grants.Contains("members.cards"),
                        canDocuments = own || grants.Contains("members.documents"),
                    }
                );
            }
        );
        api.MapGet(
            "/members/{id:guid}/photo",
            async (Guid id, MembershipService s, bool directory = false) =>
            {
                var m = await s.Find(id, directory ? "directory.view" : "members.view");
                var u = await s.Access.User();
                Check(
                    m.Person.UserId == u.Id || m.Person.Privacy.PhotoVisible
                        || !directory && (await s.Access.Grants(await s.Access.Workspace("members.view"))).Contains("members.personal.view"),
                    "Photo is private.",
                    404
                );
                var photo = await s
                    .Db.Persons.Where(x => x.Id == m.PersonId)
                    .Select(x => new { x.Photo, x.PhotoType })
                    .SingleAsync();
                return photo.Photo == null
                    ? Results.NotFound()
                    : Results.File(photo.Photo, photo.PhotoType);
            }
        );
        api.MapPost(
            "/persons/me",
            async (MembershipService s) =>
            {
                var p = await s.OwnPerson(true);
                return Results.Ok(await s.Profile(p, true, true, true, true));
            }
        );
        api.MapPut(
            "/persons/me",
            async (PersonInput r, MembershipService s) =>
            {
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                var p = await s.OwnPerson();
                await s.Lock(p.Id);
                await s.UpdatePerson(p, r);
                foreach (var m in await s.Own((await s.Access.User()).Id).ToListAsync())
                    s.Activity(m, (await s.Access.User()).Id, "profile.updated");
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(await s.Profile(p, true, true, true, true));
            }
        );
        api.MapPut(
            "/persons/me/privacy",
            async (PrivacyInput r, MembershipService s) =>
            {
                var p = await s.OwnPerson();
                Check(p.Privacy.Version == r.Version, "Privacy preferences changed. Refresh.", 409);
                var v = p.Privacy;
                v.DirectoryVisible = r.DirectoryVisible;
                v.DisplayEmail = r.DisplayEmail;
                v.DisplayPhone = r.DisplayPhone;
                v.ProfessionalVisible = r.ProfessionalVisible;
                v.BirthdayAnnouncements = r.BirthdayAnnouncements;
                v.Marketing = r.Marketing;
                v.PhotoVisible = r.PhotoVisible;
                v.AllowContactRequests = r.AllowContactRequests;
                v.CardNameVisible = r.CardNameVisible;
                v.TransactionalEmail = r.TransactionalEmail;
                v.Version++;
                await s.Db.SaveChangesAsync();
                return Results.Ok(v);
            }
        );
        api.MapPut(
            "/members/{id:guid}/person",
            async (Guid id, PersonInput r, MembershipService s) =>
            {
                await using var tx = await s.Db.Database.BeginTransactionAsync();
                var m = await s.Find(id, "members.manage", false);
                var p = await s.People.SingleAsync(x => x.Id == m.PersonId);
                await s.Lock(p.Id);
                await s.UpdatePerson(p, r);
                s.Activity(m, (await s.Access.User()).Id, "profile.updated");
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.NoContent();
            }
        );
        api.MapPost(
            "/members",
            async (ManualMember r, MembershipService s) =>
            {
                var w = await s.Access.Workspace("members.manage");
                Check(
                    await s.Access.Visible(w).AnyAsync(x => x.Id == r.ClubId),
                    "Club outside scope.",
                    403
                );
                Check(new[]{"Active","Inactive","Suspended"}.Contains(r.Status),"Choose an active, inactive or suspended initial membership status.");
                Check(r.RequestKey!=null && (r.RequestKey==""||r.RequestKey.Length is >=8 and <=100),"Invalid registration request key.");
                var hash=Hash(System.Text.Json.JsonSerializer.Serialize(r));
                await using var tx = await s.Access.Mutation(w);
                if(r.RequestKey!="") {
                    var recorded=await s.Db.Audits.SingleOrDefaultAsync(x=>x.TenantId==w.TenantId&&x.ActorId==w.UserId&&x.Action=="member.registration.create"&&x.Resource==r.RequestKey);
                    if(recorded!=null){var saved=System.Text.Json.JsonDocument.Parse(recorded.Changes);Check(saved.RootElement.GetProperty("hash").GetString()==hash,"Registration request key was used with different details.",409);return Results.Ok(saved.RootElement.GetProperty("result").Clone());}
                }
                var club = await s.Club(r.ClubId, w.TenantId);
                var category = await s.Category(r.CategoryId, w.TenantId);
                Person person;
                if (r.PersonId != null)
                {
                    Check(
                        await s.Scoped(w).AnyAsync(x => x.PersonId == r.PersonId),
                        "Person outside scope.",
                        403
                    );
                    person = await s.People.SingleAsync(x => x.Id == r.PersonId);
                }
                else
                {
                    var duplicates = await DuplicateCandidates(s, w, r.Person);
                    Check(
                        duplicates.Count == 0 || r.DuplicateDecision == "CreateSeparate",
                        "Potential duplicates need review. Confirm CreateSeparate or select an authorized existing person.",
                        409
                    );
                    person = s.NewPerson(r.Person);
                }
                if (!string.IsNullOrEmpty(r.PhotoBase64))
                {
                    Check(r.PersonId == null, "Update an existing person's photo from their member profile.");
                    Check(r.PhotoBase64.Length <= 2796204, "Photo must be smaller than 2 MB.");
                    byte[] bytes;
                    try { bytes = Convert.FromBase64String(r.PhotoBase64); }
                    catch (FormatException) { throw new ApiError(400, "Invalid photo data."); }
                    Check(bytes.Length is > 0 and <= 2097152, "Photo must be smaller than 2 MB.");
                    var image = MemberMediaEndpoints.StripPhoto(bytes);
                    person.Photo = image.Bytes;
                    person.PhotoType = image.Type;
                }
                var member = await s.Activate(person, club, category, w.UserId, r.JoinedAt);
                if(r.Status!="Active")await s.ChangeStatus(member,r.Status,"Initial member registration",w.UserId);
                var result=new { id=member.Id,number=member.Number,personId=member.PersonId,version=member.Version,status=member.Status };
                if(r.RequestKey!="")await s.Access.Log(w,"member.registration.create",r.RequestKey,System.Text.Json.JsonSerializer.Serialize(new{hash,result}));
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(result);
            }
        );
        api.MapPost(
            "/members/duplicates",
            async (PersonInput r, MembershipService s) =>
            {
                var w = await s.Access.Workspace("members.manage");
                s.Validate(r);
                return await DuplicateCandidates(s, w, r);
            }
        );
        api.MapPost(
            "/members/{id:guid}/status",
            async (Guid id, StatusInput r, MembershipService s) =>
            {
                var w = await s.Access.Workspace("members.manage");
                await using var tx = await s.Access.Mutation(w);
                await s.Lock(id);
                var m = await s.Find(id, "members.manage", false);
                Check(m.Version == r.Version, "Membership changed. Refresh.", 409);
                await s.ChangeStatus(m, r.Status, r.Reason, w.UserId, r.EffectiveAt);
                await s.Db.SaveChangesAsync();
                await tx.CommitAsync();
                return Results.Ok(
                    new
                    {
                        m.Id,
                        m.Status,
                        m.Version,
                    }
                );
            }
        );
        api.MapGet(
            "/membership/me",
            async (MembershipService s) =>
            {
                var u = await s.Access.User();
                var p = await s.People.SingleOrDefaultAsync(x => x.UserId == u.Id);
                var memberships = s.Own(u.Id);
                return new
                {
                    profile = p == null ? null : await s.Profile(p, true, true, true, true),
                    memberships = await s.Rows(
                            memberships.OrderBy(x => x.Club.Name),
                            false,
                            true,
                            true,
                            u.Id
                        )
                        .ToListAsync(),
                    applications = await s
                        .Db.MembershipApplications.Where(x => x.ApplicantUserId == u.Id)
                        .OrderByDescending(x => x.CreatedAt)
                        .Select(x => new
                        {
                            x.Id,
                            x.ClubId,
                            Club = s
                                .Db.Orgs.Where(o => o.Id == x.ClubId)
                                .Select(o => o.Name)
                                .First(),
                            x.Status,
                            x.Version,
                            x.CurrentSequence,
                            x.CreatedAt,
                        })
                        .Take(100)
                        .ToListAsync(),
                    transfers = await s
                        .Db.MembershipTransfers.Where(x =>
                            memberships.Any(m => m.Id == x.MembershipId)
                        )
                        .OrderByDescending(x => x.CreatedAt)
                        .Take(50)
                        .ToListAsync(),
                    clubContacts = await s
                        .Db.Orgs.Where(x => memberships.Any(m => m.ClubId == x.Id))
                        .Select(x => new
                        {
                            x.Id,
                            x.Email,
                            x.Phone,
                            x.Website,
                        })
                        .ToListAsync(),
                    linkRequests = await s
                        .Db.PersonLinkRequests.Where(x => x.UserId == u.Id)
                        .Select(x => new
                        {
                            x.Id,
                            x.Status,
                            x.ReviewReason,
                        })
                        .Take(50)
                        .ToListAsync(),
                    notifications = await s
                        .Db.Mails.Where(x =>
                            x.UserId == u.Id
                            && x.Subject != ""
                            && x.ExpiresAt > DateTimeOffset.UtcNow
                        )
                        .OrderByDescending(x => x.NextAttemptAt)
                        .Select(x => new
                        {
                            x.Id,
                            x.Kind,
                            x.Subject,
                            x.Body,
                            Status = x.Suppressed ? "Email suppressed"
                            : x.SentAt != null ? "Sent"
                            : x.Attempts > 0 ? "Retrying"
                            : "Queued",
                            x.SentAt,
                        })
                        .Take(20)
                        .ToListAsync(),
                    contactRequests = await s
                        .Db.MemberContactRequests.Where(x =>
                            memberships.Any(m => m.Id == x.MembershipId)
                        )
                        .OrderByDescending(x => x.At)
                        .Select(x => new
                        {
                            x.Id,
                            x.Message,
                            x.Status,
                            x.At,
                            Requester = s
                                .Db.Users.Where(u => u.Id == x.RequesterId)
                                .Select(u => u.Name)
                                .First(),
                        })
                        .Take(50)
                        .ToListAsync(),
                };
            }
        );
        api.MapPost(
            "/directory/{id:guid}/contact-request",
            async (Guid id, ContactRequestInput r, MembershipService s, NotificationService n) =>
            {
                var w = await s.Access.Workspace("directory.view");
                var m = await s.Find(id, "directory.view", false);
                Check(
                    m.Person.Privacy.DirectoryVisible && m.Person.Privacy.AllowContactRequests,
                    "This member does not accept contact requests.",
                    403
                );
                Check(
                    r.Message.Length is >= 5 and <= 1000,
                    "Write a message of 5–1000 characters."
                );
                Check(
                    !await s.Db.MemberContactRequests.AnyAsync(x =>
                        x.RequesterId == w.UserId && x.MembershipId == id && x.Status == "Pending"
                    ),
                    "A contact request is already pending.",
                    409
                );
                var request = new MemberContactRequest
                {
                    TenantId = w.TenantId,
                    MembershipId = id,
                    RequesterId = w.UserId,
                    Message = r.Message,
                };
                s.Db.MemberContactRequests.Add(request);
                await n.User(
                    m.Person.UserId,
                    "contact.request",
                    "You have a new member contact request.",
                    "/app/my-membership",
                    m.TenantId,
                    m.ClubId
                );
                await s.Db.SaveChangesAsync();
                return Results.Ok(new { request.Id, request.Status });
            }
        );
        api.MapPost(
            "/membership/contact-requests/{id:guid}/respond",
            async (Guid id, ContactResponseInput r, MembershipService s, NotificationService n) =>
            {
                var u = await s.Access.User();
                var request = await s.Db.MemberContactRequests.SingleOrDefaultAsync(x =>
                    x.Id == id && s.Own(u.Id).Any(m => m.Id == x.MembershipId)
                );
                Check(request != null, "Contact request not found.", 404);
                Check(
                    r.Reply.Length <= 1000
                        && request.Status == "Pending"
                        && new[] { "Accepted", "Declined" }.Contains(r.Status),
                    "Invalid response."
                );
                request.Status = r.Status;
                await n.User(
                    request.RequesterId,
                    "contact.response",
                    "Your contact request was "
                        + r.Status.ToLowerInvariant()
                        + ". "
                        + (r.Status == "Accepted" ? r.Reply : "")
                );
                await s.Db.SaveChangesAsync();
                return Results.NoContent();
            }
        );
        api.MapGet(
            "/membership/notification-status",
            async (MembershipService s) =>
            {
                var w = await s.Access.Workspace("members.manage");
                return await s
                    .Db.Mails.Where(x =>
                        x.TenantId == w.TenantId
                        && s.Access.Visible(w).Any(o => o.Id == x.OrgId)
                        && x.Subject != ""
                    )
                    .OrderByDescending(x => x.NextAttemptAt)
                    .Take(100)
                    .Select(x => new
                    {
                        x.Id,
                        x.Subject,
                        Scope = s
                            .Db.Orgs.Where(o => o.Id == x.OrgId)
                            .Select(o => o.Name)
                            .FirstOrDefault(),
                        Attempts = s.Db.MailDeliveryAttempts.Count(a => a.MailId == x.Id),
                        Status = x.Suppressed ? "Suppressed"
                        : x.SentAt != null ? "Sent"
                        : x.Attempts > 0 ? "Retrying"
                        : "Queued",
                        LastAttempt = s
                            .Db.MailDeliveryAttempts.Where(a => a.MailId == x.Id)
                            .Max(a => (DateTimeOffset?)a.At),
                    })
                    .ToListAsync();
            }
        );
        api.MapGet(
            "/membership/views",
            async (MembershipService s) =>
            {
                var w = await s.Access.Workspace("members.view");
                return await s
                    .Db.MemberSavedViews.Where(x =>
                        x.UserId == w.UserId && x.TenantId == w.TenantId
                    )
                    .ToListAsync();
            }
        );
        api.MapPost(
            "/membership/views",
            async (SavedViewInput r, MembershipService s) =>
            {
                var w = await s.Access.Workspace("members.view");
                Check(
                    r.Name.Length is > 0 and <= 80 && r.Configuration.Length <= 10000,
                    "Invalid saved view."
                );
                try
                {
                    System.Text.Json.JsonDocument.Parse(r.Configuration);
                }
                catch
                {
                    throw new ApiError(400, "Invalid view configuration.");
                }
                var view = await s.Db.MemberSavedViews.SingleOrDefaultAsync(x =>
                    x.UserId == w.UserId && x.TenantId == w.TenantId && x.Name == r.Name
                );
                if (view == null)
                {
                    view = new()
                    {
                        TenantId = w.TenantId,
                        UserId = w.UserId,
                        Name = r.Name,
                    };
                    s.Db.MemberSavedViews.Add(view);
                }
                view.Configuration = r.Configuration;
                await s.Db.SaveChangesAsync();
                return Results.Ok(view);
            }
        );
        api.MapGet("/membership/metrics", Metrics);
    }

    public static async Task<List<object>> DuplicateCandidates(
        MembershipService s,
        Assignment w,
        PersonInput person
    )
    {
        var names = person.LegalName.Trim().ToLowerInvariant();
        var values = (person.Contacts ?? [])
            .Select(x => MembershipService.Normalize(x.Value, x.Kind))
            .ToArray();
        var ids = s.Scoped(w).Select(x => x.PersonId);
        return (
            await s
                .Db.Persons.Where(p =>
                    ids.Contains(p.Id)
                    && (
                        p.LegalName.ToLower() == names
                        || s.Db.PersonContacts.Any(c =>
                            c.PersonId == p.Id && values.Contains(c.NormalizedValue)
                        )
                    )
                )
                .Select(p => new
                {
                    p.Id,
                    Name = p.DisplayName,
                    Reason = p.LegalName.ToLower() == names
                        ? "Matching name"
                        : "Shared contact — review, never auto-merge",
                })
                .Take(20)
                .ToListAsync()
        ).Cast<object>().ToList();
    }

    public static IQueryable<Membership> Filter(
        MembershipService s,
        IQueryable<Membership> q,
        string? search,
        string? statuses,
        Guid? clubId,
        Guid? categoryId,
        string? city,
        string? profession,
        string? industry,
        string? expertise,
        string? office, Assignment w
    )
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            Check(search.Length <= 100, "Search is too long.");
            // Separate indexed searches avoid an OR across joined tables forcing a full joined scan.
            var names = s
                .Db.Persons.Where(p =>
                    EF.Functions.ILike(p.SearchName, "%" + search.ToLower() + "%")
                )
                .Select(p => p.Id);
            var numbers = s
                .Db.Memberships.Where(m => EF.Functions.ILike(m.Number, "%" + search + "%"))
                .Select(m => m.Id);
            q = q.Where(x => names.Contains(x.PersonId))
                .Union(q.Where(x => numbers.Contains(x.Id)));
        }
        if (!string.IsNullOrWhiteSpace(statuses))
        {
            var values = statuses.Split(',');
            var due = values.Contains("Renewal Due");
            q = q.Where(x =>
                values.Contains(x.Status)
                || due
                    && x.Status == "Active"
                    && x.EndsAt > DateTimeOffset.UtcNow
                    && x.EndsAt <= DateTimeOffset.UtcNow.AddDays(30)
            );
        }
        if (clubId != null)
            q = q.Where(x => x.ClubId == clubId);
        if (categoryId != null)
            q = q.Where(x => x.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(city))
            q = q.Where(x =>
                s.Db.PersonAddresses.Any(a =>
                    a.PersonId == x.PersonId && EF.Functions.ILike(a.City, "%" + city + "%")
                )
            );
        if (!string.IsNullOrWhiteSpace(profession))
            q = q.Where(x =>
                x.Person.Privacy.ProfessionalVisible
                && EF.Functions.ILike(x.Person.Profession, "%" + profession + "%")
            );
        if (!string.IsNullOrWhiteSpace(industry))
            q = q.Where(x =>
                x.Person.Privacy.ProfessionalVisible
                && EF.Functions.ILike(x.Person.Industry, "%" + industry + "%")
            );
        if (!string.IsNullOrWhiteSpace(expertise))
            q = q.Where(x =>
                x.Person.Privacy.ProfessionalVisible
                && s.Db.PersonTags.Any(t =>
                    t.PersonId == x.PersonId
                    && t.Kind == "Expertise"
                    && EF.Functions.ILike(t.Value, "%" + expertise + "%")
                )
            );
        if (!string.IsNullOrWhiteSpace(office))
            q = q.Where(x => GovernanceService.Current(s.Db).Any(o=>o.PersonId==x.PersonId&&o.TenantId==x.TenantId&&s.Access.Visible(w).Any(v=>v.Id==o.OrgId)&&EF.Functions.ILike(o.PositionTitle,"%"+office+"%")));
        return q;
    }

    private static async Task<IResult> List(
        MembershipService s,
        bool directory,
        string? search,
        string? statuses,
        Guid? clubId,
        Guid? categoryId,
        string? city,
        string? profession,
        string? industry,
        string? expertise,
        string? office,
        string sort,
        int page,
        int size,
        string? after,
        Guid? regionId = null, Guid? districtId = null, Guid? zoneId = null, string? membershipType = null, DateOnly? joinedFrom = null, DateOnly? joinedTo = null, DateOnly? renewalFrom = null, DateOnly? renewalTo = null, Guid? nationalId=null,string? country=null,string? state=null
    )
    {
        var w = await s.Access.Workspace(directory ? "directory.view" : "members.view");
        var grants = await s.Access.Grants(w);
        var q = (await s.ScopedList(w)).AsNoTracking();
        q = await RegionalDirectory.Filter(s, w, q, regionId);
        q = await MembershipPresentation.Filter(s,w,q,districtId,zoneId,membershipType,joinedFrom,joinedTo,renewalFrom,renewalTo,nationalId,country,state);
        if (directory)
            q = q.Where(x =>
                s.Db.PersonPrivacy.Any(p => p.PersonId == x.PersonId && p.DirectoryVisible)
                && x.Status == "Active"
                && (x.EndsAt == null || x.EndsAt > DateTimeOffset.UtcNow)
            );
        q = Filter(
            s,
            q,
            search,
            statuses,
            clubId,
            categoryId,
            city,
            profession,
            industry,
            expertise,
            office, w
        );
        var total = await q.CountAsync();
        if (!string.IsNullOrEmpty(after))
        {
            Check(sort == "number", "Cursor pagination requires number sort.");
            q = q.Where(x => x.Number.CompareTo(after) > 0);
        }
        var ordered = MembershipPresentation.Sort(q,sort);
        size = Math.Clamp(size, 1, 100);
        page = Math.Clamp(page, 1, 10000);
        var pageIds = await ordered
            .ThenBy(x => x.Id)
            .Skip(after == null ? (page - 1) * size : 0)
            .Take(size)
            .Select(x => x.Id)
            .ToArrayAsync();
        var rows = await s.Rows(
                q.Where(x => pageIds.Contains(x.Id)),
                directory,
                directory || grants.Contains("members.contacts.view"),
                true,
                w.UserId
            )
            .ToListAsync();
        var order = pageIds
            .Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index);
        rows = rows.OrderBy(x => order[x.Id]).ToList();
        var ids = rows.Select(x => x.PersonId).ToArray();
        var offices = await GovernanceService.Current(s.Db).Where(a=>ids.Contains(a.PersonId)&&a.TenantId==w.TenantId&&s.Access.Visible(w).Any(o=>o.Id==a.OrgId)).Select(a=>new {a.PersonId,Name=a.PositionTitle}).ToListAsync();
        var places = await RegionalDirectory.Places(s, w.TenantId, rows.Select(r => r.ClubId).ToArray());
        return Results.Ok(
            new
            {
                total,
                page,
                size,
                items = rows.Select(x => new
                {
                    x.Id,
                    x.PersonId,
                    x.TenantId,
                    x.Number,
                    x.Name,
                    x.ClubId,
                    x.Club,
                    places[x.ClubId].Region,
                    places[x.ClubId].District,
                    places[x.ClubId].Zone,
                    x.CategoryId,
                    x.Category,
                    x.Status,
                    x.City,
                    x.Country,
                    x.JoinedAt,
                    x.EndsAt,
                    x.UpdatedAt,
                    x.Version,
                    x.RenewalStatus,
                    x.Email,
                    x.Phone,
                    x.Profession,
                    x.Industry,
                    x.HasPhoto,
                    x.ContactRequestsAllowed,
                    Offices = offices
                        .Where(o => o.PersonId == x.PersonId)
                        .Select(o => o.Name)
                        .Distinct()
                        .ToArray(),
                }),
                nextCursor = sort == "number" ? rows.LastOrDefault()?.Number : null,
            }
        );
    }

    private static async Task<IResult> Metrics(MembershipService s, string? range, DateOnly? from, DateOnly? to)
    {
        var w = await s.Access.Workspace("members.view");
        var q = s.Scoped(w);
        var now = DateTimeOffset.UtcNow;
        var month = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var active = q.Where(x => x.Status == "Active" && (x.EndsAt == null || x.EndsAt > now));
        var cohort = await s
            .Db.Database.SqlQuery<CohortMetric>(
                $"WITH RECURSIVE orgscope AS (SELECT \"Id\" FROM \"Orgs\" WHERE \"Id\"={w.OrgId} AND \"TenantId\"={w.TenantId} UNION ALL SELECT o.\"Id\" FROM \"Orgs\" o JOIN orgscope p ON o.\"ParentId\"=p.\"Id\" WHERE o.\"TenantId\"={w.TenantId}), scoped AS(SELECT m.\"Id\",m.\"PersonId\" FROM \"Memberships\" m WHERE m.\"TenantId\"={w.TenantId} AND m.\"ClubId\" IN(SELECT \"Id\" FROM orgscope)), opening AS(SELECT DISTINCT ON(h.\"MembershipId\") h.\"MembershipId\",h.\"NewStatus\",h.\"EndsAt\" FROM \"MembershipHistory\" h JOIN scoped s ON s.\"Id\"=h.\"MembershipId\" WHERE h.\"EffectiveAt\"<={month} ORDER BY h.\"MembershipId\",h.\"EffectiveAt\" DESC,h.\"RecordedAt\" DESC), initial AS(SELECT DISTINCT s.\"PersonId\" FROM scoped s JOIN opening o ON o.\"MembershipId\"=s.\"Id\" WHERE o.\"NewStatus\"='Active' AND(o.\"EndsAt\" IS NULL OR o.\"EndsAt\">{month})), current AS(SELECT DISTINCT m.\"PersonId\" FROM \"Memberships\" m JOIN scoped s ON s.\"Id\"=m.\"Id\" WHERE m.\"Status\"='Active' AND(m.\"EndsAt\" IS NULL OR m.\"EndsAt\">{now})) SELECT(SELECT count(*)::int FROM initial) AS \"Beginning\",(SELECT count(*)::int FROM initial i JOIN current c ON c.\"PersonId\"=i.\"PersonId\") AS \"Retained\""
            )
            .SingleAsync();
        var totalActive = await active.Select(x => x.PersonId).Distinct().CountAsync();
        return Results.Ok(
            new
            {
                Intelligence = await MembershipPresentation.Intelligence(s,w,range,from,to),
                TotalMembers = await q.Select(x => x.PersonId).Distinct().CountAsync(),
                Memberships = await q.CountAsync(),
                ActiveMembers = totalActive,
                InactiveMembers = await q.Where(x => !active.Any(a => a.PersonId == x.PersonId))
                    .Select(x => x.PersonId)
                    .Distinct()
                    .CountAsync(),
                JoinedThisMonth = await q.GroupBy(x => x.PersonId)
                    .Where(g => g.Min(x => x.JoinedAt) >= month)
                    .CountAsync(),
                PendingApplications = await s.Db.MembershipApplications.CountAsync(x =>
                    x.TenantId == w.TenantId
                    && s.Access.Visible(w).Any(o => o.Id == x.ClubId)
                    && MembershipStatistics.PendingApplicationStates.Contains(x.Status)
                ),
                UpcomingRenewals = await active.CountAsync(x =>
                    x.EndsAt != null && x.EndsAt <= now.AddDays(30)
                ),
                RequiringAttention = await q.CountAsync(x =>
                    x.Status == "Suspended" || x.Status == "Expired" || x.FinancialHold
                ),
                RetentionPercent = cohort.Beginning == 0
                    ? (double?)null
                    : Math.Round(100d * cohort.Retained / cohort.Beginning, 2),
                GrowthPercent = cohort.Beginning == 0
                    ? (double?)null
                    : Math.Round(100d * (totalActive - cohort.Beginning) / cohort.Beginning, 2),
                cohort.Beginning,
                cohort.Retained,
                PeriodStart = month,
                AsOf = now,
                Categories = await q.GroupBy(x => x.Category.Name)
                    .Select(g => new { Name = g.Key, Count = g.Count() })
                    .ToListAsync(),
                Clubs = await q.GroupBy(x => new { x.ClubId, x.Club.Name })
                    .Select(g => new
                    {
                        g.Key.ClubId,
                        g.Key.Name,
                        Count = g.Select(x => x.PersonId).Distinct().Count(),
                        Active = g.Where(x =>
                                x.Status == "Active" && (x.EndsAt == null || x.EndsAt > now)
                            )
                            .Select(x => x.PersonId)
                            .Distinct()
                            .Count(),
                    })
                    .OrderByDescending(x => x.Count)
                    .Take(30)
                    .ToListAsync(),
                Countries = await q.GroupBy(x => x.Club.Country)
                    .Select(g => new
                    {
                        Name = g.Key,
                        Count = g.Select(x => x.PersonId).Distinct().Count(),
                    })
                    .Take(300)
                    .ToListAsync(),
                Trends = await q.Where(x => x.JoinedAt >= month.AddMonths(-5))
                    .GroupBy(x => new { x.JoinedAt.Year, x.JoinedAt.Month })
                    .Select(g => new
                    {
                        g.Key.Year,
                        g.Key.Month,
                        Joined = g.Count(),
                    })
                    .OrderBy(x => x.Year)
                    .ThenBy(x => x.Month)
                    .ToListAsync(),
            }
        );
    }
}

public class CohortMetric
{
    public int Beginning { get; set; }
    public int Retained { get; set; }
}

public record ContactRequestInput(string Message);

public record ContactResponseInput(string Status, string Reply = "");

public record SavedViewInput(string Name, string Configuration);
