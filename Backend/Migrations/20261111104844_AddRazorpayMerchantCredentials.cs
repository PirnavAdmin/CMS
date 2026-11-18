using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations
{
    /// <inheritdoc />
    public partial class AddRazorpayMerchantCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HospitalSettlementRecords_HospitalId",
                table: "HospitalSettlementRecords");

            migrationBuilder.DropIndex(
                name: "IX_HospitalPaymentTransactions_HospitalId",
                table: "HospitalPaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_HospitalPaymentOrders_HospitalId",
                table: "HospitalPaymentOrders");

            migrationBuilder.DropIndex(
                name: "IX_CmsSubscriptionSettlementRecords_SubscriptionPurchaseId",
                table: "CmsSubscriptionSettlementRecords");

            migrationBuilder.CreateTable(
                name: "RazorpayMerchantCredentials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MerchantType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    HospitalId = table.Column<int>(type: "int", nullable: true),
                    Mode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    KeyId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EncryptedKeySecret = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EncryptedWebhookSecret = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RetiredAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RazorpayMerchantCredentials", x => x.Id);
                    table.CheckConstraint("CK_RazorpayCredentials_Hospital", "([MerchantType] = 'CMS' AND [HospitalId] IS NULL) OR ([MerchantType] = 'Hospital' AND [HospitalId] IS NOT NULL)");
                    table.CheckConstraint("CK_RazorpayCredentials_MerchantType", "[MerchantType] IN ('CMS', 'Hospital')");
                    table.CheckConstraint("CK_RazorpayCredentials_Mode", "[Mode] IN ('Test', 'Live')");
                    table.ForeignKey(
                        name: "FK_RazorpayMerchantCredentials_Hospitals_HospitalId",
                        column: x => x.HospitalId,
                        principalTable: "Hospitals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HospitalPaymentDisputes_HospitalPaymentTransactionId",
                table: "HospitalPaymentDisputes",
                column: "HospitalPaymentTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_RazorpayMerchantCredentials_HospitalId_Mode",
                table: "RazorpayMerchantCredentials",
                columns: new[] { "HospitalId", "Mode" },
                unique: true,
                filter: "[MerchantType] = 'Hospital' AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_RazorpayMerchantCredentials_MerchantType_Mode",
                table: "RazorpayMerchantCredentials",
                columns: new[] { "MerchantType", "Mode" },
                unique: true,
                filter: "[MerchantType] = 'CMS' AND [IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RazorpayMerchantCredentials");

            migrationBuilder.DropIndex(
                name: "IX_HospitalPaymentDisputes_HospitalPaymentTransactionId",
                table: "HospitalPaymentDisputes");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalSettlementRecords_HospitalId",
                table: "HospitalSettlementRecords",
                column: "HospitalId");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalPaymentTransactions_HospitalId",
                table: "HospitalPaymentTransactions",
                column: "HospitalId");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalPaymentOrders_HospitalId",
                table: "HospitalPaymentOrders",
                column: "HospitalId");

            migrationBuilder.CreateIndex(
                name: "IX_CmsSubscriptionSettlementRecords_SubscriptionPurchaseId",
                table: "CmsSubscriptionSettlementRecords",
                column: "SubscriptionPurchaseId");
        }
    }
}
