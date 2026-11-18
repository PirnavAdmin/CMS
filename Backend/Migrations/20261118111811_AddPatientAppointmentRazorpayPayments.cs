using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientAppointmentRazorpayPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "UnmatchedTransactionCount",
                table: "HospitalGatewaySettlements",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "MatchedRefundCount",
                table: "HospitalGatewaySettlements",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "MatchedPaymentCount",
                table: "HospitalGatewaySettlements",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateTable(
                name: "PatientAppointmentPayments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppointmentId = table.Column<int>(type: "int", nullable: false),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    HospitalId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LocalOrderId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RazorpayOrderId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    RazorpayPaymentId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    RazorpayCredentialId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientAppointmentPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientAppointmentPayments_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientAppointmentPayments_RazorpayMerchantCredentials_RazorpayCredentialId",
                        column: x => x.RazorpayCredentialId,
                        principalTable: "RazorpayMerchantCredentials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientAppointmentPayments_AppointmentId_Status",
                table: "PatientAppointmentPayments",
                columns: new[] { "AppointmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientAppointmentPayments_LocalOrderId",
                table: "PatientAppointmentPayments",
                column: "LocalOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientAppointmentPayments_RazorpayCredentialId",
                table: "PatientAppointmentPayments",
                column: "RazorpayCredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientAppointmentPayments_RazorpayOrderId",
                table: "PatientAppointmentPayments",
                column: "RazorpayOrderId",
                unique: true,
                filter: "[RazorpayOrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PatientAppointmentPayments_RazorpayPaymentId",
                table: "PatientAppointmentPayments",
                column: "RazorpayPaymentId",
                unique: true,
                filter: "[RazorpayPaymentId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PatientAppointmentPayments");

            migrationBuilder.AlterColumn<int>(
                name: "UnmatchedTransactionCount",
                table: "HospitalGatewaySettlements",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "MatchedRefundCount",
                table: "HospitalGatewaySettlements",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "MatchedPaymentCount",
                table: "HospitalGatewaySettlements",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);
        }
    }
}
