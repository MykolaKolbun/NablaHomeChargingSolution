using EVHomeAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EVHomeAPI.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User>          Users          => Set<User>();
    public DbSet<Station>       Stations       => Set<Station>();
    public DbSet<StationAccess> StationAccesses => Set<StationAccess>();
    public DbSet<ChargingSession>     Sessions      => Set<ChargingSession>();
    public DbSet<SessionMeterReading> MeterReadings => Set<SessionMeterReading>();
    public DbSet<DeviceToken>         DeviceTokens  => Set<DeviceToken>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).HasMaxLength(256);
            e.Property(u => u.Name).HasMaxLength(100);
        });

        b.Entity<Station>(e =>
        {
            e.HasIndex(s => s.OcppId).IsUnique();
            e.Property(s => s.OcppId).HasMaxLength(64);
            e.Property(s => s.Name).HasMaxLength(100);
            // Concurrency token: two simultaneous claims → only the first UPDATE matches.
            e.Property(s => s.ClaimCodeHash).IsConcurrencyToken();
        });

        b.Entity<StationAccess>(e =>
        {
            e.HasKey(a => new { a.StationId, a.UserId });
            e.HasOne(a => a.Station).WithMany(s => s.Accesses).HasForeignKey(a => a.StationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.User).WithMany(u => u.StationAccesses).HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
            e.Property(a => a.Role).HasConversion<string>().HasMaxLength(16);
        });

        b.Entity<Station>().Property(s => s.ConnectorStatus).HasMaxLength(32);
        b.Entity<Station>().Property(s => s.CurrentLimitA).HasPrecision(5, 1);
        b.Entity<Station>().Property(s => s.LimitStatus).HasMaxLength(16);
        b.Entity<Station>().Property(s => s.MaxCurrentA).HasDefaultValue(32);

        b.Entity<ChargingSession>(e =>
        {
            e.HasOne(s => s.Station).WithMany(st => st.Sessions).HasForeignKey(s => s.StationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(s => s.InitiatedBy).HasConversion<string>().HasMaxLength(16);
            e.Property(s => s.StopReason).HasConversion<string>().HasMaxLength(24);
            e.Property(s => s.EnergyKwh).HasPrecision(12, 3);
            e.Property(s => s.CarriedEnergyKwh).HasPrecision(12, 3);
            e.Property(s => s.MeterStartWh).HasPrecision(14, 2);
            e.Property(s => s.MeterStopWh).HasPrecision(14, 2);
            e.Property(s => s.Soc).HasPrecision(5, 1);
            e.Ignore(s => s.IsOpen);
            e.HasIndex(s => new { s.StationId, s.Status });
            // At most one open session per station — guards concurrent Start requests.
            e.HasIndex(s => s.StationId)
                .IsUnique()
                .HasFilter("\"Status\" IN ('Pending', 'Active', 'Stopping', 'Paused')")
                .HasDatabaseName("IX_Sessions_OneOpenPerStation");
            e.HasIndex(s => s.TrackingId);
        });

        b.Entity<DeviceToken>(e =>
        {
            e.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(d => d.Token).IsUnique();
            e.Property(d => d.Token).HasMaxLength(512);
            e.Property(d => d.Platform).HasMaxLength(16);
            e.Property(d => d.Language).HasMaxLength(8);
        });

        b.Entity<SessionMeterReading>(e =>
        {
            e.HasOne(r => r.Session).WithMany(s => s.MeterReadings).HasForeignKey(r => r.SessionId).OnDelete(DeleteBehavior.Cascade);
            e.Property(r => r.EnergyKwh).HasPrecision(12, 3);
            e.Property(r => r.Soc).HasPrecision(5, 1);
            e.HasIndex(r => new { r.SessionId, r.ElapsedSec });
        });
    }
}
