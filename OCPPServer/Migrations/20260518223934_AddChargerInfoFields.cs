using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPPServer.Migrations
{
    /// <inheritdoc />
    public partial class AddChargerInfoFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Connectors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Connectors",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Connectors",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Connectors",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "Connectors");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Connectors");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Connectors");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Connectors");
        }
    }
}
