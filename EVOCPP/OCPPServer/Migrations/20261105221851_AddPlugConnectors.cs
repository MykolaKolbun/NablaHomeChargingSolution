using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OCPPServer.Migrations
{
    /// <inheritdoc />
    public partial class AddPlugConnectors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlugConnectors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OcppId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ConnectorId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    LastStatusUpdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TransactionId = table.Column<int>(type: "integer", nullable: true),
                    MeterStartWh = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlugConnectors", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlugConnectors_OcppId_ConnectorId",
                table: "PlugConnectors",
                columns: new[] { "OcppId", "ConnectorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlugConnectors_OcppId_TransactionId",
                table: "PlugConnectors",
                columns: new[] { "OcppId", "TransactionId" });

            // Carry a session that is running during the upgrade over to connector 1: until now
            // MeterStartWh was stored per station. TransactionId is unknown here — StopTransaction
            // falls back to "the only open session without a transaction" (ConnectorStore).
            migrationBuilder.Sql(
                """
                INSERT INTO "PlugConnectors" ("OcppId", "ConnectorId", "Status", "LastStatusUpdate", "MeterStartWh")
                SELECT "OcppId", 1, NULL, "LastStatusUpdate", "MeterStartWh"
                FROM "Plugs" WHERE "MeterStartWh" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlugConnectors");
        }
    }
}
