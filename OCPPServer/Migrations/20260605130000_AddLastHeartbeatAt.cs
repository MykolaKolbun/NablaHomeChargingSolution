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
            migrationBuilder.AddColumn<DateTime>(
                name: "LastHeartbeatAt",
                table: "Plugs",
                type: "timestamp with time zone",
                nullable: true);
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
