using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AuthDemo.Data;

#nullable disable

namespace AuthDemo.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260731103000_AddPrescriptionLabTestsAndPrintTracking")]
    public partial class AddPrescriptionLabTestsAndPrintTracking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(name: "IsPrinted", table: "Prescriptions", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<DateTime>(name: "PrintedAt", table: "Prescriptions", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<int>(name: "PrintedByUserId", table: "Prescriptions", type: "int", nullable: true);

            migrationBuilder.CreateTable(
                name: "PrescriptionLabTests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    PrescriptionId = table.Column<int>(type: "int", nullable: false),
                    TestName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Instructions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrescriptionLabTests", x => x.Id);
                    table.ForeignKey(name: "FK_PrescriptionLabTests_Prescriptions_PrescriptionId", column: x => x.PrescriptionId, principalTable: "Prescriptions", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(name: "IX_PrescriptionLabTests_PrescriptionId", table: "PrescriptionLabTests", column: "PrescriptionId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PrescriptionLabTests");
            migrationBuilder.DropColumn(name: "IsPrinted", table: "Prescriptions");
            migrationBuilder.DropColumn(name: "PrintedAt", table: "Prescriptions");
            migrationBuilder.DropColumn(name: "PrintedByUserId", table: "Prescriptions");
        }
    }
}
