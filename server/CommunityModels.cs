using Microsoft.EntityFrameworkCore;
namespace Clubs;

public class CommunityPost : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrgId { get; set; }
    public Guid AuthorId { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Category { get; set; } = "Club activity";
    public DateOnly ActivityOn { get; set; }
    public int Participants { get; set; }
    public decimal VolunteerHours { get; set; }
    public string Location { get; set; } = "";
    public string LinkUrl { get; set; } = "";
    public bool Pinned { get; set; }
    public string Status { get; set; } = "Published";
    public string RequestKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public class CommunityComment : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid PostId { get; set; }
    public Guid AuthorId { get; set; }
    public string Body { get; set; } = "";
    public bool Hidden { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public class CommunityReaction : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public partial class Db
{
    public DbSet<CommunityPost> CommunityPosts => Set<CommunityPost>();
    public DbSet<CommunityComment> CommunityComments => Set<CommunityComment>();
    public DbSet<CommunityReaction> CommunityReactions => Set<CommunityReaction>();
    static void ConfigureCommunity(ModelBuilder b)
    {
        b.Entity<CommunityPost>().HasAlternateKey(p => new { p.TenantId, p.Id });
        b.Entity<CommunityPost>().HasOne<Org>().WithMany().HasForeignKey(p => new { p.TenantId, p.OrgId }).HasPrincipalKey(o => new { o.TenantId, o.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CommunityPost>().HasOne<User>().WithMany().HasForeignKey(p => p.AuthorId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CommunityPost>().Property(p => p.Version).IsConcurrencyToken();
        b.Entity<CommunityPost>().HasIndex(p => new { p.TenantId, p.OrgId, p.Status, p.ActivityOn });
        b.Entity<CommunityPost>().HasIndex(p => new { p.TenantId, p.AuthorId, p.RequestKey }).IsUnique();
        b.Entity<CommunityComment>().HasOne<CommunityPost>().WithMany().HasForeignKey(c => new { c.TenantId, c.PostId }).HasPrincipalKey(p => new { p.TenantId, p.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CommunityComment>().HasOne<User>().WithMany().HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CommunityComment>().HasIndex(c => new { c.PostId, c.CreatedAt });
        b.Entity<CommunityReaction>().HasOne<CommunityPost>().WithMany().HasForeignKey(c => new { c.TenantId, c.PostId }).HasPrincipalKey(p => new { p.TenantId, p.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CommunityReaction>().HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CommunityReaction>().HasIndex(c => new { c.PostId, c.UserId }).IsUnique();
    }
}
