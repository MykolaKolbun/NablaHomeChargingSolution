using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OCPPServer.Data;

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
    ///
    /// The original file shipped without a Designer (no [Migration] attribute), so EF never
    /// discovered it and fresh databases lacked the column. Attributes are declared here;
    /// IF NOT EXISTS keeps it safe on databases where the column was added another way.
    /// </summary>
    [DbContext(typeof(ChargingDBContext))]
    [Migration("20260609120000_AddMeterStartWhToPlug")]
    public partial class AddMeterStartWhToPlug : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Plugs" ADD COLUMN IF NOT EXISTS "MeterStartWh" numeric;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name:  "MeterStartWh",
                table: "Plugs");
        }
    }
}
