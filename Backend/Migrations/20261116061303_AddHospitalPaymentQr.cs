using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations
{
    /// <inheritdoc />
    public partial class AddHospitalPaymentQr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HospitalPaymentQrs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LocalReference = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    HospitalId = table.Column<int>(type: "int", nullable: false),
                    BillingId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RazorpayCredentialId = table.Column<int>(type: "int", nullable: true),
                    GatewayQrId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    GatewayPaymentId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HospitalPaymentQrs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HospitalPaymentQrs_Billings_BillingId",
                        column: x => x.BillingId,
                        principalTable: "Billings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HospitalPaymentQrs_Hospitals_HospitalId",
                        column: x => x.HospitalId,
                        principalTable: "Hospitals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HospitalPaymentQrs_RazorpayMerchantCredentials_RazorpayCredentialId",
                        column: x => x.RazorpayCredentialId,
                        principalTable: "RazorpayMerchantCredentials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HospitalPaymentQrs_BillingId",
                table: "HospitalPaymentQrs",
                column: "BillingId");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalPaymentQrs_GatewayPaymentId",
                table: "HospitalPaymentQrs",
                column: "GatewayPaymentId",
                filter: "[GatewayPaymentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalPaymentQrs_GatewayQrId",
                table: "HospitalPaymentQrs",
                column: "GatewayQrId",
                unique: true,
                filter: "[GatewayQrId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalPaymentQrs_HospitalId_BillingId_Status",
                table: "HospitalPaymentQrs",
                columns: new[] { "HospitalId", "BillingId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_HospitalPaymentQrs_LocalReference",
                table: "HospitalPaymentQrs",
                column: "LocalReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HospitalPaymentQrs_RazorpayCredentialId",
                table: "HospitalPaymentQrs",
                column: "RazorpayCredentialId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HospitalPaymentQrs");
        }
    }
}
