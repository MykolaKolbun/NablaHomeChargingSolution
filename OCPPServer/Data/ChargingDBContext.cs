using Microsoft.EntityFrameworkCore;
using OCPPServer.ChargingStationInterface;
using OCPPServer.DataBase.DBModels;
namespace OCPPServer.Data
{
    public class ChargingDBContext: DbContext
    {
        public ChargingDBContext(DbContextOptions<ChargingDBContext> options): base(options)
        {
            
        }

        /// <summary>
        /// Gets the set of connectors in the database context.
        /// </summary>
        public DbSet<Connector> Connectors => Set<Connector>();

        /// <summary>
        /// Gets the set of charging stations in the context.
        /// </summary>
        public DbSet<ChargingStation> ChargingStations => Set<ChargingStation>();

        /// <summary>
        /// Gets the set of charging session records in the database.
        /// </summary>
        public DbSet<ChargingSessionInfo> ChargingSessions => Set<ChargingSessionInfo>();

        /// <summary>
        /// Gets the set of locations in the context, allowing queries and updates to the Location entities.
        /// </summary>
        /// <remarks>This property provides access to the underlying database table for locations. Use
        /// LINQ queries or Entity Framework methods to retrieve, add, update, or remove Location entities from the
        /// context.</remarks>
        public DbSet<Location> Locations => Set<Location>();

        /// <summary>
        /// Gets the set of users in the context for querying and saving.
        /// </summary>
        /// <remarks>This property provides access to the users stored in the database. Use LINQ queries
        /// to retrieve, add, update, or remove user entities. Changes made to the returned set are tracked by the
        /// context and persisted to the database when SaveChanges is called.</remarks>
        public DbSet<User> Users => Set<User>();
    }
}
