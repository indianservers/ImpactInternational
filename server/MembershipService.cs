using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public record ContactInput(string Kind, string Label, string Value);

public record AddressInput(
    string Country,
    string State,
    string City,
    string PostalCode,
    string Address
);

public record PersonInput(
    string LegalName,
    string DisplayName,
    DateOnly? BirthDate = null,
    string Gender = "",
    DateOnly? Anniversary = null,
    string Company = "",
    string Designation = "",
    string Industry = "",
    string Profession = "",
    string BusinessCategory = "",
    string WorkLocation = "",
    string Website = "",
    string Networking = "",
    ContactInput[]? Contacts = null,
    AddressInput[]? Addresses = null,
    string[]? Skills = null,
    string[]? Expertise = null,
    string[]? Interests = null,
    string[]? Languages = null,
    long Version = 1,
    string MaritalStatus = "",
    string SpouseName = "",
    string EducationLevel = "",
    string Qualification = "",
    string Institution = "",
    string EmploymentStatus = "",
    string AdditionalInformation = "",
    Dictionary<string, string>? SocialLinks = null
);

public record MemberRow(
    Guid Id,
    Guid PersonId,
    Guid TenantId,
    string Number,
    string Name,
    Guid ClubId,
    string Club,
    Guid CategoryId,
    string Category,
    string Status,
    string City,
    string Country,
    DateTimeOffset JoinedAt,
    DateTimeOffset? EndsAt,
    DateTimeOffset UpdatedAt,
    long Version,
    string RenewalStatus,
    string? Email,
    string? Phone,
    string? Profession,
    string? Industry,
    bool HasPhoto,
    bool ContactRequestsAllowed
);

