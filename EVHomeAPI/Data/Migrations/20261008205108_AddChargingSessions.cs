using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EVHomeAPI.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChargingSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConnectorStatus",
                table: "Stations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOnline",
                table: "Stations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastStatusAt",
                table: "Stations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StationId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ConnectorId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    InitiatedBy = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StopReason = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    TrackingId = table.Column<Guid>(type: "uuid", nullable: true),
                    OcppTransactionId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StopRequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MeterStartWh = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    MeterStopWh = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    EnergyKwh = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    CurrentPowerKw = table.Column<double>(type: "double precision", nullable: true),
                    Soc = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessions_Stations_StationId",
                        column: x => x.StationId,
                        principalTable: "Stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Sessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeterReadings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionId = table.Column<int>(type: "integer", nullable: false),
                    ElapsedSec = table.Column<int>(type: "integer", nullable: false),
                    CurrentPowerKw = table.Column<double>(type: "double precision", nullable: true),
                    Soc = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    EnergyKwh = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeterReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeterReadings_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_SessionId_ElapsedSec",
                table: "MeterReadings",
                columns: new[] { "SessionId", "ElapsedSec" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_OneOpenPerStation",
                table: "Sessions",
                column: "StationId",
                unique: true,
                filter: "\"Status\" IN ('Pending', 'Active', 'Stopping')");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_StationId_Status",
                table: "Sessions",
                columns: new[] { "StationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_TrackingId",
                table: "Sessions",
                column: "TrackingId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_UserId",
                table: "Sessions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeterReadings");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropColumn(
                name: "ConnectorStatus",
                table: "Stations");

            migrationBuilder.DropColumn(
                name: "IsOnline",
                table: "Stations");

            migrationBuilder.DropColumn(
                name: "LastStatusAt",
                table: "Stations");
        }
    }
}
