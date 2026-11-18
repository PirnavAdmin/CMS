using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionPaymentTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastPaymentAttemptAt",
                table: "SubscriptionPurchases",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentAttemptCount",
                table: "SubscriptionPurchases",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PaymentFailureReason",
                table: "SubscriptionPurchases",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionReference",
                table: "SubscriptionPurchases",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastPaymentAttemptAt",
                table: "SubscriptionPurchases");

            migrationBuilder.DropColumn(
                name: "PaymentAttemptCount",
                table: "SubscriptionPurchases");

            migrationBuilder.DropColumn(
                name: "PaymentFailureReason",
                table: "SubscriptionPurchases");

            migrationBuilder.DropColumn(
                name: "TransactionReference",
                table: "SubscriptionPurchases");
        }
    }
}
