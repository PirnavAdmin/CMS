using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations
{
    /// <inheritdoc />
    public partial class AddHospitalPaymentRecoverySafeguards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "GatewayPaymentId",
                table: "HospitalPaymentRecoveryAttempts",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "UX_HospitalPaymentRecoveryAttempts_PaymentAttempt",
                table: "HospitalPaymentRecoveryAttempts",
                columns: new[] { "HospitalId", "GatewayPaymentId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_BillingPaymentTransactions_RazorpayReference",
                table: "BillingPaymentTransactions",
                column: "ReferenceNumber",
                unique: true,
                filter: "[PaymentMethod] = 'Razorpay' AND [ReferenceNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_HospitalPaymentRecoveryAttempts_PaymentAttempt",
                table: "HospitalPaymentRecoveryAttempts");

            migrationBuilder.DropIndex(
                name: "UX_BillingPaymentTransactions_RazorpayReference",
                table: "BillingPaymentTransactions");

            migrationBuilder.AlterColumn<string>(
                name: "GatewayPaymentId",
                table: "HospitalPaymentRecoveryAttempts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);
        }
    }
}
