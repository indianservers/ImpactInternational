using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static Clubs.Access;

namespace Clubs;

public static class Operations
{
    public static async Task<Tenant> Provision(string name, Access a)
    {
        var user = await a.User();
        Check(user.PlatformAdmin, "Platform administrator required.", 403);
        throw new ApiError(409, "This app is dedicated to Impact International. Create clubs and units within the existing organization.");
    }

    public static async Task<IResult> Save(Org r, Guid? id, Access a, GovernanceService governance)
    {
        var w = await a.Workspace(id == null ? "organization.create" : "organization.update");
        if (id == null && string.IsNullOrWhiteSpace(r.Code)) r.Code = "IM-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        Check(
            (r.Name?.Trim().Length ?? 0) is >= 2 and <= 200
                && (r.Code?.Length ?? 0) is > 0 and <= 40,
            "Name and code required."
        );
        Check(new[] { "Active", "Pending", "Archived" }.Contains(r.Status), "Invalid status.");
        Check(
            string.IsNullOrEmpty(r.Email) || System.Net.Mail.MailAddress.TryCreate(r.Email, out _),
            "Invalid email."
        );
        await using var tx = await a.Db.Database.BeginTransactionAsync();
        await a.Db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({w.TenantId.ToString()}))"
        );
        var level = await a.Db.Levels.SingleOrDefaultAsync(x =>
            x.Id == r.LevelId && x.TenantId == w.TenantId
        );
        Check(level != null, "Invalid level.");
        var platformAdmin = (await a.User()).PlatformAdmin;
        Check(r.ParentId == null || ImpactStructure.Kinds.Contains(level!.Name), "Only National units, Regions, Districts, Zones, and Clubs can be created within Impact International.");
        if (r.ParentId != null)
        {
            var p = await a.Visible(w).AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.ParentId);
            Check(p != null, "Parent outside authorized scope.", 403);
            Check(level!.ParentLevelId == p!.LevelId, "Invalid parent type.");
        }
        else
            Check(level!.ParentLevelId == null, "Parent required.");
        Org entity;
        if (id != null)
        {
            entity =
                await a.Visible(w).SingleOrDefaultAsync(x => x.Id == id)
                ?? throw new ApiError(404, "Organization not found.");
            if (await a.Db.Levels.AnyAsync(l => l.Id == entity.LevelId && l.IsClub))
            {
                var needsApproval = await a.Db.ClubHealthConfigurations.AnyAsync(c =>
                    c.TenantId == w.TenantId && c.RequireClubChangeApproval
                );
                Check(platformAdmin || !needsApproval, "Submit club changes through governance approval.", 409);
                Check(
                    platformAdmin || entity.ParentId == r.ParentId,
                    "Use the controlled organization restructuring workflow.",
                    409
                );
            }
            Check(entity.Version == r.Version, "Record changed. Refresh.", 409);
            Check(entity.LevelId == r.LevelId, "Existing level cannot be changed.");
            Check(
                platformAdmin || entity.ParentId == r.ParentId,
                "Use the controlled organization restructuring workflow.",
                409
            );
            if (r.ParentId != null)
                Check(
                    !await a.Tree(w.TenantId, id.Value).AnyAsync(x => x.Id == r.ParentId),
                    "Hierarchy cycle rejected."
                );
            Check(
                entity.ParentId == r.ParentId || id != w.OrgId,
                "Cannot move your own scope root.",
                403
            );
        }
        else
        {
            Check(r.ParentId != null, "Impact International is already configured. Create a National unit, Region, District, Zone, or Club.");
            entity = new() { TenantId = w.TenantId };
            a.Db.Orgs.Add(entity);
        }
        var before = new
        {
            entity.Name,
            entity.Code,
            entity.Status,
            entity.ParentId,
        };
        Check(id == null || entity.ParentId != null || r.Name?.Trim() == ImpactSetup.Name,
            "The root organization must remain Impact International.");
        entity.Name = r.Name!.Trim();
        entity.Code = r.Code!.Trim().ToUpperInvariant();
        entity.ParentId = r.ParentId;
        entity.LevelId = r.LevelId;
        entity.ShortName = r.ShortName;
        entity.Country = r.Country;
        entity.State = r.State;
        entity.City = r.City;
        entity.Address = r.Address;
        entity.Email = r.Email;
        entity.Phone = r.Phone;
        entity.Website = r.Website;
        entity.Logo = r.Logo;
        entity.CharterDate = r.CharterDate;
        entity.TimeZone = r.TimeZone;
        entity.Currency = r.Currency;
        if (id != null && entity.Status != r.Status)
        {
            var grants = await a.Grants(w);
            Check(
                r.Status != "Archived" || grants.Contains("organization.archive"),
                "Archive permission required.",
                403
            );
            Check(
                entity.Status != "Pending"
                    || r.Status != "Active"
                    || grants.Contains("organization.approve"),
                "Approval permission required.",
                403
            );
        }
        if (id != null && level.IsClub && !platformAdmin)
            Check(
                entity.Status == r.Status,
                "Submit club lifecycle changes through governance approval.",
                409
            );
        entity.Status = id == null && level.IsClub && !platformAdmin ? "Pending" : r.Status;
        entity.Version++;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await a.Log(
            w,
            id == null ? "organization.create" : "organization.update",
            entity.Id.ToString(),
            System.Text.Json.JsonSerializer.Serialize(
                new
                {
                    before,
                    after = new
                    {
                        entity.Name,
                        entity.Code,
                        entity.Status,
                        entity.ParentId,
                    },
                }
            )
        );
        if (id == null && level.IsClub && !platformAdmin)
        {
            await governance.Request(
                w,
                entity.Id,
                "ClubStatus",
                "Approve new club: " + entity.Name,
                GovernanceService.Pack(
                    new ClubStatusInput("Active", "New club activation", entity.Version)
                )
            );
            await a.Db.SaveChangesAsync();
        }
        await tx.CommitAsync();
        return Results.Ok(entity);
    }
}

