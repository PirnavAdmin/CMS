using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations
{
    /// <inheritdoc />
    public partial class SupportQrPaymentTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "HospitalPaymentOrderId",
                table: "HospitalPaymentTransactions",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "GatewayOrderId",
                table: "HospitalPaymentTransactions",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AddColumn<long>(
                name: "HospitalPaymentQrId",
                table: "HospitalPaymentTransactions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentSource",
                table: "HospitalPaymentTransactions",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Order");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalPaymentTransactions_HospitalPaymentQrId",
                table: "HospitalPaymentTransactions",
                column: "HospitalPaymentQrId",
                filter: "[HospitalPaymentQrId] IS NOT NULL");

            // Enforce exactly one source, and require an order gateway ID only
            // when the transaction is backed by a Razorpay order.
            migrationBuilder.AddCheckConstraint(
                name: "CK_HospitalPaymentTransactions_Source",
                table: "HospitalPaymentTransactions",
                sql: @"(
                    [PaymentSource] = 'Order'
                    AND [HospitalPaymentOrderId] IS NOT NULL
                    AND [HospitalPaymentQrId] IS NULL
                    AND [GatewayOrderId] IS NOT NULL
                ) OR (
                    [PaymentSource] = 'Qr'
                    AND [HospitalPaymentQrId] IS NOT NULL
                    AND [HospitalPaymentOrderId] IS NULL
                    AND [GatewayOrderId] IS NULL
                )");

            migrationBuilder.AddForeignKey(
                name: "FK_HospitalPaymentTransactions_HospitalPaymentQrs_HospitalPaymentQrId",
                table: "HospitalPaymentTransactions",
                column: "HospitalPaymentQrId",
                principalTable: "HospitalPaymentQrs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A rollback requires removing/migrating QR-backed transactions first.
            // SQL Server will reject restoring NOT NULL columns while QR rows exist.
            migrationBuilder.DropForeignKey(
                name: "FK_HospitalPaymentTransactions_HospitalPaymentQrs_HospitalPaymentQrId",
                table: "HospitalPaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_HospitalPaymentTransactions_HospitalPaymentQrId",
                table: "HospitalPaymentTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HospitalPaymentTransactions_Source",
                table: "HospitalPaymentTransactions");

            migrationBuilder.DropColumn(
                name: "HospitalPaymentQrId",
                table: "HospitalPaymentTransactions");

            migrationBuilder.DropColumn(
                name: "PaymentSource",
                table: "HospitalPaymentTransactions");

            migrationBuilder.AlterColumn<long>(
                name: "HospitalPaymentOrderId",
                table: "HospitalPaymentTransactions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GatewayOrderId",
                table: "HospitalPaymentTransactions",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150,
                oldNullable: true);
        }
    }
}
