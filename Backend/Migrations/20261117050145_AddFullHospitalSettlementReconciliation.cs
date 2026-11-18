using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations
{
    /// <inheritdoc />
    public partial class AddFullHospitalSettlementReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Check historical payment associations before changing any columns.
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM dbo.HospitalSettlementRecords AS sr
                    LEFT JOIN dbo.HospitalPaymentTransactions AS pt
                      ON pt.Id = sr.HospitalPaymentTransactionId
                     AND pt.HospitalId = sr.HospitalId
                    WHERE sr.HospitalId <= 0
                       OR sr.HospitalPaymentTransactionId IS NULL
                       OR pt.Id IS NULL
                       OR pt.Mode IS NULL
                       OR pt.Mode NOT IN ('Test', 'Live')
                )
                    THROW 51019, 'Cannot determine historical hospital settlement mode.', 1;
            ");

            migrationBuilder.AlterColumn<string>(
                name: "ReconciliationStatus",
                table: "HospitalSettlementRecords",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "HospitalSettlementRecords",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "INR");

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedGrossAmount",
                table: "HospitalSettlementRecords",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedNetAmount",
                table: "HospitalSettlementRecords",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayPaymentId",
                table: "HospitalSettlementRecords",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewaySettlementTransactionId",
                table: "HospitalSettlementRecords",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayTransactionType",
                table: "HospitalSettlementRecords",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossDifference",
                table: "HospitalSettlementRecords",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReconciliationAttemptAtUtc",
                table: "HospitalSettlementRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mode",
                table: "HospitalSettlementRecords",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Test");

            // Existing records must inherit their actual payment mode.
            // Fail the migration (and roll it back) if a historical record
            // cannot be safely associated with its payment transaction.
            // Preserve the historical reconciliation status; backfill only mode.
            migrationBuilder.Sql(@"
                UPDATE sr
                   SET sr.Mode = pt.Mode
                FROM dbo.HospitalSettlementRecords AS sr
                INNER JOIN dbo.HospitalPaymentTransactions AS pt
                   ON pt.Id = sr.HospitalPaymentTransactionId
                  AND pt.HospitalId = sr.HospitalId;

                IF EXISTS (
                    SELECT 1
                    FROM dbo.HospitalSettlementRecords AS sr
                    INNER JOIN dbo.HospitalPaymentTransactions AS pt
                      ON pt.Id = sr.HospitalPaymentTransactionId
                     AND pt.HospitalId = sr.HospitalId
                    WHERE sr.Mode <> pt.Mode OR sr.Mode NOT IN ('Test', 'Live')
                )
                    THROW 51020, 'Historical settlement mode backfill verification failed.', 1;
            ");

            migrationBuilder.AddColumn<decimal>(
                name: "NetDifference",
                table: "HospitalSettlementRecords",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReconciledAtUtc",
                table: "HospitalSettlementRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReconciliationAttemptCount",
                table: "HospitalSettlementRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReconciliationNotes",
                table: "HospitalSettlementRecords",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresManualReview",
                table: "HospitalSettlementRecords",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "HospitalSettlementRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GatewaySettledAtUtc",
                table: "HospitalPaymentTransactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewaySettlementId",
                table: "HospitalPaymentTransactions",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SettlementFeeAmount",
                table: "HospitalPaymentTransactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SettlementNetAmount",
                table: "HospitalPaymentTransactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SettlementReconciledAtUtc",
                table: "HospitalPaymentTransactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementReconciliationNotes",
                table: "HospitalPaymentTransactions",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementReconciliationStatus",
                table: "HospitalPaymentTransactions",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<decimal>(
                name: "SettlementTaxAmount",
                table: "HospitalPaymentTransactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GatewaySettledAtUtc",
                table: "HospitalPaymentRefunds",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewaySettlementId",
                table: "HospitalPaymentRefunds",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SettlementAdjustmentAmount",
                table: "HospitalPaymentRefunds",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SettlementFeeAmount",
                table: "HospitalPaymentRefunds",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SettlementReconciledAtUtc",
                table: "HospitalPaymentRefunds",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementReconciliationNotes",
                table: "HospitalPaymentRefunds",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementReconciliationStatus",
                table: "HospitalPaymentRefunds",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<decimal>(
                name: "SettlementTaxAmount",
                table: "HospitalPaymentRefunds",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedNetAmount",
                table: "HospitalGatewaySettlements",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "GatewayEvidenceFetchedAtUtc",
                table: "HospitalGatewaySettlements",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GatewayTransactionCount",
                table: "HospitalGatewaySettlements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReconciliationAttemptAtUtc",
                table: "HospitalGatewaySettlements",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MatchedPaymentAmount",
                table: "HospitalGatewaySettlements",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "MatchedPaymentCount",
                table: "HospitalGatewaySettlements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "MatchedRefundAmount",
                table: "HospitalGatewaySettlements",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "MatchedRefundCount",
                table: "HospitalGatewaySettlements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReconciledAtUtc",
                table: "HospitalGatewaySettlements",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReconciliationAttemptCount",
                table: "HospitalGatewaySettlements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReconciliationNotes",
                table: "HospitalGatewaySettlements",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReconciliationStatus",
                table: "HospitalGatewaySettlements",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<bool>(
                name: "RequiresManualReview",
                table: "HospitalGatewaySettlements",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "SettlementDifference",
                table: "HospitalGatewaySettlements",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "UnmatchedGatewayTransactionCount",
                table: "HospitalGatewaySettlements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UnmatchedLocalTransactionCount",
                table: "HospitalGatewaySettlements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UnmatchedTransactionCount",
                table: "HospitalGatewaySettlements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "HospitalGatewaySettlements",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HospitalSettlementRecords_ModeLookup",
                table: "HospitalSettlementRecords",
                columns: new[] { "HospitalId", "Mode", "SettlementId" });

            migrationBuilder.CreateIndex(
                name: "IX_HospitalPaymentTransactions_SettlementLookup",
                table: "HospitalPaymentTransactions",
                columns: new[] { "HospitalId", "Mode", "GatewaySettlementId" },
                filter: "[GatewaySettlementId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalPaymentRefunds_SettlementLookup",
                table: "HospitalPaymentRefunds",
                columns: new[] { "HospitalId", "GatewaySettlementId" },
                filter: "[GatewaySettlementId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalGatewaySettlements_ReconciliationQueue",
                table: "HospitalGatewaySettlements",
                columns: new[] { "HospitalId", "Mode", "ReconciliationStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HospitalSettlementRecords_ModeLookup",
                table: "HospitalSettlementRecords");

            migrationBuilder.DropIndex(
                name: "IX_HospitalPaymentTransactions_SettlementLookup",
                table: "HospitalPaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_HospitalPaymentRefunds_SettlementLookup",
                table: "HospitalPaymentRefunds");

            migrationBuilder.DropIndex(
                name: "IX_HospitalGatewaySettlements_ReconciliationQueue",
                table: "HospitalGatewaySettlements");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "HospitalSettlementRecords");

            migrationBuilder.DropColumn(
                name: "ExpectedGrossAmount",
                table: "HospitalSettlementRecords");

            migrationBuilder.DropColumn(
                name: "ExpectedNetAmount",
                table: "HospitalSettlementRecords");

            migrationBuilder.DropColumn(
                name: "GatewayPaymentId",
                table: "HospitalSettlementRecords");

            migrationBuilder.DropColumn(
                name: "GatewaySettlementTransactionId",
                table: "HospitalSettlementRecords");

            migrationBuilder.DropColumn(
                name: "GatewayTransactionType",
                table: "HospitalSettlementRecords");

            migrationBuilder.DropColumn(
                name: "GrossDifference",
                table: "HospitalSettlementRecords");

            migrationBuilder.DropColumn(
                name: "LastReconciliationAttemptAtUtc",
                table: "HospitalSettlementRecords");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "HospitalSettlementRecords");

            migrationBuilder.DropColumn(
                name: "NetDifference",
                table: "HospitalSettlementRecords");

            migrationBuilder.DropColumn(
                name: "ReconciledAtUtc",
                table: "HospitalSettlementRecords");

            migrationBuilder.DropColumn(
                name: "ReconciliationAttemptCount",
                table: "HospitalSettlementRecords");

            migrationBuilder.DropColumn(
                name: "ReconciliationNotes",
                table: "HospitalSettlementRecords");

            migrationBuilder.DropColumn(
                name: "RequiresManualReview",
                table: "HospitalSettlementRecords");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "HospitalSettlementRecords");

            migrationBuilder.DropColumn(
                name: "GatewaySettledAtUtc",
                table: "HospitalPaymentTransactions");

            migrationBuilder.DropColumn(
                name: "GatewaySettlementId",
                table: "HospitalPaymentTransactions");

            migrationBuilder.DropColumn(
                name: "SettlementFeeAmount",
                table: "HospitalPaymentTransactions");

            migrationBuilder.DropColumn(
                name: "SettlementNetAmount",
                table: "HospitalPaymentTransactions");

            migrationBuilder.DropColumn(
                name: "SettlementReconciledAtUtc",
                table: "HospitalPaymentTransactions");

            migrationBuilder.DropColumn(
                name: "SettlementReconciliationNotes",
                table: "HospitalPaymentTransactions");

            migrationBuilder.DropColumn(
                name: "SettlementReconciliationStatus",
                table: "HospitalPaymentTransactions");

            migrationBuilder.DropColumn(
                name: "SettlementTaxAmount",
                table: "HospitalPaymentTransactions");

            migrationBuilder.DropColumn(
                name: "GatewaySettledAtUtc",
                table: "HospitalPaymentRefunds");

            migrationBuilder.DropColumn(
                name: "GatewaySettlementId",
                table: "HospitalPaymentRefunds");

            migrationBuilder.DropColumn(
                name: "SettlementAdjustmentAmount",
                table: "HospitalPaymentRefunds");

            migrationBuilder.DropColumn(
                name: "SettlementFeeAmount",
                table: "HospitalPaymentRefunds");

            migrationBuilder.DropColumn(
                name: "SettlementReconciledAtUtc",
                table: "HospitalPaymentRefunds");

            migrationBuilder.DropColumn(
                name: "SettlementReconciliationNotes",
                table: "HospitalPaymentRefunds");

            migrationBuilder.DropColumn(
                name: "SettlementReconciliationStatus",
                table: "HospitalPaymentRefunds");

            migrationBuilder.DropColumn(
                name: "SettlementTaxAmount",
                table: "HospitalPaymentRefunds");

            migrationBuilder.DropColumn(
                name: "ExpectedNetAmount",
                table: "HospitalGatewaySettlements");

            migrationBuilder.DropColumn(
                name: "GatewayEvidenceFetchedAtUtc",
                table: "HospitalGatewaySettlements");

            migrationBuilder.DropColumn(
                name: "GatewayTransactionCount",
                table: "HospitalGatewaySettlements");

            migrationBuilder.DropColumn(
                name: "LastReconciliationAttemptAtUtc",
                table: "HospitalGatewaySettlements");

            migrationBuilder.DropColumn(
                name: "MatchedPaymentAmount",
                table: "HospitalGatewaySettlements");

            migrationBuilder.DropColumn(
                name: "MatchedPaymentCount",
                table: "HospitalGatewaySettlements");

            migrationBuilder.DropColumn(
                name: "MatchedRefundAmount",
                table: "HospitalGatewaySettlements");

            migrationBuilder.DropColumn(
                name: "MatchedRefundCount",
                table: "HospitalGatewaySettlements");

            migrationBuilder.DropColumn(
                name: "ReconciledAtUtc",
                table: "HospitalGatewaySettlements");

            migrationBuilder.DropColumn(
                name: "ReconciliationAttemptCount",
                table: "HospitalGatewaySettlements");

            migrationBuilder.DropColumn(
                name: "ReconciliationNotes",
                table: "HospitalGatewaySettlements");

            migrationBuilder.DropColumn(
                name: "ReconciliationStatus",
                table: "HospitalGatewaySettlements");

            migrationBuilder.DropColumn(
                name: "RequiresManualReview",
                table: "HospitalGatewaySettlements");

            migrationBuilder.DropColumn(
                name: "SettlementDifference",
                table: "HospitalGatewaySettlements");

            migrationBuilder.DropColumn(
                name: "UnmatchedGatewayTransactionCount",
                table: "HospitalGatewaySettlements");

            migrationBuilder.DropColumn(
                name: "UnmatchedLocalTransactionCount",
                table: "HospitalGatewaySettlements");

            migrationBuilder.DropColumn(
                name: "UnmatchedTransactionCount",
                table: "HospitalGatewaySettlements");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "HospitalGatewaySettlements");

            migrationBuilder.AlterColumn<string>(
                name: "ReconciliationStatus",
                table: "HospitalSettlementRecords",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldDefaultValue: "Pending");

        }
    }
}