public static class Seed
{
    public static async Task Run(IServiceProvider sp)
    {
        var env = sp.GetRequiredService<IHostEnvironment>();
        Check(env.IsDevelopment(), "Development seed is forbidden outside Development.");
        var db = sp.GetRequiredService<Db>();
        if (await db.Users.AnyAsync())
            return;
        var password =
            Environment.GetEnvironmentVariable("CLUBS_SEED_PASSWORD")
            ?? throw new Exception("Set CLUBS_SEED_PASSWORD (12+ characters)");
        Check(password.Length >= 12, "Seed password too short.");
        var h = sp.GetRequiredService<IPasswordHasher<User>>();
        User Make(string email, string name, bool admin = false)
        {
            var u = new User
            {
                Email = email,
                Name = name,
                Verified = true,
                PlatformAdmin = admin,
            };
            u.PasswordHash = h.HashPassword(u, password);
            db.Users.Add(u);
            return u;
        }
        var admin = Make("admin@example.test", "Ananya Rao", true);
        var manager = Make("manager@example.test", "Ravi Kumar");
        var member = Make("member@example.test", "Maya Sen");
        foreach (var second in new[] { false, true })
        {
            var t = new Tenant
            {
                Name = second ? "Civic Futures Network" : "Global Community Leadership Association",
                Currency = "INR",
                TimeZone = "Asia/Kolkata",
            };
            db.Tenants.Add(t);
            var names = second
                ? new[] { "Headquarters", "Chapter" }
                : new[] { "International", "National", "Region", "District", "Zone", "Club" };
            Level? prev = null;
            var levels = new List<Level>();
            foreach (var name in names)
            {
                var l = new Level
                {
                    TenantId = t.Id,
                    Name = name,
                    ParentLevelId = prev?.Id,
                    IsClub = name is "Club" or "Chapter",
                };
                db.Levels.Add(l);
                levels.Add(l);
                prev = l;
            }
            var roleNames = new[]
            {
                "Association Super Admin",
                "International Administrator",
                "National Administrator",
                "District Administrator",
                "Regional Administrator",
                "Zone Administrator",
                "Club President",
                "Club Secretary",
                "Club Treasurer",
                "Club Manager",
                "Club Member",
                "Guest / Applicant",
            };
            var roles = roleNames
                .Select(name => new Role
                {
                    TenantId = t.Id,
                    Name = name,
                    Permissions =
                        name == "Association Super Admin" ? All
                        : name is "Club Member" or "Guest / Applicant" ? ["organization.view"]
                        :
                        [
                            "organization.view",
                            "organization.create",
                            "organization.update",
                            "users.view",
                            "reports.view",
                        ],
                })
                .ToArray();
            foreach (var role in roles)
                MembershipSetup.Template(role);
            db.Roles.AddRange(roles);
            Org? parent = null;
            var path = second
                ? new[] { "Civic Futures Headquarters", "Hyderabad Chapter" }
                : new[]
                {
                    "International Headquarters",
                    "India National Organization",
                    "South India Region",
                    "District 1",
                    "Zone A",
                    "Vijayawada Community Club",
                };
            var units = new List<Org>();
            for (int i = 0; i < path.Length; i++)
            {
                var o = new Org
                {
                    TenantId = t.Id,
                    LevelId = levels[i].Id,
                    ParentId = parent?.Id,
                    Name = path[i],
                    Code = $"{(second ? "CF" : "GC")}-{i + 1:000}",
                    Country = "India",
                    City = i == path.Length - 1 ? "Vijayawada" : "",
                    Currency = "INR",
                    TimeZone = "Asia/Kolkata",
                    CharterDate = new DateOnly(2020 + i, 1, 12),
                };
                db.Orgs.Add(o);
                units.Add(o);
                parent = o;
            }
            db.Assignments.Add(
                new()
                {
                    TenantId = t.Id,
                    UserId = admin.Id,
                    OrgId = units[0].Id,
                    RoleId = roles[0].Id,
                }
            );
            if (!second)
            {
                var club = units[^1];
                var zoneB = new Org
                {
                    TenantId = t.Id,
                    LevelId = levels[^2].Id,
                    ParentId = units[^3].Id,
                    Name = "Zone B",
                    Code = "GC-ZB",
                    Country = "India",
                };
                db.Orgs.Add(zoneB);
                foreach (
                    var name in new[] { "Guntur Leadership Club", "Visakhapatnam Service Club" }
                )
                    db.Orgs.Add(
                        new()
                        {
                            TenantId = t.Id,
                            ParentId = name.StartsWith("Guntur") ? club.ParentId : zoneB.Id,
                            LevelId = club.LevelId,
                            Name = name,
                            Code = name.StartsWith("Guntur") ? "GC-007" : "GC-008",
                            Country = "India",
                            City = name.Split(' ')[0],
                            Status = name.StartsWith("Guntur") ? "Pending" : "Active",
                        }
                    );
                db.Assignments.AddRange(
                    new Assignment
                    {
                        TenantId = t.Id,
                        UserId = manager.Id,
                        OrgId = club.Id,
                        RoleId = roles.Single(x => x.Name == "Club Manager").Id,
                    },
                    new Assignment
                    {
                        TenantId = t.Id,
                        UserId = member.Id,
                        OrgId = club.Id,
                        RoleId = roles.Single(x => x.Name == "Club Member").Id,
                    },
                    new Assignment
                    {
                        TenantId = t.Id,
                        UserId = admin.Id,
                        OrgId = club.Id,
                        RoleId = roles.Single(x => x.Name == "Club Member").Id,
                    }
                );
            }
            db.Audits.Add(
                new()
                {
                    TenantId = t.Id,
                    OrgId = units[0].Id,
                    ActorId = admin.Id,
                    Action = "association.provisioned",
                    Resource = t.Name,
                }
            );
        }
        await db.SaveChangesAsync();
    }
}