public class MembershipService(
    Access access,
    IDataProtectionProvider protection,
    NotificationService notifications
)
{
    public Db Db => access.Db;
    public Access Access => access;
    private readonly IDataProtector protector = protection.CreateProtector(
        "membership-card-and-invitation-v1"
    );

    public string Protect(string value) => protector.Protect(value);

    public string Unprotect(string value) => protector.Unprotect(value);

    public IQueryable<Membership> Scoped(Assignment w) =>
        Db.Memberships.Where(x =>
            x.TenantId == w.TenantId && access.VisibleIds(w).Contains(x.ClubId)
        );

    public async Task<IQueryable<Membership>> ScopedList(Assignment w)
    {
        var roots = await Db
            .Orgs.Where(o => o.TenantId == w.TenantId && o.ParentId == null)
            .Select(o => o.Id)
            .Take(2)
            .ToArrayAsync();
        if (roots.Length != 1 || roots[0] != w.OrgId)
            return Scoped(w);
        // Recheck the sole-root invariant in the data statement, preventing a hierarchy race.
        return Db.Memberships.Where(x =>
            x.TenantId == w.TenantId
            && Db.Orgs.Any(o => o.Id == w.OrgId && o.TenantId == w.TenantId && o.ParentId == null)
            && !Db.Orgs.Any(o => o.TenantId == w.TenantId && o.ParentId == null && o.Id != w.OrgId)
        );
    }

    public IQueryable<Membership> Own(Guid userId) =>
        Db.Memberships.Where(x => x.Person.UserId == userId);

    public IQueryable<Person> People =>
        Db
            .Persons.Include(x => x.Privacy)
            .Include(x => x.Contacts)
            .Include(x => x.Addresses)
            .Include(x => x.Tags)
            .AsSplitQuery();

    public async Task<Person> OwnPerson(bool create = false)
    {
        var u = await access.User();
        Check(u.Verified, "Verify your email before continuing.", 403);
        var person = await People.SingleOrDefaultAsync(x => x.UserId == u.Id);
        if (person != null)
            return person;
        Check(create, "Create your member profile first.", 404);
        person = new()
        {
            UserId = u.Id,
            LegalName = u.Name,
            DisplayName = u.Name,
            SearchName = u.Name.ToLowerInvariant(),
            Contacts =
            [
                new()
                {
                    Kind = "Email",
                    Value = u.Email,
                    NormalizedValue = u.Email,
                    Verified = true,
                },
            ],
        };
        Db.Persons.Add(person);
        await Db.SaveChangesAsync();
        return person;
    }

    public async Task<Membership> Find(
        Guid id,
        string permission = "members.view",
        bool allowOwn = true
    )
    {
        var u = await access.User();
        var own = allowOwn
            ? await Own(u.Id)
                .Include(x => x.Person)
                    .ThenInclude(p => p.Privacy)
                .Include(x => x.Club)
                .Include(x => x.Category)
                .SingleOrDefaultAsync(x => x.Id == id)
            : null;
        if (own != null)
            return own;
        var w = await access.Workspace(permission);
        return await Scoped(w)
                .Include(x => x.Person)
                    .ThenInclude(p => p.Privacy)
                .Include(x => x.Club)
                .Include(x => x.Category)
                .SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new ApiError(404, "Membership not found in your authorized scope.");
    }

    public async Task<Org> Club(Guid id, Guid? tenant = null)
    {
        return await Db.Orgs.SingleOrDefaultAsync(x =>
                x.Id == id
                && (tenant == null || x.TenantId == tenant)
                && x.Status == "Active"
                && Db.Levels.Any(l => l.Id == x.LevelId && l.IsClub)
            ) ?? throw new ApiError(400, "Choose an active club in this association.");
    }

    public async Task<MembershipCategory> Category(Guid id, Guid tenant) =>
        await Db.MembershipCategories.SingleOrDefaultAsync(x =>
            x.Id == id && x.TenantId == tenant && x.Active
        ) ?? throw new ApiError(400, "Invalid membership category.");

    public void Validate(PersonInput r)
    {
        SocialProfiles.Normalize(r.SocialLinks);
        Check(
            !string.IsNullOrWhiteSpace(r.LegalName) && r.LegalName.Length <= 200,
            "Legal name is required (maximum 200 characters)."
        );
        Check((r.DisplayName?.Length ?? 0) <= 200, "Display name is too long.");
        Check(new[] { "", "Single", "Married", "Widowed", "Divorced", "Separated", "Prefer not to say" }.Contains(r.MaritalStatus), "Invalid marital status.");
        Check(new[] { r.SpouseName, r.EducationLevel, r.Qualification, r.Institution, r.EmploymentStatus }.All(v => v != null && v.Length <= 200), "Profile fields must be at most 200 characters.");
        Check(r.AdditionalInformation != null && r.AdditionalInformation.Length <= 2000, "Additional information must be at most 2000 characters.");
        Check(r.Anniversary == null || (r.Anniversary >= new DateOnly(1900, 1, 1) && r.Anniversary <= DateOnly.FromDateTime(DateTime.UtcNow) && (r.BirthDate == null || r.Anniversary >= r.BirthDate)), "Invalid marriage anniversary date.");
        foreach (var a in r.Addresses ?? [])
            Check(a.PostalCode != null && a.PostalCode.Length <= 20 && (a.Country.Trim().Equals("India", StringComparison.OrdinalIgnoreCase) == false || string.IsNullOrWhiteSpace(a.PostalCode) || System.Text.RegularExpressions.Regex.IsMatch(a.PostalCode, @"^[1-9][0-9]{5}$")), "Enter a valid six-digit Indian PIN code.");

        Check(
            r.BirthDate == null
                || (
                    r.BirthDate >= new DateOnly(1900, 1, 1)
                    && r.BirthDate <= DateOnly.FromDateTime(DateTime.UtcNow)
                ),
            "Invalid birth date."
        );
        Check(
            (r.Contacts?.Length ?? 0) <= 12 && (r.Addresses?.Length ?? 0) <= 5,
            "Too many contact or address entries."
        );
        foreach (var c in r.Contacts ?? [])
        {
            Check(
                new[] { "Email", "Phone", "Emergency" }.Contains(c.Kind) && c.Value.Length <= 200,
                "Invalid contact."
            );
            if (c.Kind == "Email")
                Check(
                    System.Net.Mail.MailAddress.TryCreate(c.Value, out _),
                    "Invalid email address."
                );
            if (c.Kind == "Phone")
                Check(
                    System.Text.RegularExpressions.Regex.IsMatch(c.Value, @"^[+0-9 ()-]{5,30}$"),
                    "Invalid telephone number."
                );
        }
        foreach (var values in new[] { r.Skills, r.Expertise, r.Interests, r.Languages })
            Check(
                values == null
                    || values.Length <= 30 && values.All(x => x.Length is > 0 and <= 100),
                "Limit tags to 30 values of 100 characters each."
            );
        Check(
            string.IsNullOrEmpty(r.Website)
                || Uri.TryCreate(r.Website, UriKind.Absolute, out var website)
                    && website.Scheme is "https" or "http",
            "Invalid website URL."
        );
    }

    public static string Normalize(string value, string kind) =>
        kind == "Phone"
            ? new string(value.Where(char.IsDigit).ToArray())
            : value.Trim().ToLowerInvariant();

    public Person NewPerson(PersonInput r)
    {
        Validate(r);
        var p = new Person();
        ApplyFields(p, r);
        p.Contacts = (r.Contacts ?? [])
            .Select(x => new PersonContact
            {
                Kind = x.Kind,
                Label = x.Label,
                Value = x.Value.Trim(),
                NormalizedValue = Normalize(x.Value, x.Kind),
            })
            .ToList();
        p.Addresses = (r.Addresses ?? [])
            .Select(x => new PersonAddress
            {
                Country = x.Country,
                State = x.State,
                City = x.City,
                PostalCode = x.PostalCode,
                Address = x.Address,
            })
            .ToList();
        p.Tags = Tags(r);
        Db.Persons.Add(p);
        return p;
    }

    private static List<PersonTag> Tags(PersonInput r) =>
        new[]
        {
            ("Skill", r.Skills),
            ("Expertise", r.Expertise),
            ("Interest", r.Interests),
            ("Language", r.Languages),
        }
            .SelectMany(k =>
                (k.Item2 ?? [])
                    .Distinct()
                    .Select(v => new PersonTag { Kind = k.Item1, Value = v.Trim() })
            )
            .ToList();

    private static void ApplyFields(Person p, PersonInput r)
    {
        p.LegalName = r.LegalName.Trim();
        p.DisplayName = string.IsNullOrWhiteSpace(r.DisplayName)
            ? p.LegalName
            : r.DisplayName.Trim();
        p.SearchName = (p.DisplayName + " " + p.LegalName).ToLowerInvariant();
        p.BirthDate = r.BirthDate;
        p.Gender = r.Gender;
        p.MaritalStatus = r.MaritalStatus.Trim();
        p.SpouseName = r.SpouseName.Trim();
        p.EducationLevel = r.EducationLevel.Trim();
        p.Qualification = r.Qualification.Trim();
        p.Institution = r.Institution.Trim();
        p.EmploymentStatus = r.EmploymentStatus.Trim();
        p.AdditionalInformation = r.AdditionalInformation.Trim();
        p.Anniversary = r.Anniversary;
        p.Company = r.Company;
        p.Designation = r.Designation;
        p.Industry = r.Industry;
        p.Profession = r.Profession;
        p.BusinessCategory = r.BusinessCategory;
        p.WorkLocation = r.WorkLocation;
        p.Website = r.Website;
        p.Networking = r.Networking;
        if (r.SocialLinks != null) p.SocialLinksJson = System.Text.Json.JsonSerializer.Serialize(SocialProfiles.Normalize(r.SocialLinks));
        p.UpdatedAt = DateTimeOffset.UtcNow;
    }

    public async Task UpdatePerson(Person p, PersonInput r)
    {
        Validate(r);
        Check(r.Version == p.Version, "The profile changed. Refresh before editing.", 409);
        var u = await access.User();
        if (p.UserId != u.Id)
        {
            Check(
                p.UserId == null,
                "A linked member controls their verified personal profile.",
                403
            );
            var w = await access.Workspace("members.manage");
            Check(
                await Scoped(w).AnyAsync(x => x.PersonId == p.Id),
                "Person outside authorized scope.",
                403
            );
            Check(
                !await Db
                    .Memberships.Where(x => x.PersonId == p.Id)
                    .AnyAsync(x => !Scoped(w).Any(s => s.Id == x.Id)),
                "Shared identity changes require its verified owner.",
                403
            );
        }
        Db.PersonContacts.RemoveRange(p.Contacts);
        Db.PersonAddresses.RemoveRange(p.Addresses);
        Db.PersonTags.RemoveRange(p.Tags);
        await Db.SaveChangesAsync();
        ApplyFields(p, r);
        p.Contacts = (r.Contacts ?? [])
            .Select(x => new PersonContact
            {
                PersonId = p.Id,
                Kind = x.Kind,
                Label = x.Label,
                Value = x.Value.Trim(),
                NormalizedValue = Normalize(x.Value, x.Kind),
                Verified =
                    x.Kind == "Email"
                    && u.Verified
                    && Normalize(x.Value, x.Kind) == u.Email
                    && p.UserId == u.Id,
            })
            .ToList();
        p.Addresses = (r.Addresses ?? [])
            .Select(x => new PersonAddress
            {
                PersonId = p.Id,
                Country = x.Country,
                State = x.State,
                City = x.City,
                PostalCode = x.PostalCode,
                Address = x.Address,
            })
            .ToList();
        p.Tags = Tags(r);
        foreach (var tag in p.Tags)
            tag.PersonId = p.Id;
        Db.PersonContacts.AddRange(p.Contacts);
        Db.PersonAddresses.AddRange(p.Addresses);
        Db.PersonTags.AddRange(p.Tags);
        p.Version++;
        await Db.SaveChangesAsync();
    }

    public async Task<object> Profile(
        Person p,
        bool personal,
        bool contacts,
        bool professional,
        bool owner,
        bool directory = false
    )
    {
        var roles =
            p.UserId == null
                ? []
                : await (
                    from a in Db.Assignments
                    join r in Db.Roles on a.RoleId equals r.Id
                    join o in Db.Orgs on a.OrgId equals o.Id
                    where a.UserId == p.UserId
                    select new
                    {
                        Role = r.Name,
                        Organization = o.Name,
                        TenantId = a.TenantId,
                    }
                ).ToListAsync();
        return new
        {
            p.Id,
            Reference = "P-" + p.Id.ToString("N"),
            Name = p.DisplayName,
            LegalName = personal ? p.LegalName : null,
            p.DisplayName,
            BirthDate = personal ? p.BirthDate : null,
            Gender = personal ? p.Gender : null,
            MaritalStatus = personal ? p.MaritalStatus : null,
            SpouseName = personal ? p.SpouseName : null,
            EducationLevel = personal ? p.EducationLevel : null,
            Qualification = personal ? p.Qualification : null,
            Institution = personal ? p.Institution : null,
            EmploymentStatus = personal ? p.EmploymentStatus : null,
            AdditionalInformation = personal ? p.AdditionalInformation : null,
            Anniversary = personal ? p.Anniversary : null,
            Contacts = p
                .Contacts.Where(c =>
                    c.Kind == "Emergency"
                        ? personal
                        : contacts
                            && (
                                owner
                                || !directory
                                || (
                                    c.Kind == "Email"
                                        ? p.Privacy.DisplayEmail
                                        : p.Privacy.DisplayPhone
                                )
                            )
                )
                .Select(c => new
                {
                    c.Kind,
                    c.Label,
                    c.Value,
                    c.Verified,
                }),
            Addresses = p.Addresses.Select(a => new
            {
                a.Country,
                a.State,
                a.City,
                PostalCode = personal ? a.PostalCode : null,
                Address = personal ? a.Address : null,
            }),
            Professional = professional
                ? new
                {
                    p.Company,
                    p.Designation,
                    p.Industry,
                    p.Profession,
                    p.BusinessCategory,
                    p.WorkLocation,
                    p.Website,
                    p.Networking,
                    Skills = p.Tags.Where(x => x.Kind == "Skill").Select(x => x.Value),
                    Expertise = p.Tags.Where(x => x.Kind == "Expertise").Select(x => x.Value),
                    Interests = p.Tags.Where(x => x.Kind == "Interest").Select(x => x.Value),
                }
                : null,
            Languages = p.Tags.Where(x => x.Kind == "Language").Select(x => x.Value),
            SocialLinks = owner || personal || professional ? SocialProfiles.Read(p.SocialLinksJson) : null,
            Privacy = owner ? p.Privacy : null,
            p.Version,
            HasPhoto = p.Photo != null && (owner || personal && !directory || p.Privacy.PhotoVisible),
            ContactAllowed = p.Privacy.AllowContactRequests && p.Privacy.DirectoryVisible,
            Roles = owner ? roles : null,
        };
    }

    public IQueryable<MemberRow> Rows(
        IQueryable<Membership> q,
        bool directory,
        bool contacts,
        bool professional,
        Guid user
    )
    {
        return q.Select(m => new MemberRow(
            m.Id,
            m.PersonId,
            m.TenantId,
            m.Number,
            m.Person.DisplayName,
            m.ClubId,
            m.Club.Name,
            m.CategoryId,
            m.Category.Name,
            m.Status,
            Db.PersonAddresses.Where(a => a.PersonId == m.PersonId)
                .Select(a => a.City)
                .FirstOrDefault()
                ?? "",
            Db.PersonAddresses.Where(a => a.PersonId == m.PersonId)
                .Select(a => a.Country)
                .FirstOrDefault()
                ?? "",
            m.JoinedAt,
            m.EndsAt,
            m.UpdatedAt,
            m.Version,
            m.EndsAt == null ? "Not applicable"
                : m.EndsAt < DateTimeOffset.UtcNow ? "Expired"
                : m.EndsAt < DateTimeOffset.UtcNow.AddDays(30) ? "Due"
                : "Current",
            contacts && (!directory || m.Person.Privacy.DisplayEmail || m.Person.UserId == user)
                ? Db
                    .PersonContacts.Where(c => c.PersonId == m.PersonId && c.Kind == "Email")
                    .Select(c => c.Value)
                    .FirstOrDefault()
                : null,
            contacts && (!directory || m.Person.Privacy.DisplayPhone || m.Person.UserId == user)
                ? Db
                    .PersonContacts.Where(c => c.PersonId == m.PersonId && c.Kind == "Phone")
                    .Select(c => c.Value)
                    .FirstOrDefault()
                : null,
            professional && (m.Person.Privacy.ProfessionalVisible || m.Person.UserId == user)
                ? m.Person.Profession
                : null,
            professional && (m.Person.Privacy.ProfessionalVisible || m.Person.UserId == user)
                ? m.Person.Industry
                : null,
            m.Person.Photo != null && (!directory || m.Person.Privacy.PhotoVisible || m.Person.UserId == user),
            m.Person.Privacy.DirectoryVisible && m.Person.Privacy.AllowContactRequests
        ));
    }

    public async Task Lock(Guid id) =>
        await Db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({id.ToString()}))"
        );

    public async Task<string> Number() =>
        "IM-"
        + (
            await Db
                .Database.SqlQuery<long>($"SELECT nextval('membership_numbers') AS \"Value\"")
                .SingleAsync()
        ).ToString("D9");

    public async Task<Membership> Activate(
        Person person,
        Org club,
        MembershipCategory category,
        Guid actor,
        DateTimeOffset? joined = null,
        Guid? application = null,
        Guid? importRow = null,
        string? number = null
    )
    {
        await Lock(person.Id);
        var policy = await Db.MembershipPolicies.SingleOrDefaultAsync(x =>
            x.TenantId == club.TenantId
        );
        var existing = Db.Memberships.Where(x =>
            x.TenantId == club.TenantId
            && x.PersonId == person.Id
            && new[] { "Active", "Inactive", "Suspended", "Expired" }.Contains(x.Status)
        );
        Check(
            !await existing.AnyAsync(x => x.ClubId == club.Id && x.CategoryId == category.Id),
            "This person already has a membership in this club/category.",
            409
        );
        if (policy?.AllowMultipleClubs == false)
            Check(
                !await existing.AnyAsync(x => x.ClubId != club.Id),
                "Association policy permits only one current club affiliation.",
                409
            );
        var start = joined ?? DateTimeOffset.UtcNow;
        Check(start <= DateTimeOffset.UtcNow && start.Year >= 1900, "Invalid joining date.");
        var member = new Membership
        {
            TenantId = club.TenantId,
            PersonId = person.Id,
            ClubId = club.Id,
            CategoryId = category.Id,
            Number = number ?? await Number(),
            JoinedAt = start,
            EndsAt =
                category.ValidityMonths == null
                    ? null
                    : start.AddMonths(category.ValidityMonths.Value),
            SourceApplicationId = application,
            ImportRowId = importRow,
        };
        if (member.EndsAt < DateTimeOffset.UtcNow)
            member.Status = "Expired";
        Db.Memberships.Add(member);
        History(member, "", member.Status, "Membership activated", actor, start, application);
        Card(member);
        if (person.UserId != null && member.Status == "Active")
            await Grant(member, person.UserId.Value);
        await notifications.User(
            person.UserId,
            "membership.activated",
            $"Your {category.Name} membership at {club.Name} is now {member.Status.ToLowerInvariant()}.",
            "/app/my-membership",
            club.TenantId,
            club.Id
        );
        Activity(member, actor, "membership.activated", member.Number);
        return member;
    }

    public void History(
        Membership m,
        string old,
        string next,
        string reason,
        Guid actor,
        DateTimeOffset effective,
        Guid? approval = null
    ) =>
        Db.MembershipHistory.Add(
            new()
            {
                TenantId = m.TenantId,
                MembershipId = m.Id,
                OldStatus = old,
                NewStatus = next,
                CategoryId = m.CategoryId,
                EndsAt = m.EndsAt,
                Reason = reason,
                ActorId = actor,
                EffectiveAt = effective,
                ApprovalId = approval,
            }
        );

    public void Activity(Membership m, Guid actor, string action, string detail = "")
    {
        Db.MemberActivities.Add(
            new()
            {
                TenantId = m.TenantId,
                PersonId = m.PersonId,
                ClubId = m.ClubId,
                ActorId = actor,
                Action = action,
                Detail = detail,
            }
        );
        Db.Audits.Add(
            new()
            {
                TenantId = m.TenantId,
                OrgId = m.ClubId,
                ActorId = actor,
                Action = action,
                Resource = m.Id.ToString(),
            }
        );
    }

    public void Card(Membership m)
    {
        var token = Token();
        Db.DigitalCards.Add(
            new()
            {
                TenantId = m.TenantId,
                MembershipId = m.Id,
                TokenHash = Hash(token),
                ProtectedToken = Protect(token),
            }
        );
    }

    public async Task Grant(Membership m, Guid user)
    {
        var role = await Db
            .Roles.Include(x => x.Grants)
            .SingleOrDefaultAsync(x => x.TenantId == m.TenantId && x.Name == "Club Member");
        if (role == null)
        {
            role = new()
            {
                TenantId = m.TenantId,
                Name = "Club Member",
                Permissions = ["organization.view", "directory.view"],
            };
            Db.Roles.Add(role);
        }
        if (
            !await Db.Assignments.AnyAsync(x =>
                x.UserId == user && x.OrgId == m.ClubId && x.RoleId == role.Id
            )
        )
            Db.Assignments.Add(
                new()
                {
                    UserId = user,
                    TenantId = m.TenantId,
                    OrgId = m.ClubId,
                    RoleId = role.Id,
                    MembershipId = m.Id,
                }
            );
    }

    public async Task RevokeManaged(Membership m)
    {
        var other = await Db
            .Memberships.Where(x =>
                x.Id != m.Id
                && x.PersonId == m.PersonId
                && x.ClubId == m.ClubId
                && x.Status == "Active"
                && (x.EndsAt == null || x.EndsAt > DateTimeOffset.UtcNow)
            )
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync();
        if (other == null)
            await Db.Assignments.Where(x => x.MembershipId == m.Id).ExecuteDeleteAsync();
        else
            await Db
                .Assignments.Where(x => x.MembershipId == m.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.MembershipId, other));
    }

    public async Task ChangeStatus(
        Membership m,
        string status,
        string reason,
        Guid actor,
        DateTimeOffset? effective = null
    )
    {
        var allowed = new Dictionary<string, string[]>
        {
            {
                "Active",
                ["Expired", "Inactive", "Suspended", "Resigned", "Terminated", "Deceased"]
            },
            {
                "Inactive",
                ["Expired", "Active", "Suspended", "Resigned", "Terminated", "Deceased"]
            },
            { "Suspended", ["Active", "Inactive", "Resigned", "Terminated", "Deceased"] },
            { "Expired", ["Active", "Resigned", "Deceased"] },
        };
        Check(
            allowed.TryGetValue(m.Status, out var next) && next.Contains(status),
            "Invalid membership status transition.",
            409
        );
        Check(!string.IsNullOrWhiteSpace(reason) && reason.Length <= 2000, "A reason is required.");
        Check(
            status != "Expired" || m.EndsAt <= DateTimeOffset.UtcNow,
            "A current validity period cannot be marked expired."
        );
        var at = effective ?? DateTimeOffset.UtcNow;
        var last = await Db
            .MembershipHistory.Where(x => x.MembershipId == m.Id)
            .MaxAsync(x => (DateTimeOffset?)x.EffectiveAt);
        Check(
            at <= DateTimeOffset.UtcNow && at >= m.JoinedAt && (last == null || at >= last),
            "Effective date would contradict the recorded history."
        );
        Check(
            status != "Active" || m.EndsAt == null || m.EndsAt > DateTimeOffset.UtcNow,
            "Renew the expired validity period before activation."
        );
        var old = m.Status;
        m.Status = status;
        m.Version++;
        m.UpdatedAt = DateTimeOffset.UtcNow;
        History(m, old, status, reason, actor, at);
        Activity(m, actor, "membership.status", old + " → " + status);
        if (status != "Active")
        {
            await RevokeManaged(m);
            await Db
                .DigitalCards.Where(x => x.MembershipId == m.Id && x.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow));
        }
        else
        {
            if (m.Person.UserId != null)
                await Grant(m, m.Person.UserId.Value);
            if (!await Db.DigitalCards.AnyAsync(x => x.MembershipId == m.Id && x.RevokedAt == null))
                Card(m);
        }
    }
}
