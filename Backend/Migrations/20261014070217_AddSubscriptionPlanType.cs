
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations
{
    public partial class AddSubscriptionPlanType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Remove the old four-column unique index.
            migrationBuilder.DropIndex(
                name: "IX_SubscriptionPlans_IncludesCms_IncludesLab_IncludesPharmacy_DurationMonths",
                table: "SubscriptionPlans");

            // 2. Add PlanType. Existing records initially default to Basic.
            migrationBuilder.AddColumn<string>(
                name: "PlanType",
                table: "SubscriptionPlans",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Basic");

            // 3. Preserve the correct classification of existing plans.
            // Run before creating the new unique index.
            migrationBuilder.Sql(@"
                UPDATE dbo.SubscriptionPlans
                SET PlanType =
                    CASE
                        WHEN LTRIM(RTRIM(Name)) LIKE 'Premium%' THEN 'Premium'
                        WHEN LTRIM(RTRIM(Name)) LIKE 'Super%' THEN 'Super'
                        ELSE 'Basic'
                    END;
            ");

            // 4. Create the new five-column unique index.
            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_PlanType_IncludesCms_IncludesLab_IncludesPharmacy_DurationMonths",
                table: "SubscriptionPlans",
                columns: new[]
                {
                    "PlanType",
                    "IncludesCms",
                    "IncludesLab",
                    "IncludesPharmacy",
                    "DurationMonths"
                },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove the five-column index.
            migrationBuilder.DropIndex(
                name: "IX_SubscriptionPlans_PlanType_IncludesCms_IncludesLab_IncludesPharmacy_DurationMonths",
                table: "SubscriptionPlans");

            // Remove PlanType.
            migrationBuilder.DropColumn(
                name: "PlanType",
                table: "SubscriptionPlans");

            // Restore the original four-column unique index.
            // Rollback will fail if different plan types now share
            // the same module combination and duration.
            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_IncludesCms_IncludesLab_IncludesPharmacy_DurationMonths",
                table: "SubscriptionPlans",
                columns: new[]
                {
                    "IncludesCms",
                    "IncludesLab",
                    "IncludesPharmacy",
                    "DurationMonths"
                },
                unique: true);
        }
    }
}
