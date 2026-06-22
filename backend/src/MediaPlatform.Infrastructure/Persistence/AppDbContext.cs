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
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // --- User / Role / RBAC ---
        b.Entity<User>(e =>
        {
            e.Property(x => x.Email).IsRequired().HasMaxLength(256);
            e.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
            e.Property(x => x.PasswordHash).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
        });

        b.Entity<Role>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(50);
            e.HasIndex(x => x.Name).IsUnique();
            e.HasData(
                new Role { Id = SeedData.AdminRoleId, Name = "Admin" },
                new Role { Id = SeedData.EditeurRoleId, Name = "Editeur" },
                new Role { Id = SeedData.VisiteurRoleId, Name = "Visiteur" });
        });

        b.Entity<UserRole>(e =>
        {
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.HasOne(x => x.User).WithMany(u => u.Roles).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Role).WithMany(r => r.Users).HasForeignKey(x => x.RoleId);
        });

        // --- Catalogue ---
        b.Entity<Category>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(150);
            e.Property(x => x.Slug).IsRequired().HasMaxLength(160);
            e.HasIndex(x => x.Slug).IsUnique();
        });

        b.Entity<Tag>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<Video>(e =>
        {
            e.Property(x => x.Title).IsRequired().HasMaxLength(300);
            e.Property(x => x.Slug).IsRequired().HasMaxLength(320);
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
            e.HasGeneratedTsVectorColumn(x => x.SearchVector, "french", x => new { x.Title, x.Description })
             .HasIndex(x => x.SearchVector).HasMethod("GIN");
        });

        b.Entity<VideoTag>(e =>
        {
            e.HasKey(x => new { x.VideoId, x.TagId });
            e.HasOne(x => x.Video).WithMany(v => v.Tags).HasForeignKey(x => x.VideoId);
            e.HasOne(x => x.Tag).WithMany(t => t.Videos).HasForeignKey(x => x.TagId);
        });

        b.Entity<VideoRendition>(e =>
        {
            e.Property(x => x.Resolution).IsRequired().HasMaxLength(20);
            e.Property(x => x.ManifestKey).IsRequired().HasMaxLength(500);
            e.HasOne(x => x.Video).WithMany(v => v.Renditions).HasForeignKey(x => x.VideoId);
        });

        b.Entity<TranscodeJob>(e =>
        {
            e.HasIndex(x => x.VideoId);
        });

        // --- Engagement ---
        b.Entity<Comment>(e =>
        {
            e.Property(x => x.Body).IsRequired().HasMaxLength(4000);
            e.HasOne<Video>().WithMany(v => v.Comments).HasForeignKey(x => x.VideoId);
            e.HasIndex(x => x.VideoId);
        });

        b.Entity<Like>(e =>
        {
            e.HasIndex(x => new { x.VideoId, x.UserId }).IsUnique();
        });

        b.Entity<Share>(e =>
        {
            e.Property(x => x.Channel).IsRequired().HasMaxLength(50);
            e.HasIndex(x => x.VideoId);
        });

        b.Entity<ViewEvent>(e =>
        {
            e.Property(x => x.SessionId).HasMaxLength(100);
            e.HasIndex(x => new { x.VideoId, x.OccurredAt });
        });

        b.Entity<AuditLog>(e =>
        {
            e.Property(x => x.Action).IsRequired().HasMaxLength(100);
            e.Property(x => x.EntityType).IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.OccurredAt);
        });

        b.Entity<SystemSetting>(e =>
        {
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(200);
            e.Property(x => x.Value).IsRequired();
        });

        base.OnModelCreating(b);
    }
}
