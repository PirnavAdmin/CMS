using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations
{
    public partial class AddClinicSubscriptionLimitsAndUpgrades : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(name: "FinalPayableAmount", table: "SubscriptionPurchases", type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<decimal>(name: "GrossNewPlanAmount", table: "SubscriptionPurchases", type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<decimal>(name: "OriginalPaidAmount", table: "SubscriptionPurchases", type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<int>(name: "OriginalSubscriptionId", table: "SubscriptionPurchases", type: "int", nullable: true);
            migrationBuilder.AddColumn<int>(name: "PurchasedDailyAppointmentLimit", table: "SubscriptionPurchases", type: "int", nullable: false, defaultValue: 20);
            migrationBuilder.AddColumn<int>(name: "PurchasedMaxBranches", table: "SubscriptionPurchases", type: "int", nullable: false, defaultValue: 1);
            migrationBuilder.AddColumn<decimal>(name: "UnusedCreditAmount", table: "SubscriptionPurchases", type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<string>(name: "UpgradeMode", table: "SubscriptionPurchases", type: "nvarchar(30)", maxLength: 30, nullable: true);
            migrationBuilder.AddColumn<int>(name: "DailyAppointmentLimit", table: "SubscriptionPlans", type: "int", nullable: false, defaultValue: 20);
            migrationBuilder.AddColumn<int>(name: "MaxBranches", table: "SubscriptionPlans", type: "int", nullable: false, defaultValue: 1);
            migrationBuilder.AddColumn<int>(name: "PurchasedDailyAppointmentLimit", table: "ClinicSubscriptions", type: "int", nullable: false, defaultValue: 20);
            migrationBuilder.AddColumn<int>(name: "PurchasedMaxBranches", table: "ClinicSubscriptions", type: "int", nullable: false, defaultValue: 1);
            migrationBuilder.AddColumn<DateTime>(name: "SupersededAt", table: "ClinicSubscriptions", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<int>(name: "SupersededBySubscriptionId", table: "ClinicSubscriptions", type: "int", nullable: true);

            // This schema did not previously store purchased branch/appointment limits.
            // Defaults are placeholders, not evidence of what existing customers purchased.
            // Update historical plans and entitlements explicitly after reviewing contracts.
            // Existing paid purchases can safely retain their actual checkout Amount as
            // the gross and payable amount for initial/renewal purchases.
            migrationBuilder.Sql(@"
UPDATE [SubscriptionPurchases]
SET [OriginalPaidAmount] = CASE WHEN [PaymentStatus] = 'Paid' THEN [Amount] ELSE 0 END,
    [GrossNewPlanAmount] = [Amount],
    [FinalPayableAmount] = [Amount]
WHERE [PurchaseType] IN ('Initial', 'Renewal');

UPDATE cp
SET cp.[PurchasedMaxBranches] = sp.[MaxBranches],
    cp.[PurchasedDailyAppointmentLimit] = sp.[DailyAppointmentLimit]
FROM [ClinicSubscriptions] AS cp
INNER JOIN [SubscriptionPlans] AS sp ON sp.[Id] = cp.[SubscriptionPlanId];

UPDATE p
SET p.[PurchasedMaxBranches] = sp.[MaxBranches],
    p.[PurchasedDailyAppointmentLimit] = sp.[DailyAppointmentLimit]
FROM [SubscriptionPurchases] AS p
INNER JOIN [SubscriptionPlans] AS sp ON sp.[Id] = p.[SubscriptionPlanId];
");

            // Fail clearly rather than silently discard or rewrite duplicate gateway IDs.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [SubscriptionPurchases] WHERE [GatewayOrderId] IS NOT NULL GROUP BY [GatewayOrderId] HAVING COUNT(*) > 1)
    THROW 51001, 'Duplicate non-null GatewayOrderId values: resolve before applying migration.', 1;
IF EXISTS (SELECT 1 FROM [SubscriptionPurchases] WHERE [GatewayPaymentId] IS NOT NULL GROUP BY [GatewayPaymentId] HAVING COUNT(*) > 1)
    THROW 51002, 'Duplicate non-null GatewayPaymentId values: resolve before applying migration.', 1;
");

            migrationBuilder.CreateIndex(name: "IX_SubscriptionPurchases_GatewayOrderId", table: "SubscriptionPurchases", column: "GatewayOrderId", unique: true, filter: "[GatewayOrderId] IS NOT NULL");
            migrationBuilder.CreateIndex(name: "IX_SubscriptionPurchases_GatewayPaymentId", table: "SubscriptionPurchases", column: "GatewayPaymentId", unique: true, filter: "[GatewayPaymentId] IS NOT NULL");
            migrationBuilder.CreateIndex(name: "IX_SubscriptionPurchases_OriginalSubscriptionId", table: "SubscriptionPurchases", column: "OriginalSubscriptionId");
            migrationBuilder.CreateIndex(name: "IX_ClinicSubscriptions_HospitalId_Status_StartDate_EndDate", table: "ClinicSubscriptions", columns: new[] { "HospitalId", "Status", "StartDate", "EndDate" });
            migrationBuilder.CreateIndex(name: "IX_ClinicSubscriptions_SupersededBySubscriptionId", table: "ClinicSubscriptions", column: "SupersededBySubscriptionId");
            migrationBuilder.AddForeignKey(name: "FK_ClinicSubscriptions_ClinicSubscriptions_SupersededBySubscriptionId", table: "ClinicSubscriptions", column: "SupersededBySubscriptionId", principalTable: "ClinicSubscriptions", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_SubscriptionPurchases_ClinicSubscriptions_OriginalSubscriptionId", table: "SubscriptionPurchases", column: "OriginalSubscriptionId", principalTable: "ClinicSubscriptions", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_ClinicSubscriptions_ClinicSubscriptions_SupersededBySubscriptionId", table: "ClinicSubscriptions");
            migrationBuilder.DropForeignKey(name: "FK_SubscriptionPurchases_ClinicSubscriptions_OriginalSubscriptionId", table: "SubscriptionPurchases");
            migrationBuilder.DropIndex(name: "IX_SubscriptionPurchases_GatewayOrderId", table: "SubscriptionPurchases");
            migrationBuilder.DropIndex(name: "IX_SubscriptionPurchases_GatewayPaymentId", table: "SubscriptionPurchases");
            migrationBuilder.DropIndex(name: "IX_SubscriptionPurchases_OriginalSubscriptionId", table: "SubscriptionPurchases");
            migrationBuilder.DropIndex(name: "IX_ClinicSubscriptions_HospitalId_Status_StartDate_EndDate", table: "ClinicSubscriptions");
            migrationBuilder.DropIndex(name: "IX_ClinicSubscriptions_SupersededBySubscriptionId", table: "ClinicSubscriptions");
            migrationBuilder.DropColumn(name: "FinalPayableAmount", table: "SubscriptionPurchases");
            migrationBuilder.DropColumn(name: "GrossNewPlanAmount", table: "SubscriptionPurchases");
            migrationBuilder.DropColumn(name: "OriginalPaidAmount", table: "SubscriptionPurchases");
            migrationBuilder.DropColumn(name: "OriginalSubscriptionId", table: "SubscriptionPurchases");
            migrationBuilder.DropColumn(name: "PurchasedDailyAppointmentLimit", table: "SubscriptionPurchases");
            migrationBuilder.DropColumn(name: "PurchasedMaxBranches", table: "SubscriptionPurchases");
            migrationBuilder.DropColumn(name: "UnusedCreditAmount", table: "SubscriptionPurchases");
            migrationBuilder.DropColumn(name: "UpgradeMode", table: "SubscriptionPurchases");
            migrationBuilder.DropColumn(name: "DailyAppointmentLimit", table: "SubscriptionPlans");
            migrationBuilder.DropColumn(name: "MaxBranches", table: "SubscriptionPlans");
            migrationBuilder.DropColumn(name: "PurchasedDailyAppointmentLimit", table: "ClinicSubscriptions");
            migrationBuilder.DropColumn(name: "PurchasedMaxBranches", table: "ClinicSubscriptions");
            migrationBuilder.DropColumn(name: "SupersededAt", table: "ClinicSubscriptions");
            migrationBuilder.DropColumn(name: "SupersededBySubscriptionId", table: "ClinicSubscriptions");
        }
    }
}
