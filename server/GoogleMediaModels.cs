using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
namespace Clubs;

public class GoogleMediaConnection : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ConnectedBy { get; set; }
    public string GoogleSubject { get; set; } = "";
    public string Email { get; set; } = "";
    [JsonIgnore] public string RefreshTokenProtected { get; set; } = "";
    [JsonIgnore] public string AccessTokenProtected { get; set; } = "";
    public DateTimeOffset AccessExpiresAt { get; set; }
    public string RootFolderId { get; set; } = "";
    public bool ActiveForUploads { get; set; }
    public bool Connected { get; set; } = true;
    public DateTimeOffset ConnectedAt { get; set; } = DateTimeOffset.UtcNow;
}
public class GoogleMediaOAuthState : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string SessionId { get; set; } = "";
    public string StateHash { get; set; } = "";
    public string VerifierProtected { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Consumed { get; set; }
}
public class CommunityMedia : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid PostId { get; set; }
    public Guid ConnectionId { get; set; }
    public Guid UploadedBy { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public string FileName { get; set; } = "";
    public string MimeType { get; set; } = "";
    public long Size { get; set; }
    [JsonIgnore] public string DriveFileId { get; set; } = "";
    public string LastError { get; set; } = "";
    public int Attempts { get; set; } = 1;
    public string Status { get; set; } = "Uploading";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}
public partial class Db
{
    public DbSet<GoogleMediaConnection> GoogleMediaConnections => Set<GoogleMediaConnection>();
    public DbSet<GoogleMediaOAuthState> GoogleMediaOAuthStates => Set<GoogleMediaOAuthState>();
    public DbSet<CommunityMedia> CommunityMedia => Set<CommunityMedia>();
    static void ConfigureGoogleMedia(ModelBuilder b)
    {
        b.Entity<GoogleMediaConnection>().HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Entity<GoogleMediaConnection>().HasIndex(x => new { x.TenantId, x.GoogleSubject }).IsUnique();
        b.Entity<GoogleMediaConnection>().HasIndex(x => x.TenantId).IsUnique().HasFilter("\"ActiveForUploads\" = TRUE");
        b.Entity<GoogleMediaConnection>().HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<GoogleMediaOAuthState>().HasIndex(x => x.StateHash).IsUnique();
        b.Entity<CommunityMedia>().HasOne<CommunityPost>().WithMany().HasForeignKey(x => new { x.TenantId, x.PostId }).HasPrincipalKey(p => new { p.TenantId, p.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CommunityMedia>().HasOne<GoogleMediaConnection>().WithMany().HasForeignKey(x => new { x.TenantId, x.ConnectionId }).HasPrincipalKey(c => new { c.TenantId, c.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CommunityMedia>().HasIndex(x => new { x.PostId, x.Status, x.CreatedAt });
        b.Entity<CommunityMedia>().HasIndex(x => x.DriveFileId).IsUnique();
        b.Entity<CommunityMedia>().HasIndex(x => new { x.PostId,x.SourceDocumentId }).IsUnique().HasFilter("\"SourceDocumentId\" IS NOT NULL");
    }
}
