using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations
{
    /// <inheritdoc />
    public partial class AddCmsPaymentWebhookInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CmsPaymentWebhookEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GatewayOrderId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    GatewayPaymentId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    SubscriptionPurchaseId = table.Column<int>(type: "int", nullable: true),
                    ProcessingStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CmsPaymentWebhookEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CmsPaymentWebhookEvents_GatewayOrderId",
                table: "CmsPaymentWebhookEvents",
                column: "GatewayOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CmsPaymentWebhookEvents_Mode_EventId",
                table: "CmsPaymentWebhookEvents",
                columns: new[] { "Mode", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CmsPaymentWebhookEvents_ProcessingStatus_ReceivedAt",
                table: "CmsPaymentWebhookEvents",
                columns: new[] { "ProcessingStatus", "ReceivedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CmsPaymentWebhookEvents");
        }
    }
}
