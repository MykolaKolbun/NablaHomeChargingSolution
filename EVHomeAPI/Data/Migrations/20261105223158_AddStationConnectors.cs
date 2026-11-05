using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVHomeAPI.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStationConnectors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_OneOpenPerStation",
                table: "Sessions");

            migrationBuilder.CreateTable(
                name: "StationConnectors",
                columns: table => new
                {
                    StationId = table.Column<int>(type: "integer", nullable: false),
                    ConnectorId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    LastStatusAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StationConnectors", x => new { x.StationId, x.ConnectorId });
                    table.ForeignKey(
                        name: "FK_StationConnectors_Stations_StationId",
                        column: x => x.StationId,
                        principalTable: "Stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_OneOpenPerConnector",
                table: "Sessions",
                columns: new[] { "StationId", "ConnectorId" },
                unique: true,
                filter: "\"Status\" IN ('Pending', 'Active', 'Stopping', 'Paused')");

            // Existing stations: connector 1 from the per-station status mirror.
            migrationBuilder.Sql(
                """
                INSERT INTO "StationConnectors" ("StationId", "ConnectorId", "Status", "LastStatusAt")
                SELECT "Id", 1, "ConnectorStatus", "LastStatusAt" FROM "Stations"
                WHERE "ConnectorStatus" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StationConnectors");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_OneOpenPerConnector",
                table: "Sessions");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_OneOpenPerStation",
                table: "Sessions",
                column: "StationId",
                unique: true,
                filter: "\"Status\" IN ('Pending', 'Active', 'Stopping', 'Paused')");
        }
    }
}
