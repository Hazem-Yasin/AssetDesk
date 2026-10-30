using AssetDesk.Application.Abstractions;
using AssetDesk.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssetDesk.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.Property(u => u.Email).HasMaxLength(200).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
            e.Property(u => u.PasswordHash).HasMaxLength(300).IsRequired();
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<Asset>(e =>
        {
            e.Property(a => a.Tag).HasMaxLength(20).IsRequired();
            e.HasIndex(a => a.Tag).IsUnique();
            e.Property(a => a.Name).HasMaxLength(150).IsRequired();
            e.Property(a => a.SerialNumber).HasMaxLength(100);
            e.Property(a => a.Notes).HasMaxLength(1000);
            e.Property(a => a.PurchasePrice).HasPrecision(12, 2);
            e.Property(a => a.Category).HasConversion<string>().HasMaxLength(20);
            e.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(a => a.Status);
            // Optimistic concurrency: the version is bumped in SaveChanges and checked by EF in the UPDATE.
            e.Property(a => a.Version).IsConcurrencyToken();
            e.Ignore(a => a.CurrentAssignment);
        });

        b.Entity<Assignment>(e =>
        {
            e.Property(a => a.Notes).HasMaxLength(500);
            e.Ignore(a => a.IsActive);
            e.HasOne(a => a.Asset).WithMany(x => x.Assignments).HasForeignKey(a => a.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(a => new { a.UserId, a.ReturnedAt });
        });

        b.Entity<Attachment>(e =>
        {
            e.Property(a => a.FileName).HasMaxLength(255).IsRequired();
            e.Property(a => a.ContentType).HasMaxLength(100).IsRequired();
            e.Property(a => a.StorageKey).HasMaxLength(300).IsRequired();
            e.HasOne(a => a.Asset).WithMany(x => x.Attachments).HasForeignKey(a => a.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<Asset>().Where(e => e.State == EntityState.Modified))
        {
            var version = entry.Property(a => a.Version);
            version.CurrentValue = (int)version.OriginalValue! + 1;
        }
        return base.SaveChangesAsync(ct);
    }

    Task<int> IUnitOfWork.SaveChangesAsync(CancellationToken ct) => SaveChangesAsync(ct);
}
