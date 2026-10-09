using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVHomeAPI.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionPause : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_OneOpenPerStation",
                table: "Sessions");

            migrationBuilder.AddColumn<decimal>(
                name: "CarriedEnergyKwh",
                table: "Sessions",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "PausedAt",
                table: "Sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResumeAttempts",
                table: "Sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResumeRequestedAt",
                table: "Sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_OneOpenPerStation",
                table: "Sessions",
                column: "StationId",
                unique: true,
                filter: "\"Status\" IN ('Pending', 'Active', 'Stopping', 'Paused')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_OneOpenPerStation",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "CarriedEnergyKwh",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "PausedAt",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "ResumeAttempts",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "ResumeRequestedAt",
                table: "Sessions");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_OneOpenPerStation",
                table: "Sessions",
                column: "StationId",
                unique: true,
                filter: "\"Status\" IN ('Pending', 'Active', 'Stopping')");
        }
    }
}
