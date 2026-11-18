using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations
{
    /// <inheritdoc />
    public partial class AddCmsRazorpayPaymentVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GatewayMode",
                table: "SubscriptionPurchases",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GatewayVerifiedAt",
                table: "SubscriptionPurchases",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsGatewayPaymentVerified",
                table: "SubscriptionPurchases",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GatewayMode",
                table: "SubscriptionPurchases");

            migrationBuilder.DropColumn(
                name: "GatewayVerifiedAt",
                table: "SubscriptionPurchases");

            migrationBuilder.DropColumn(
                name: "IsGatewayPaymentVerified",
                table: "SubscriptionPurchases");
        }
    }
}
