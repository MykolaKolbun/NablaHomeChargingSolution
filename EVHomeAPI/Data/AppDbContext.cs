using EVHomeAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EVHomeAPI.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User>          Users          => Set<User>();
    public DbSet<Station>       Stations       => Set<Station>();
    public DbSet<StationAccess> StationAccesses => Set<StationAccess>();

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
    }
}
