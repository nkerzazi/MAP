using MediaPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MediaPlatform.Infrastructure.Persistence;

/// <summary>Contexte EF Core (PostgreSQL). Les configurations fines (index, contraintes
/// d'unicité Like(VideoId,UserId), tsvector de recherche) seront ajoutées en phase 1/3.</summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Video> Videos => Set<Video>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<VideoTag> VideoTags => Set<VideoTag>();
    public DbSet<VideoRendition> VideoRenditions => Set<VideoRendition>();
    public DbSet<TranscodeJob> TranscodeJobs => Set<TranscodeJob>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Like> Likes => Set<Like>();
    public DbSet<Share> Shares => Set<Share>();
    public DbSet<ViewEvent> ViewEvents => Set<ViewEvent>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<UserRole>().HasKey(x => new { x.UserId, x.RoleId });
        b.Entity<VideoTag>().HasKey(x => new { x.VideoId, x.TagId });
        b.Entity<Like>().HasIndex(x => new { x.VideoId, x.UserId }).IsUnique();
        base.OnModelCreating(b);
    }
}
