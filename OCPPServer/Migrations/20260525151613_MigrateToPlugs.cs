using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OCPPServer.Migrations
{
    /// <inheritdoc />
    public partial class MigrateToPlugs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Connectors");

            migrationBuilder.CreateTable(
                name: "Plugs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsOnline = table.Column<bool>(type: "boolean", nullable: false),
                    MeterValue = table.Column<decimal>(type: "numeric", nullable: true),
                    LastMeterValueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastStatusUpdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsFastCharger = table.Column<bool>(type: "boolean", nullable: false),
                    MaxPower = table.Column<int>(type: "integer", nullable: false),
                    Vendor = table.Column<string>(type: "text", nullable: true),
                    ChargePointModel = table.Column<string>(type: "text", nullable: true),
                    ChargePointSN = table.Column<string>(type: "text", nullable: false),
                    FirmwareVersion = table.Column<string>(type: "text", nullable: true),
                    SIMNr = table.Column<string>(type: "text", nullable: true),
                    OcppId = table.Column<string>(type: "text", nullable: false),
                    OcppVersion = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plugs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Plugs");

            migrationBuilder.CreateTable(
                name: "Connectors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActiveTransactionId = table.Column<int>(type: "integer", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    ChargePointModel = table.Column<string>(type: "text", nullable: true),
                    ChargePointSN = table.Column<string>(type: "text", nullable: false),
                    ChargerSN = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CurrentPowerKw = table.Column<double>(type: "double precision", nullable: true),
                    FirmwareVersion = table.Column<string>(type: "text", nullable: true),
                    IsFastCharger = table.Column<bool>(type: "boolean", nullable: false),
                    LastMeterValueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUpdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    MaxPowerKw = table.Column<double>(type: "double precision", nullable: true),
                    MeterValue = table.Column<decimal>(type: "numeric", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true),
                    NumberOfConnectors = table.Column<int>(type: "integer", nullable: false),
                    OcppId = table.Column<string>(type: "text", nullable: false),
                    SIMNr = table.Column<string>(type: "text", nullable: true),
                    SessionStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ShowOnMap = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Vendor = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Connectors", x => x.Id);
                });
        }
    }
}
