using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionPurchaseType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "PaymentAttemptCount",
                table: "SubscriptionPurchases",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "PurchaseType",
                table: "SubscriptionPurchases",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Initial");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPurchases_AdminUserId_PaymentStatus",
                table: "SubscriptionPurchases",
                columns: new[] { "AdminUserId", "PaymentStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPurchases_HospitalId_ActivatedAt",
                table: "SubscriptionPurchases",
                columns: new[] { "HospitalId", "ActivatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPurchases_PurchaseType_AdminUserId_HospitalId",
                table: "SubscriptionPurchases",
                columns: new[] { "PurchaseType", "AdminUserId", "HospitalId" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPurchases_TransactionReference",
                table: "SubscriptionPurchases",
                column: "TransactionReference",
                unique: true,
                filter: "[TransactionReference] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SubscriptionPurchases_AdminUserId_PaymentStatus",
                table: "SubscriptionPurchases");

            migrationBuilder.DropIndex(
                name: "IX_SubscriptionPurchases_HospitalId_ActivatedAt",
                table: "SubscriptionPurchases");

            migrationBuilder.DropIndex(
                name: "IX_SubscriptionPurchases_PurchaseType_AdminUserId_HospitalId",
                table: "SubscriptionPurchases");

            migrationBuilder.DropIndex(
                name: "IX_SubscriptionPurchases_TransactionReference",
                table: "SubscriptionPurchases");

            migrationBuilder.DropColumn(
                name: "PurchaseType",
                table: "SubscriptionPurchases");

            migrationBuilder.AlterColumn<int>(
                name: "PaymentAttemptCount",
                table: "SubscriptionPurchases",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);
        }
    }
}
