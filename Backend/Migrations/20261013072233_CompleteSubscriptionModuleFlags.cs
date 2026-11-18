
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations
{
    public partial class CompleteSubscriptionModuleFlags : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove existing non-unique subscription plan index
            migrationBuilder.DropIndex(
                name: "IX_SubscriptionPlans_IncludesCms_IncludesLab_IncludesPharmacy_DurationMonths",
                table: "SubscriptionPlans");

            // CMS is included in all four subscription combinations.
            // Existing subscription requests will receive true.
            migrationBuilder.AddColumn<bool>(
                name: "IncludesCms",
                table: "SubscriptionRequests",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // Pharmacy is optional.
            // Existing requests default to false.
            migrationBuilder.AddColumn<bool>(
                name: "IncludesPharmacy",
                table: "SubscriptionRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Prevent duplicate combinations for the same duration.
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

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove unique index
            migrationBuilder.DropIndex(
                name: "IX_SubscriptionPlans_IncludesCms_IncludesLab_IncludesPharmacy_DurationMonths",
                table: "SubscriptionPlans");

            // Remove newly added fields
            migrationBuilder.DropColumn(
                name: "IncludesCms",
                table: "SubscriptionRequests");

            migrationBuilder.DropColumn(
                name: "IncludesPharmacy",
                table: "SubscriptionRequests");

            // Restore original non-unique index
            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_IncludesCms_IncludesLab_IncludesPharmacy_DurationMonths",
                table: "SubscriptionPlans",
                columns: new[]
                {
                    "IncludesCms",
                    "IncludesLab",
                    "IncludesPharmacy",
                    "DurationMonths"
                });
        }
    }
}
