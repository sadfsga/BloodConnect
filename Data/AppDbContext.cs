using BloodConnect.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BloodConnect.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<BloodRequest> BloodRequests => Set<BloodRequest>();
    public DbSet<Pledge> Pledges => Set<Pledge>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).HasMaxLength(256);
            e.Property(u => u.FullName).HasMaxLength(100);
            e.Property(u => u.Phone).HasMaxLength(30);
            e.Property(u => u.City).HasMaxLength(80);
            // Enums are stored as readable text ("OPositive") instead of numbers.
            e.Property(u => u.BloodType).HasConversion<string>().HasMaxLength(20);
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<BloodRequest>(e =>
        {
            e.Property(r => r.PatientName).HasMaxLength(100);
            e.Property(r => r.Hospital).HasMaxLength(150);
            e.Property(r => r.City).HasMaxLength(80);
            e.Property(r => r.ContactPhone).HasMaxLength(30);
            e.Property(r => r.Notes).HasMaxLength(500);
            e.Property(r => r.BloodType).HasConversion<string>().HasMaxLength(20);
            e.Property(r => r.Urgency).HasConversion<string>().HasMaxLength(20);
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

            e.HasOne(r => r.Requester)
                .WithMany(u => u.Requests)
                .HasForeignKey(r => r.RequesterId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(r => new { r.Status, r.BloodType, r.City });
        });

        b.Entity<Pledge>(e =>
        {
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

            e.HasOne(p => p.Request)
                .WithMany(r => r.Pledges)
                .HasForeignKey(p => p.RequestId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(p => p.Donor)
                .WithMany(u => u.Pledges)
                .HasForeignKey(p => p.DonorId)
                .OnDelete(DeleteBehavior.Restrict);

            // One donor can pledge to the same request only once.
            e.HasIndex(p => new { p.RequestId, p.DonorId }).IsUnique();
        });
    }
}
