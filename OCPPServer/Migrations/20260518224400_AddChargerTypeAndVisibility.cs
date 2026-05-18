using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPPServer.Migrations
{
    /// <inheritdoc />
    public partial class AddChargerTypeAndVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFastCharger",
                table: "Connectors",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "MaxPowerKw",
                table: "Connectors",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowOnMap",
                table: "Connectors",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsFastCharger",
                table: "Connectors");

            migrationBuilder.DropColumn(
                name: "MaxPowerKw",
                table: "Connectors");

            migrationBuilder.DropColumn(
                name: "ShowOnMap",
                table: "Connectors");
        }
    }
}
