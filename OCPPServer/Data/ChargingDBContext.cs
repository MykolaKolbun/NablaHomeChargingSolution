using Microsoft.EntityFrameworkCore;
using OCPPServer.DataBase.DBModels;

namespace OCPPServer.Data
{
    public class ChargingDBContext : DbContext
    {
        public ChargingDBContext(DbContextOptions<ChargingDBContext> options) : base(options) { }

        public DbSet<Plug> Plugs => Set<Plug>();
    }
}
