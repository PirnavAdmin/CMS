using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations
{
    /// <inheritdoc />
    public partial class AddPharmacyOtcSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PharmacyOtcSales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HospitalId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    AppointmentId = table.Column<int>(type: "int", nullable: false),
                    PharmacistUserId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CommittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BillingId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacyOtcSales", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PharmacyOtcSaleItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PharmacyOtcSaleId = table.Column<int>(type: "int", nullable: false),
                    MedicineId = table.Column<int>(type: "int", nullable: false),
                    StockBatchId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GstPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacyOtcSaleItems", x => x.Id);
                    table.CheckConstraint("CK_PharmacyOtcSaleItems_Quantity", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_PharmacyOtcSaleItems_Medicines_MedicineId",
                        column: x => x.MedicineId,
                        principalTable: "Medicines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PharmacyOtcSaleItems_PharmacyOtcSales_PharmacyOtcSaleId",
                        column: x => x.PharmacyOtcSaleId,
                        principalTable: "PharmacyOtcSales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PharmacyOtcSaleItems_PharmacyStockBatches_StockBatchId",
                        column: x => x.StockBatchId,
                        principalTable: "PharmacyStockBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyOtcSaleItems_MedicineId",
                table: "PharmacyOtcSaleItems",
                column: "MedicineId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyOtcSaleItems_PharmacyOtcSaleId",
                table: "PharmacyOtcSaleItems",
                column: "PharmacyOtcSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyOtcSaleItems_StockBatchId",
                table: "PharmacyOtcSaleItems",
                column: "StockBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyOtcSales_BillingId",
                table: "PharmacyOtcSales",
                column: "BillingId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyOtcSales_HospitalId_BranchId_AppointmentId",
                table: "PharmacyOtcSales",
                columns: new[] { "HospitalId", "BranchId", "AppointmentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PharmacyOtcSaleItems");

            migrationBuilder.DropTable(
                name: "PharmacyOtcSales");
        }
    }
}
