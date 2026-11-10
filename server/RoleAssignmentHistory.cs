using Microsoft.EntityFrameworkCore;

namespace Clubs;

public class RoleAssignmentHistory : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public Guid OrgId { get; set; }
    public string RoleName { get; set; } = "";
    public string OrganizationName { get; set; } = "";
    public string Action { get; set; } = "";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public partial class Db
{
    public DbSet<RoleAssignmentHistory> RoleAssignmentHistory => Set<RoleAssignmentHistory>();

    private void ConfigureRoleHistory(ModelBuilder b)
    {
        b.Entity<RoleAssignmentHistory>()
            .HasOne<Role>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.RoleId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<RoleAssignmentHistory>()
            .HasOne<Org>()
            .WithMany()
            .HasForeignKey(x => new { x.TenantId, x.OrgId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<RoleAssignmentHistory>()
            .HasIndex(x => new
            {
                x.UserId,
                x.TenantId,
                x.At,
            });
    }
}
