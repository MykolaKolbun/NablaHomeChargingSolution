using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPPServer.Migrations
{
    /// <summary>
    /// Adds MeterStartWh to Plugs so the OCPP server can recover ConnectorState.MeterStartWh
    /// after a process restart without relying solely on in-memory state.
    ///
    /// Written by HandleStartTransaction / HandleTxStarted; cleared by HandleStopTransaction /
    /// HandleTxEnded.  MeterValues handlers restore the in-memory value from this column when
    /// state.MeterStartWh is null (i.e. after a restart while a session was active).
    /// </summary>
    public partial class AddMeterStartWhToPlug : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name:      "MeterStartWh",
                table:     "Plugs",
                type:      "numeric",
                nullable:  true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name:  "MeterStartWh",
                table: "Plugs");
        }
    }
}
