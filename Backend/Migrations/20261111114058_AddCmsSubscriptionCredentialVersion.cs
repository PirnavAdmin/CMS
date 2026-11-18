using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations
{
    /// <inheritdoc />
    public partial class AddCmsSubscriptionCredentialVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RazorpayCredentialId",
                table: "SubscriptionPurchases",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPurchases_RazorpayCredentialId",
                table: "SubscriptionPurchases",
                column: "RazorpayCredentialId");

            migrationBuilder.AddForeignKey(
                name: "FK_SubscriptionPurchases_RazorpayMerchantCredentials_RazorpayCredentialId",
                table: "SubscriptionPurchases",
                column: "RazorpayCredentialId",
                principalTable: "RazorpayMerchantCredentials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubscriptionPurchases_RazorpayMerchantCredentials_RazorpayCredentialId",
                table: "SubscriptionPurchases");

            migrationBuilder.DropIndex(
                name: "IX_SubscriptionPurchases_RazorpayCredentialId",
                table: "SubscriptionPurchases");

            migrationBuilder.DropColumn(
                name: "RazorpayCredentialId",
                table: "SubscriptionPurchases");
        }
    }
}
