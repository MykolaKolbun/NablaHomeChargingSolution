using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPPServer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMeterStartStopFromConnector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MeterStart and MeterStop are no longer stored in the OCPP DB.
            // MeterStart is kept in-memory (ChargingStationConnections) during a session
            // and persisted in EVChargingDB.ChargingSessions via RabbitMQ.
            // MeterStop is momentary — written directly to EVChargingDB.ChargingSessions.
            migrationBuilder.DropColumn(
                name: "MeterStart",
                table: "Connectors");

            migrationBuilder.DropColumn(
                name: "MeterStop",
                table: "Connectors");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MeterStart",
                table: "Connectors",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MeterStop",
                table: "Connectors",
                type: "numeric",
                nullable: true);
        }
    }
}
