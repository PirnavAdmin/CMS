using AuthDemo.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace AuthDemo.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261110153000_AddCmsRefundSettlementReconciliation")]
public partial class AddCmsRefundSettlementReconciliation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CmsSubscriptionRefunds",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                SubscriptionPurchaseId = table.Column<int>(type: "int", nullable: false),
                HospitalId = table.Column<int>(type: "int", nullable: true),
                GatewayRefundId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                Mode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CmsSubscriptionRefunds", x => x.Id);
                table.ForeignKey("FK_CmsSubscriptionRefunds_SubscriptionPurchases_SubscriptionPurchaseId", x => x.SubscriptionPurchaseId, "SubscriptionPurchases", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CmsSubscriptionSettlementRecords",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                SubscriptionPurchaseId = table.Column<int>(type: "int", nullable: false),
                GatewayPaymentId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                SettlementId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Mode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                GrossAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                FeeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                NetAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                SettledAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CmsSubscriptionSettlementRecords", x => x.Id);
                table.ForeignKey("FK_CmsSubscriptionSettlementRecords_SubscriptionPurchases_SubscriptionPurchaseId", x => x.SubscriptionPurchaseId, "SubscriptionPurchases", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_CmsSubscriptionRefunds_SubscriptionPurchaseId", "CmsSubscriptionRefunds", "SubscriptionPurchaseId");
        migrationBuilder.CreateIndex("IX_CmsSubscriptionRefunds_GatewayRefundId", "CmsSubscriptionRefunds", "GatewayRefundId", unique: true);
        migrationBuilder.CreateIndex("IX_CmsSubscriptionSettlementRecords_SubscriptionPurchaseId", "CmsSubscriptionSettlementRecords", "SubscriptionPurchaseId");
        migrationBuilder.CreateIndex("IX_CmsSubscriptionSettlementRecords_SubscriptionPurchaseId_SettlementId_GatewayPaymentId", "CmsSubscriptionSettlementRecords", new[] { "SubscriptionPurchaseId", "SettlementId", "GatewayPaymentId" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CmsSubscriptionRefunds");
        migrationBuilder.DropTable(name: "CmsSubscriptionSettlementRecords");
    }
}
