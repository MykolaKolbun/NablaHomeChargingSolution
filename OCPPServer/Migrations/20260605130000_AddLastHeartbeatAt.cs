using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPPServer.Migrations
{
    /// <inheritdoc />
    public partial class AddLastHeartbeatAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF NOT EXISTS makes this idempotent — safe to run even if the column
            // already exists (e.g. added manually or by a previous failed migration run).
            migrationBuilder.Sql(
                """
                ALTER TABLE "Plugs" ADD COLUMN IF NOT EXISTS "LastHeartbeatAt" timestamp with time zone;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastHeartbeatAt",
                table: "Plugs");
        }
    }
}
