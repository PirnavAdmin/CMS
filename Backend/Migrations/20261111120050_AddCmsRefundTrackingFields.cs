using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations
{
    /// <inheritdoc />
    public partial class AddCmsRefundTrackingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CmsSubscriptionRefunds_SubscriptionPurchaseId",
                table: "CmsSubscriptionRefunds");

            migrationBuilder.AddColumn<string>(
                name: "GatewayPaymentId",
                table: "CmsSubscriptionRefunds",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "CmsSubscriptionRefunds",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CmsSubscriptionRefunds_GatewayPaymentId",
                table: "CmsSubscriptionRefunds",
                column: "GatewayPaymentId",
                filter: "[GatewayPaymentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CmsSubscriptionRefunds_SubscriptionPurchaseId_IdempotencyKey",
                table: "CmsSubscriptionRefunds",
                columns: new[] { "SubscriptionPurchaseId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CmsSubscriptionRefunds_GatewayPaymentId",
                table: "CmsSubscriptionRefunds");

            migrationBuilder.DropIndex(
                name: "IX_CmsSubscriptionRefunds_SubscriptionPurchaseId_IdempotencyKey",
                table: "CmsSubscriptionRefunds");

            migrationBuilder.DropColumn(
                name: "GatewayPaymentId",
                table: "CmsSubscriptionRefunds");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "CmsSubscriptionRefunds");

            migrationBuilder.CreateIndex(
                name: "IX_CmsSubscriptionRefunds_SubscriptionPurchaseId",
                table: "CmsSubscriptionRefunds",
                column: "SubscriptionPurchaseId");
        }
    }
}
