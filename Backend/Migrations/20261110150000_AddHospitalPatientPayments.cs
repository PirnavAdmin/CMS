using AuthDemo.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261110150000_AddHospitalPatientPayments")]
public partial class AddHospitalPatientPayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "HospitalPaymentOrders",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                LocalOrderId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                HospitalId = table.Column<int>(type: "int", nullable: false),
                BillingId = table.Column<int>(type: "int", nullable: false),
                PatientId = table.Column<int>(type: "int", nullable: false),
                BranchId = table.Column<int>(type: "int", nullable: true),
                Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                Mode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                GatewayOrderId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                GatewayPaymentId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HospitalPaymentOrders", x => x.Id);
                table.ForeignKey("FK_HospitalPaymentOrders_Billings_BillingId", x => x.BillingId, "Billings", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_HospitalPaymentOrders_Hospitals_HospitalId", x => x.HospitalId, "Hospitals", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "HospitalPaymentWebhookEvents",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                EventId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                HospitalId = table.Column<int>(type: "int", nullable: false),
                Mode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                GatewayOrderId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                ProcessingStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_HospitalPaymentWebhookEvents", x => x.Id));

        migrationBuilder.CreateTable(
            name: "HospitalPaymentTransactions",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                HospitalPaymentOrderId = table.Column<long>(type: "bigint", nullable: false),
                HospitalId = table.Column<int>(type: "int", nullable: false),
                BillingId = table.Column<int>(type: "int", nullable: false),
                BillingPaymentTransactionId = table.Column<int>(type: "int", nullable: false),
                GatewayPaymentId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                GatewayOrderId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                RefundedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                Mode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                CapturedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HospitalPaymentTransactions", x => x.Id);
                table.ForeignKey("FK_HospitalPaymentTransactions_HospitalPaymentOrders_HospitalPaymentOrderId", x => x.HospitalPaymentOrderId, "HospitalPaymentOrders", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_HospitalPaymentTransactions_Billings_BillingId", x => x.BillingId, "Billings", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_HospitalPaymentTransactions_Hospitals_HospitalId", x => x.HospitalId, "Hospitals", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "HospitalPaymentRefunds",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                HospitalPaymentTransactionId = table.Column<long>(type: "bigint", nullable: false),
                HospitalId = table.Column<int>(type: "int", nullable: false),
                BillingId = table.Column<int>(type: "int", nullable: false),
                GatewayRefundId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HospitalPaymentRefunds", x => x.Id);
                table.ForeignKey("FK_HospitalPaymentRefunds_HospitalPaymentTransactions_HospitalPaymentTransactionId", x => x.HospitalPaymentTransactionId, "HospitalPaymentTransactions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_HospitalPaymentRefunds_Hospitals_HospitalId", x => x.HospitalId, "Hospitals", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "HospitalSettlementRecords",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                HospitalId = table.Column<int>(type: "int", nullable: false),
                HospitalPaymentTransactionId = table.Column<long>(type: "bigint", nullable: false),
                SettlementId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                GrossAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                FeeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                NetAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                SettledAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ReconciliationStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HospitalSettlementRecords", x => x.Id);
                table.ForeignKey("FK_HospitalSettlementRecords_HospitalPaymentTransactions_HospitalPaymentTransactionId", x => x.HospitalPaymentTransactionId, "HospitalPaymentTransactions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_HospitalSettlementRecords_Hospitals_HospitalId", x => x.HospitalId, "Hospitals", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_HospitalPaymentOrders_BillingId", "HospitalPaymentOrders", "BillingId");
        migrationBuilder.CreateIndex("IX_HospitalPaymentOrders_HospitalId", "HospitalPaymentOrders", "HospitalId");
        migrationBuilder.CreateIndex("IX_HospitalPaymentOrders_LocalOrderId", "HospitalPaymentOrders", "LocalOrderId", unique: true);
        migrationBuilder.CreateIndex("IX_HospitalPaymentOrders_GatewayOrderId", "HospitalPaymentOrders", "GatewayOrderId", unique: true, filter: "[GatewayOrderId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_HospitalPaymentOrders_HospitalId_IdempotencyKey", "HospitalPaymentOrders", new[] { "HospitalId", "IdempotencyKey" }, unique: true, filter: "[IdempotencyKey] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_HospitalPaymentOrders_HospitalId_BillingId_Status", "HospitalPaymentOrders", new[] { "HospitalId", "BillingId", "Status" });
        migrationBuilder.CreateIndex("IX_HospitalPaymentWebhookEvents_Mode_EventId", "HospitalPaymentWebhookEvents", new[] { "Mode", "EventId" }, unique: true);
        migrationBuilder.CreateIndex("IX_HospitalPaymentWebhookEvents_HospitalId_ReceivedAt", "HospitalPaymentWebhookEvents", new[] { "HospitalId", "ReceivedAt" });
        migrationBuilder.CreateIndex("IX_HospitalPaymentTransactions_HospitalPaymentOrderId", "HospitalPaymentTransactions", "HospitalPaymentOrderId");
        migrationBuilder.CreateIndex("IX_HospitalPaymentTransactions_BillingId", "HospitalPaymentTransactions", "BillingId");
        migrationBuilder.CreateIndex("IX_HospitalPaymentTransactions_HospitalId", "HospitalPaymentTransactions", "HospitalId");
        migrationBuilder.CreateIndex("IX_HospitalPaymentTransactions_GatewayPaymentId", "HospitalPaymentTransactions", "GatewayPaymentId", unique: true);
        migrationBuilder.CreateIndex("IX_HospitalPaymentTransactions_HospitalId_BillingId", "HospitalPaymentTransactions", new[] { "HospitalId", "BillingId" });
        migrationBuilder.CreateIndex("IX_HospitalPaymentRefunds_HospitalPaymentTransactionId", "HospitalPaymentRefunds", "HospitalPaymentTransactionId");
        migrationBuilder.CreateIndex("IX_HospitalPaymentRefunds_HospitalId", "HospitalPaymentRefunds", "HospitalId");
        migrationBuilder.CreateIndex("IX_HospitalPaymentRefunds_GatewayRefundId", "HospitalPaymentRefunds", "GatewayRefundId", unique: true);
        migrationBuilder.CreateIndex("IX_HospitalSettlementRecords_HospitalPaymentTransactionId", "HospitalSettlementRecords", "HospitalPaymentTransactionId");
        migrationBuilder.CreateIndex("IX_HospitalSettlementRecords_HospitalId", "HospitalSettlementRecords", "HospitalId");
        migrationBuilder.CreateIndex("IX_HospitalSettlementRecords_HospitalId_SettlementId_HospitalPaymentTransactionId", "HospitalSettlementRecords", new[] { "HospitalId", "SettlementId", "HospitalPaymentTransactionId" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "HospitalPaymentRefunds");
        migrationBuilder.DropTable(name: "HospitalSettlementRecords");
        migrationBuilder.DropTable(name: "HospitalPaymentWebhookEvents");
        migrationBuilder.DropTable(name: "HospitalPaymentTransactions");
        migrationBuilder.DropTable(name: "HospitalPaymentOrders");
    }
}
