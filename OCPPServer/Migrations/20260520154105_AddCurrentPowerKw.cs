using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPPServer.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrentPowerKw : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CurrentPowerKw",
                table: "Connectors",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastMeterValueAt",
                table: "Connectors",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActiveTransactionId",
                table: "Connectors",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentPowerKw",
                table: "Connectors");

            migrationBuilder.DropColumn(
                name: "LastMeterValueAt",
                table: "Connectors");

            migrationBuilder.DropColumn(
                name: "ActiveTransactionId",
                table: "Connectors");
        }
    }
}
