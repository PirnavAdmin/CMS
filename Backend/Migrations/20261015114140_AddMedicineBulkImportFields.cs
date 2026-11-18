using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations
{
    /// <inheritdoc />
    public partial class AddMedicineBulkImportFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GST",
                table: "PharmacyStockBatches",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ManufactureDate",
                table: "PharmacyStockBatches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaximumStock",
                table: "PharmacyStockBatches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumStock",
                table: "PharmacyStockBatches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PurchasePrice",
                table: "PharmacyStockBatches",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReorderLevel",
                table: "PharmacyStockBatches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageLocation",
                table: "PharmacyStockBatches",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierCode",
                table: "PharmacyStockBatches",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierName",
                table: "PharmacyStockBatches",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BrandName",
                table: "Medicines",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Medicines",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DosageForm",
                table: "Medicines",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GenericName",
                table: "Medicines",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Manufacturer",
                table: "Medicines",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PackSize",
                table: "Medicines",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Strength",
                table: "Medicines",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GST",
                table: "PharmacyStockBatches");

            migrationBuilder.DropColumn(
                name: "ManufactureDate",
                table: "PharmacyStockBatches");

            migrationBuilder.DropColumn(
                name: "MaximumStock",
                table: "PharmacyStockBatches");

            migrationBuilder.DropColumn(
                name: "MinimumStock",
                table: "PharmacyStockBatches");

            migrationBuilder.DropColumn(
                name: "PurchasePrice",
                table: "PharmacyStockBatches");

            migrationBuilder.DropColumn(
                name: "ReorderLevel",
                table: "PharmacyStockBatches");

            migrationBuilder.DropColumn(
                name: "StorageLocation",
                table: "PharmacyStockBatches");

            migrationBuilder.DropColumn(
                name: "SupplierCode",
                table: "PharmacyStockBatches");

            migrationBuilder.DropColumn(
                name: "SupplierName",
                table: "PharmacyStockBatches");

            migrationBuilder.DropColumn(
                name: "BrandName",
                table: "Medicines");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Medicines");

            migrationBuilder.DropColumn(
                name: "DosageForm",
                table: "Medicines");

            migrationBuilder.DropColumn(
                name: "GenericName",
                table: "Medicines");

            migrationBuilder.DropColumn(
                name: "Manufacturer",
                table: "Medicines");

            migrationBuilder.DropColumn(
                name: "PackSize",
                table: "Medicines");

            migrationBuilder.DropColumn(
                name: "Strength",
                table: "Medicines");
        }
    }
}
