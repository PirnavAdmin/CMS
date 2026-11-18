using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingInvoiceNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.RenameTable(
                name: "LabTestMasters",
                newName: "LabTestMasters",
                newSchema: "dbo");

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                table: "Billings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Billings_InvoiceNumber",
                table: "Billings",
                column: "InvoiceNumber",
                unique: true,
                filter: "[InvoiceNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Billings_InvoiceNumber",
                table: "Billings");

            migrationBuilder.DropColumn(
                name: "InvoiceNumber",
                table: "Billings");

            migrationBuilder.RenameTable(
                name: "LabTestMasters",
                schema: "dbo",
                newName: "LabTestMasters");
        }
    }
}
