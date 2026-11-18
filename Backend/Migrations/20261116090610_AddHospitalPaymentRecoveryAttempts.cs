using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations
{
    /// <inheritdoc />
    public partial class AddHospitalPaymentRecoveryAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_HospitalPaymentTransactions_Source",
                table: "HospitalPaymentTransactions");

            migrationBuilder.CreateTable(
                name: "HospitalPaymentRecoveryAttempts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HospitalId = table.Column<int>(type: "int", nullable: false),
                    PaymentSource = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GatewayPaymentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HospitalPaymentOrderId = table.Column<long>(type: "bigint", nullable: true),
                    HospitalPaymentQrId = table.Column<long>(type: "bigint", nullable: true),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextRetryAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HospitalPaymentRecoveryAttempts", x => x.Id);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_HospitalPaymentTransactions_Source",
                table: "HospitalPaymentTransactions",
                sql: "([PaymentSource] = 'Order' AND [HospitalPaymentOrderId] IS NOT NULL AND [HospitalPaymentQrId] IS NULL AND [GatewayOrderId] IS NOT NULL) OR ([PaymentSource] = 'Qr' AND [HospitalPaymentQrId] IS NOT NULL AND [HospitalPaymentOrderId] IS NULL AND [GatewayOrderId] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalPaymentRecoveryAttempts_HospitalId_GatewayPaymentId",
                table: "HospitalPaymentRecoveryAttempts",
                columns: new[] { "HospitalId", "GatewayPaymentId" });

            migrationBuilder.CreateIndex(
                name: "IX_HospitalPaymentRecoveryAttempts_HospitalId_Status_NextRetryAtUtc",
                table: "HospitalPaymentRecoveryAttempts",
                columns: new[] { "HospitalId", "Status", "NextRetryAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HospitalPaymentRecoveryAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HospitalPaymentTransactions_Source",
                table: "HospitalPaymentTransactions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HospitalPaymentTransactions_Source",
                table: "HospitalPaymentTransactions",
                sql: "([PaymentSource] = 'Order' AND [HospitalPaymentOrderId] IS NOT NULL AND [HospitalPaymentQrId] IS NULL) OR ([PaymentSource] = 'Qr' AND [HospitalPaymentQrId] IS NOT NULL AND [HospitalPaymentOrderId] IS NULL)");
        }
    }
}
