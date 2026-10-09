using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EVHomeAPI.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChargingLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CurrentLimitA",
                table: "Stations",
                type: "numeric(5,1)",
                precision: 5,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LimitStatus",
                table: "Stations",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LimitUpdatedAt",
                table: "Stations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxCurrentA",
                table: "Stations",
                type: "integer",
                nullable: false,
                defaultValue: 32);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentLimitA",
                table: "Stations");

            migrationBuilder.DropColumn(
                name: "LimitStatus",
                table: "Stations");

            migrationBuilder.DropColumn(
                name: "LimitUpdatedAt",
                table: "Stations");

            migrationBuilder.DropColumn(
                name: "MaxCurrentA",
                table: "Stations");
        }
    }
}
