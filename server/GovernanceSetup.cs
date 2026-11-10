using Microsoft.EntityFrameworkCore;

namespace Clubs;

public static class GovernanceSetup
{
    public static async Task Tenant(Db db, Guid tenant)
    {
        if (!await db.ClubHealthConfigurations.AnyAsync(x => x.TenantId == tenant))
            db.ClubHealthConfigurations.Add(
                new() { TenantId = tenant, RequireClubChangeApproval = false }
            );
        if (!await db.CommitteeRoles.AnyAsync(x => x.TenantId == tenant))
            db.CommitteeRoles.AddRange(
                new CommitteeRole
                {
                    TenantId = tenant,
                    Name = "Chairperson",
                    CanManage = true,
                },
                new CommitteeRole
                {
                    TenantId = tenant,
                    Name = "Vice Chairperson",
                    CanManage = true,
                },
                new CommitteeRole
                {
                    TenantId = tenant,
                    Name = "Secretary",
                    CanManage = false,
                },
                new CommitteeRole { TenantId = tenant, Name = "Member" }
            );
        if (!await db.OfficerPositions.AnyAsync(x => x.TenantId == tenant))
        {
            foreach (var level in await db.Levels.Where(x => x.TenantId == tenant).ToListAsync())
            {
                string[] titles = level.IsClub
                    ?
                    [
                        "President",
                        "Immediate Past President",
                        "President Elect",
                        "Vice President",
                        "Secretary",
                        "Joint Secretary",
                        "Treasurer",
                        "Membership Director",
                        "Service Director",
                        "Event Director",
                        "Public Relations Officer",
                        "Training Director",
                        "Board Director",
                    ]
                    : level.Name.ToLowerInvariant() switch
                    {
                        "zone" => ["Zone Chairperson", "Zone Coordinator", "Zone Secretary"],
                        "region" =>
                        [
                            "Regional Chairperson",
                            "Regional Secretary",
                            "Regional Coordinator",
                        ],
                        "district" =>
                        [
                            "District Governor",
                            "Vice District Governor",
                            "District Secretary",
                            "District Treasurer",
                            "District Membership Chair",
                            "District Committee Chair",
                        ],
                        "national" =>
                        [
                            "National President",
                            "National Vice President",
                            "National Secretary",
                            "National Treasurer",
                            "National Director",
                        ],
                        "international" =>
                        [
                            "International President",
                            "International Vice President",
                            "International Secretary",
                            "International Director",
                            "International Committee Chair",
                        ],
                        _ => ["Chairperson", "Secretary", "Treasurer"],
                    };
                for (int i = 0; i < titles.Length; i++)
                    db.OfficerPositions.Add(
                        new()
                        {
                            TenantId = tenant,
                            LevelId = level.Id,
                            Title = titles[i],
                            Code =
                                level.Id.ToString("N")[..8].ToUpper()
                                + "-"
                                + (i + 1).ToString("00"),
                            Mandatory = level.IsClub && titles[i] is "President" or "Secretary",
                            MaximumHolders = titles[i] == "Board Director" ? null : 1,
                        }
                    );
            }
        }
        if (
            !await db.MembershipWorkflows.AnyAsync(x =>
                x.TenantId == tenant && x.Kind == "Governance" && x.Active
            )
        )
        {
            var roles = await db
                .Roles.Where(x =>
                    x.TenantId == tenant
                    && db.RolePermissions.Any(g =>
                        g.RoleId == x.Id && g.PermissionId == "governance.approve"
                    )
                )
                .Select(x => x.Id)
                .ToArrayAsync();
            if (roles.Length > 0)
                db.MembershipWorkflows.Add(
                    new()
                    {
                        TenantId = tenant,
                        Kind = "Governance",
                        Stages =
                        [
                            new()
                            {
                                TenantId = tenant,
                                Sequence = 1,
                                Name = "Authorized governance review",
                                RequiredApprovals = 1,
                                Roles = roles
                                    .Select(id => new MembershipStageRole
                                    {
                                        TenantId = tenant,
                                        RoleId = id,
                                    })
                                    .ToList(),
                            },
                        ],
                    }
                );
        }
        await db.SaveChangesAsync();
    }

    public static async Task Initialize(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Db>();
        foreach (var id in await db.Tenants.Select(x => x.Id).ToArrayAsync())
            await Tenant(db, id);
    }
}
