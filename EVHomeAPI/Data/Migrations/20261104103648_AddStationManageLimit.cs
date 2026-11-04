using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVHomeAPI.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStationManageLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ManageLimit",
                table: "Stations",
                type: "boolean",
                nullable: false,
                defaultValue: true);   // existing stations stay managed (D11 — the 16 A house limit)
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManageLimit",
                table: "Stations");
        }
    }
}
