
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations
{
    public partial class AddMedicineCrudAuditFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use migration time for existing records.
            // This does not recover their original creation dates.
            var migrationTime = DateTime.UtcNow;

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Medicines",
                type: "datetime2",
                nullable: false,
                defaultValue: migrationTime);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeactivatedAt",
                table: "Medicines",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReactivatedAt",
                table: "Medicines",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Medicines",
                type: "datetime2",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Medicines");

            migrationBuilder.DropColumn(
                name: "DeactivatedAt",
                table: "Medicines");

            migrationBuilder.DropColumn(
                name: "ReactivatedAt",
                table: "Medicines");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Medicines");
        }
    }
}
