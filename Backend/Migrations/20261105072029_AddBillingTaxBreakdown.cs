using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingTaxBreakdown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CgstAmount",
                table: "Billings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CgstPercentage",
                table: "Billings",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Billings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "IgstAmount",
                table: "Billings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "IgstPercentage",
                table: "Billings",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SgstAmount",
                table: "Billings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SgstPercentage",
                table: "Billings",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TaxType",
                table: "Billings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<decimal>(
                name: "TaxableAmount",
                table: "Billings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CgstAmount",
                table: "Billings");

            migrationBuilder.DropColumn(
                name: "CgstPercentage",
                table: "Billings");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Billings");

            migrationBuilder.DropColumn(
                name: "IgstAmount",
                table: "Billings");

            migrationBuilder.DropColumn(
                name: "IgstPercentage",
                table: "Billings");

            migrationBuilder.DropColumn(
                name: "SgstAmount",
                table: "Billings");

            migrationBuilder.DropColumn(
                name: "SgstPercentage",
                table: "Billings");

            migrationBuilder.DropColumn(
                name: "TaxType",
                table: "Billings");

            migrationBuilder.DropColumn(
                name: "TaxableAmount",
                table: "Billings");
        }
    }
}
