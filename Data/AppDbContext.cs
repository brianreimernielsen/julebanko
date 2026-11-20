using Julebanko.Models;
using Microsoft.EntityFrameworkCore;

namespace Julebanko.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<DrawnNumber> DrawnNumbers => Set<DrawnNumber>();
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<SeasonBackup> SeasonBackups => Set<SeasonBackup>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Season>(e =>
        {
            e.HasIndex(x => x.Year);
            e.Property(x => x.Title).HasMaxLength(200);
        });

        modelBuilder.Entity<DrawnNumber>(e =>
        {
            e.HasIndex(x => new { x.SeasonId, x.Number }).IsUnique();
            e.HasOne(x => x.Season).WithMany(s => s.DrawnNumbers).HasForeignKey(x => x.SeasonId);
            e.Property(x => x.Number).IsRequired();
        });

        modelBuilder.Entity<Claim>(e =>
        {
            e.HasOne(x => x.Season).WithMany(s => s.Claims).HasForeignKey(x => x.SeasonId);
            e.HasOne(x => x.ReviewedByAdmin).WithMany().HasForeignKey(x => x.ReviewedByAdminId)
                .OnDelete(DeleteBehavior.SetNull);
            e.Property(x => x.PlayerName).HasMaxLength(120).IsRequired();
        });

        modelBuilder.Entity<AdminUser>(e =>
        {
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.Username).HasMaxLength(64).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
        });

        modelBuilder.Entity<AppSetting>(e =>
        {
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).HasMaxLength(100).IsRequired();
            e.Property(x => x.Value).HasMaxLength(2000).IsRequired();
        });

        modelBuilder.Entity<MediaAsset>(e =>
        {
            e.HasIndex(x => x.Slot).IsUnique();
            e.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            e.Property(x => x.RelativePath).HasMaxLength(500).IsRequired();
            e.HasOne(x => x.UploadedByAdmin).WithMany().HasForeignKey(x => x.UploadedByAdminId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SeasonBackup>(e =>
        {
            e.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            e.Property(x => x.RelativePath).HasMaxLength(500).IsRequired();
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasOne(x => x.Season).WithMany().HasForeignKey(x => x.SeasonId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedByAdmin).WithMany().HasForeignKey(x => x.CreatedByAdminId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
