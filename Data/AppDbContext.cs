namespace inzynierka.Data;

using inzynierka.Models.Entities;
using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserFtpHistory> UserFtpHistories => Set<UserFtpHistory>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ActivityCogganMetrics> ActivityCogganMetrics => Set<ActivityCogganMetrics>();
    public DbSet<DailyMetric> DailyMetrics => Set<DailyMetric>();
    public DbSet<ActivityPowerPeak> ActivityPowerPeaks => Set<ActivityPowerPeak>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.WeightKg).HasPrecision(5, 2);
        });

        modelBuilder.Entity<UserFtpHistory>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.HasOne(f => f.User)
                  .WithMany(u => u.FtpHistory)
                  .HasForeignKey(f => f.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(f => new { f.UserId, f.EffectiveDate });
        });

        modelBuilder.Entity<Activity>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasOne(a => a.User)
                .WithMany(u => u.Activities)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(a => new { a.UserId, a.StartTime });
            entity.HasIndex(a => new { a.UserId, a.FileHash }).IsUnique();
            entity.Property(a => a.DistanceMeters).HasPrecision(10, 2);
        });

        modelBuilder.Entity<ActivityCogganMetrics>(entity =>
        {
            entity.HasKey(m => m.ActivityId);
            entity.HasOne(m => m.Activity)
                  .WithOne(a => a.CogganMetrics)
                  .HasForeignKey<ActivityCogganMetrics>(m => m.ActivityId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Property(m => m.IntensityFactor).HasPrecision(4, 3);
            entity.Property(m => m.TrainingStressScore).HasPrecision(6, 1);
        });

        modelBuilder.Entity<DailyMetric>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.HasOne(d => d.User)
                  .WithMany(u => u.DailyMetrics)
                  .HasForeignKey(d => d.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(d => new { d.UserId, d.Date }).IsUnique();
            entity.Property(d => d.TotalTss).HasPrecision(6, 1);
            entity.Property(d => d.Ctl).HasPrecision(6, 2);
            entity.Property(d => d.Atl).HasPrecision(6, 2);
            entity.Property(d => d.Tsb).HasPrecision(6, 2);
        });

        modelBuilder.Entity<ActivityPowerPeak>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasOne(p => p.Activity)
                  .WithMany(a => a.PowerPeaks)
                  .HasForeignKey(p => p.ActivityId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(p => new { p.ActivityId, p.DurationSeconds });
        });
    }
}