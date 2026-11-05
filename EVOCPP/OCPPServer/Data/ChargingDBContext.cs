using Microsoft.EntityFrameworkCore;
using OCPPServer.DataBase.DBModels;

namespace OCPPServer.Data
{
    public class ChargingDBContext : DbContext
    {
        public ChargingDBContext(DbContextOptions<ChargingDBContext> options) : base(options) { }

        public DbSet<Plug>     Plugs     => Set<Plug>();
        public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();
        public DbSet<PlugConnector> PlugConnectors => Set<PlugConnector>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // OcppId is the lookup key for every OCPP message — index eliminates sequential scans.
            modelBuilder.Entity<Plug>()
                .HasIndex(p => p.OcppId)
                .IsUnique();

            modelBuilder.Entity<PlugConnector>()
                .HasIndex(c => new { c.OcppId, c.ConnectorId })
                .IsUnique();
            modelBuilder.Entity<PlugConnector>()
                .HasIndex(c => new { c.OcppId, c.TransactionId });
            modelBuilder.Entity<PlugConnector>()
                .Property(c => c.MeterStartWh).HasColumnType("numeric");
        }
    }
}
