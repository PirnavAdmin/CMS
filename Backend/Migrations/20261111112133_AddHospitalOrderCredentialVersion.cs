using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations
{
    /// <inheritdoc />
    public partial class AddHospitalOrderCredentialVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RazorpayCredentialId",
                table: "HospitalPaymentOrders",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HospitalPaymentOrders_RazorpayCredentialId",
                table: "HospitalPaymentOrders",
                column: "RazorpayCredentialId");

            migrationBuilder.AddForeignKey(
                name: "FK_HospitalPaymentOrders_RazorpayMerchantCredentials_RazorpayCredentialId",
                table: "HospitalPaymentOrders",
                column: "RazorpayCredentialId",
                principalTable: "RazorpayMerchantCredentials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HospitalPaymentOrders_RazorpayMerchantCredentials_RazorpayCredentialId",
                table: "HospitalPaymentOrders");

            migrationBuilder.DropIndex(
                name: "IX_HospitalPaymentOrders_RazorpayCredentialId",
                table: "HospitalPaymentOrders");

            migrationBuilder.DropColumn(
                name: "RazorpayCredentialId",
                table: "HospitalPaymentOrders");
        }
    }
}
