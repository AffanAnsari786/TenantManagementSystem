using Microsoft.EntityFrameworkCore;
using Tenant.Api.Models;
using Tenant.Api.Model;

namespace Tenant.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Entry> Entries => Set<Entry>();
        public DbSet<Record> Records => Set<Record>();
        public DbSet<SharedLink> SharedLinks => Set<SharedLink>();
        public DbSet<User> Users => Set<User>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Entry
            modelBuilder.Entity<Entry>(e =>
            {
                e.ToTable("Entries");
                e.HasKey(x => x.Id);
                e.Property(x => x.PublicId).IsRequired();
                e.HasIndex(x => x.PublicId).IsUnique();
                // Hot path: every ownership-scoped query filters by UserId.
                e.HasIndex(x => x.UserId).HasDatabaseName("IX_Entries_UserId");
                e.Property(x => x.Name).HasMaxLength(200);
                e.Property(x => x.Address).HasMaxLength(500);
                e.Property(x => x.AadhaarNumber).HasMaxLength(12);
                e.Property(x => x.PropertyName).HasMaxLength(200);
                e.Property(x => x.StartDate).HasColumnType("date");
                e.Property(x => x.EndDate).HasColumnType("date");
                e.Property(x => x.UserId);
                e.HasMany(x => x.Records)
                    .WithOne(x => x.Entry)
                    .HasForeignKey(x => x.EntryId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Record
            modelBuilder.Entity<Record>(e =>
            {
                e.ToTable("Records");
                e.HasKey(x => x.Id);
                e.Property(x => x.PublicId).IsRequired();
                e.HasIndex(x => x.PublicId).IsUnique();
                // FK lookups: Records.EntryId is queried on every record list.
                e.HasIndex(x => x.EntryId).HasDatabaseName("IX_Records_EntryId");
                e.Property(x => x.RentPeriod).HasColumnType("date");
                e.Property(x => x.Amount).HasPrecision(18, 2);
                e.Property(x => x.ReceivedDate).HasColumnType("date");
                e.Property(x => x.CreatedDate);
                e.Property(x => x.TenantSign).HasMaxLength(5000);
                e.Property(x => x.ReceiptNumber).HasMaxLength(50);
            });

            // SharedLink
            modelBuilder.Entity<SharedLink>(e =>
            {
                e.ToTable("SharedLinks");
                e.HasKey(x => x.Id);
                e.Property(x => x.ShareToken).HasMaxLength(255);
                e.HasIndex(x => x.ShareToken).IsUnique();
                // Filtered-queries hot path: active + non-expired lookups.
                e.HasIndex(x => new { x.IsActive, x.ExpiryDate })
                    .HasDatabaseName("IX_SharedLinks_IsActive_ExpiryDate");
                e.Property(x => x.IsActive);
                e.HasOne<Entry>()
                    .WithMany()
                    .HasForeignKey(x => x.EntryId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // User - map Password to DB column PasswordHash
            modelBuilder.Entity<User>(e =>
            {
                e.ToTable("Users");
                e.HasKey(x => x.Id);
                e.Property(x => x.Username).HasMaxLength(100);
                e.Property(x => x.Password).HasColumnName("PasswordHash").HasMaxLength(255);
                e.Property(x => x.Role).HasMaxLength(50);
                e.HasIndex(x => x.Username).IsUnique();
            });

            // RefreshToken
            modelBuilder.Entity<RefreshToken>(e =>
            {
                e.ToTable("RefreshTokens");
                e.HasKey(x => x.Id);
                e.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
                e.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);
                e.HasIndex(x => x.TokenHash).IsUnique();
                e.HasIndex(x => x.UserId).HasDatabaseName("IX_RefreshTokens_UserId");
                e.Ignore(x => x.IsActive);
            });
        }
    }
}
