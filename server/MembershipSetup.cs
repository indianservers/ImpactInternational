using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public static class MembershipSetup
{
    public static readonly string[] MemberPermissions =
    [
        "members.view",
        "members.manage",
        "members.contacts.view",
        "members.export",
        "members.import",
        "members.invite",
        "members.transfer.approve",
        "members.link",
        "members.documents",
        "members.cards",
        "directory.view",
    ];

    public static void Template(Role role)
    {
        var add =
            role.Name == "Association Super Admin" ? All
            : role.Name is "Club Member" or "Guest / Applicant" ? ["directory.view"]
            : MemberPermissions
                .Concat(
                    role.Name is "Club Secretary" or "Club President"
                        ? ["members.approve"]
                        : Array.Empty<string>()
                )
                .ToArray();
        if (role.Name is "Club Member" or "Applicant")
            add = add.Concat(new[] { "communications.view", "operations.view" }).ToArray();
        if (role.Name is "Club Secretary" or "Club President" or "Club Manager")
            add = add.Concat(
                    CommunicationService.Permissions.Where(p => p != "communications.configure")
                )
                .ToArray();
        if (role.Name is "Club Secretary" or "Club President" or "Club Manager")
            add = add.Concat(
                    GovernanceService.Permissions.Where(p =>
                        p is not "governance.configure" and not "governance.restructure"
                        && (
                            p != "governance.approve"
                            || role.Name is "Club Secretary" or "Club President"
                        )
                    )
                )
                .ToArray();
        if (role.Name is "Club Secretary" or "Club President" or "Club Manager")
            add = add.Concat(EventSetup.Permissions.Where(p => p != "events.configure")).ToArray();
        if (role.Name is "Club Member" or "Guest / Applicant")
            add = add.Concat(["events.view"]).ToArray();
        if (role.Name is "Club Secretary" or "Club President" or "Club Manager")
            add = add.Concat(OperationsService.Permissions).ToArray();
        role.Permissions = role.Permissions.Concat(add).Distinct().ToArray();
    }

    public static async Task Tenant(Db db, Guid tenant)
    {
        if (!await db.MembershipCategories.AnyAsync(x => x.TenantId == tenant))
            db.MembershipCategories.AddRange(
                new MembershipCategory
                {
                    TenantId = tenant,
                    Name = "Regular",
                    Code = "REG",
                    ValidityMonths = 12,
                },
                new MembershipCategory
                {
                    TenantId = tenant,
                    Name = "Honorary",
                    Code = "HON",
                },
                new MembershipCategory
                {
                    TenantId = tenant,
                    Name = "Young leader",
                    Code = "YTH",
                    ValidityMonths = 12,
                }
            );
        if (!await db.MembershipPolicies.AnyAsync(x => x.TenantId == tenant))
            db.MembershipPolicies.Add(new() { TenantId = tenant });
        if (
            !await db.MembershipWorkflows.AnyAsync(x =>
                x.TenantId == tenant && x.Kind == "Membership" && x.Active
            )
        )
        {
            var roles = await db.Roles.Where(x => x.TenantId == tenant).ToListAsync();
            var admin = roles.SingleOrDefault(x => x.Name == "Association Super Admin");
            if (admin != null)
            {
                var stages = new[]
                {
                    ("Secretary review", "Club Secretary"),
                    ("President approval", "Club President"),
                };
                db.MembershipWorkflows.Add(
                    new()
                    {
                        TenantId = tenant,
                        Stages = stages
                            .Select(
                                (s, i) =>
                                    new MembershipApprovalStage
                                    {
                                        TenantId = tenant,
                                        Name = s.Item1,
                                        Sequence = i + 1,
                                        Roles = roles
                                            .Where(x => x.Id == admin.Id || x.Name == s.Item2)
                                            .Select(r => new MembershipStageRole
                                            {
                                                TenantId = tenant,
                                                RoleId = r.Id,
                                            })
                                            .ToList(),
                                    }
                            )
                            .ToList(),
                    }
                );
            }
        }
        await db.SaveChangesAsync();
    }

    public static async Task Demo(IServiceProvider sp)
    {
        Check(
            sp.GetRequiredService<IHostEnvironment>().IsDevelopment(),
            "Membership demonstration seed is Development-only."
        );
        var db = sp.GetRequiredService<Db>();
        var service = sp.GetRequiredService<MembershipService>();
        var password =
            Environment.GetEnvironmentVariable("CLUBS_SEED_PASSWORD")
            ?? throw new InvalidOperationException("Set CLUBS_SEED_PASSWORD");
        Check(password.Length >= 12, "Seed password must be at least 12 characters.");
        foreach (var tenant in await db.Tenants.Select(x => x.Id).ToListAsync())
            await Tenant(db, tenant);
        var hasher = sp.GetRequiredService<IPasswordHasher<User>>();
        foreach (
            var (email, name) in new[]
            {
                ("secretary@example.test", "Leela Varma"),
                ("president@example.test", "Arjun Mehta"),
                ("applicant@example.test", "Isha Nair"),
            }
        )
            if (!await db.Users.AnyAsync(x => x.Email == email))
            {
                var u = new User
                {
                    Email = email,
                    Name = name,
                    Verified = true,
                };
                u.PasswordHash = hasher.HashPassword(u, password);
                db.Users.Add(u);
            }
        await db.SaveChangesAsync();
        var club = await db.Orgs.SingleOrDefaultAsync(x => x.Code == "GC-006");
        if (club == null)
            return;
        foreach (
            var (email, roleName) in new[]
            {
                ("secretary@example.test", "Club Secretary"),
                ("president@example.test", "Club President"),
            }
        )
        {
            var user = await db.Users.SingleAsync(x => x.Email == email);
            var role = await db.Roles.SingleAsync(x =>
                x.TenantId == club.TenantId && x.Name == roleName
            );
            if (
                !await db.Assignments.AnyAsync(x =>
                    x.UserId == user.Id && x.RoleId == role.Id && x.OrgId == club.Id
                )
            )
                db.Assignments.Add(
                    new()
                    {
                        TenantId = club.TenantId,
                        UserId = user.Id,
                        RoleId = role.Id,
                        OrgId = club.Id,
                    }
                );
        }
        await db.SaveChangesAsync();
        var category = await db.MembershipCategories.SingleAsync(x =>
            x.TenantId == club.TenantId && x.Code == "REG"
        );
        var actor = (await db.Users.SingleAsync(x => x.Email == "admin@example.test")).Id;
        foreach (
            var email in new[]
            {
                "admin@example.test",
                "manager@example.test",
                "member@example.test",
                "secretary@example.test",
                "president@example.test",
            }
        )
        {
            var user = await db.Users.SingleAsync(x => x.Email == email);
            var p = await db.Persons.SingleOrDefaultAsync(x => x.UserId == user.Id);
            if (p == null)
            {
                p = new()
                {
                    UserId = user.Id,
                    LegalName = user.Name,
                    DisplayName = user.Name,
                    SearchName = user.Name.ToLowerInvariant(),
                    Profession = email.StartsWith("member")
                        ? "Community designer"
                        : "Community leadership",
                    Industry = "Social impact",
                    Company = "Fictional Community Partners",
                    Contacts =
                    [
                        new()
                        {
                            Kind = "Email",
                            Value = user.Email,
                            NormalizedValue = user.Email,
                            Verified = true,
                        },
                    ],
                    Addresses = [new() { City = "Vijayawada", Country = "India" }],
                    Privacy = new()
                    {
                        DirectoryVisible = true,
                        ProfessionalVisible = true,
                        DisplayEmail = true,
                        CardNameVisible = true,
                    },
                };
                db.Persons.Add(p);
                await db.SaveChangesAsync();
            }
            if (!await db.Memberships.AnyAsync(x => x.PersonId == p.Id && x.ClubId == club.Id))
            {
                await using var tx = await db.Database.BeginTransactionAsync();
                var m = await service.Activate(
                    p,
                    club,
                    category,
                    actor,
                    DateTimeOffset.UtcNow.AddMonths(-6)
                );
                var assignment = await db.Assignments.SingleOrDefaultAsync(x =>
                    x.UserId == user.Id
                    && x.OrgId == club.Id
                    && db.Roles.Any(r => r.Id == x.RoleId && r.Name == "Club Member")
                );
                if (assignment != null)
                    assignment.MembershipId = m.Id;
                await db.SaveChangesAsync();
                await tx.CommitAsync();
            }
        }
        var names = new[]
        {
            "Aarav Sharma",
            "Diya Reddy",
            "Kabir Desai",
            "Meera Iyer",
            "Rohan Kapoor",
            "Sana Ali",
            "Vivaan Gupta",
            "Anika Das",
            "Reyansh Shah",
            "Tara Menon",
            "Aditya Bose",
            "Nisha Pillai",
            "Dev Patel",
            "Mira Joshi",
            "Kavya Rao",
            "Zoya Khan",
            "Ishan Verma",
            "Priya Chandra",
            "Neel Malhotra",
            "Riya Bhat",
        };
        var clubs = await db
            .Orgs.Where(x =>
                x.TenantId == club.TenantId
                && x.Status == "Active"
                && db.Levels.Any(l => l.Id == x.LevelId && l.IsClub)
            )
            .OrderBy(x => x.Code)
            .ToListAsync();
        foreach (var (name, index) in names.Select((x, i) => (x, i)))
        {
            var email = "fictional." + index + "@example.test";
            if (await db.PersonContacts.AnyAsync(x => x.Value == email))
                continue;
            var p = service.NewPerson(
                new(
                    name,
                    name,
                    Company: "Fictional Civic Studio",
                    Profession: index % 2 == 0 ? "Architect" : "Educator",
                    Industry: index % 2 == 0 ? "Design" : "Education",
                    Contacts:
                    [
                        new("Email", "Personal", email),
                        new("Phone", "Personal", "+910000000" + (index % 2).ToString("D3")),
                    ],
                    Addresses:
                    [
                        new(
                            "India",
                            "Andhra Pradesh",
                            index % 2 == 0 ? "Vijayawada" : "Visakhapatnam",
                            "",
                            "Fictional demonstration address"
                        ),
                    ],
                    Skills: ["Community building"],
                    Expertise: ["Leadership"]
                )
            );
            p.Privacy.DirectoryVisible = index % 5 != 0;
            p.Privacy.ProfessionalVisible = true;
            p.Privacy.CardNameVisible = true;
            await using var tx = await db.Database.BeginTransactionAsync();
            var m = await service.Activate(
                p,
                clubs[index % clubs.Count],
                category,
                actor,
                index < 3
                    ? DateTimeOffset.UtcNow.AddDays(-index)
                    : DateTimeOffset.UtcNow.AddMonths(-3).AddDays(index)
            );
            await db.SaveChangesAsync();
            if (index % 7 == 0)
            {
                m.Person = p;
                await service.ChangeStatus(m, "Inactive", "Development demonstration", actor);
                await db.SaveChangesAsync();
            }
            await tx.CommitAsync();
        }
    }
}
