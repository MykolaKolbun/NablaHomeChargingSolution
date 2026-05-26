using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPPServer.Migrations
{
    /// <inheritdoc />
    public partial class ErrorLogsLocalTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert OccurredAt and SolvedAt from UTC timestamptz to local
            // timestamp (Europe/Kyiv).  The AT TIME ZONE expression converts the
            // stored UTC instant to the wall-clock time in that zone so the value
            // is preserved correctly for rows that already exist.
            migrationBuilder.Sql(
                """
                ALTER TABLE "ErrorLogs"
                ALTER COLUMN "OccurredAt" TYPE timestamp without time zone
                USING "OccurredAt" AT TIME ZONE 'Europe/Kyiv'
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "ErrorLogs"
                ALTER COLUMN "SolvedAt" TYPE timestamp without time zone
                USING "SolvedAt" AT TIME ZONE 'Europe/Kyiv'
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse: treat the stored local values as Europe/Kyiv and convert
            // back to UTC timestamptz.
            migrationBuilder.Sql(
                """
                ALTER TABLE "ErrorLogs"
                ALTER COLUMN "OccurredAt" TYPE timestamp with time zone
                USING "OccurredAt" AT TIME ZONE 'Europe/Kyiv'
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "ErrorLogs"
                ALTER COLUMN "SolvedAt" TYPE timestamp with time zone
                USING "SolvedAt" AT TIME ZONE 'Europe/Kyiv'
                """);
        }
    }
}
