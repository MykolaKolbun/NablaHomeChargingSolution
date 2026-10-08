using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPPServer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLiveMeterColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastMeterValueAt",
                table: "Plugs");

            migrationBuilder.DropColumn(
                name: "MeterValue",
                table: "Plugs");

            migrationBuilder.DropColumn(
                name: "StateOfCharge",
                table: "Plugs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastMeterValueAt",
                table: "Plugs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MeterValue",
                table: "Plugs",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StateOfCharge",
                table: "Plugs",
                type: "numeric",
                nullable: true);
        }
    }
}
