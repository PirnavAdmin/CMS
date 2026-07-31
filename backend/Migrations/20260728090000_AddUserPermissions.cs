using AuthDemo.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260728090000_AddUserPermissions")]
public partial class AddUserPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "UserPermissions",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                UserId = table.Column<int>(type: "int", nullable: false),
                HospitalId = table.Column<int>(type: "int", nullable: false),
                Module = table.Column<string>(type: "nvarchar(450)", nullable: false),
                CanView = table.Column<bool>(type: "bit", nullable: false),
                CanCreate = table.Column<bool>(type: "bit", nullable: false),
                CanEdit = table.Column<bool>(type: "bit", nullable: false),
                CanDelete = table.Column<bool>(type: "bit", nullable: false),
                AssignedByUserId = table.Column<int>(type: "int", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserPermissions", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserPermissions_Hospitals_HospitalId",
                    column: x => x.HospitalId,
                    principalTable: "Hospitals",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_UserPermissions_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_UserPermissions_HospitalId",
            table: "UserPermissions",
            column: "HospitalId");

        migrationBuilder.CreateIndex(
            name: "IX_UserPermissions_UserId_Module",
            table: "UserPermissions",
            columns: new[] { "UserId", "Module" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "UserPermissions");
    }
}
