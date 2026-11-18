using AuthDemo.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261110120000_AddAdminSelfRegistrationVerification")]
public partial class AddAdminSelfRegistrationVerification : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "EmailVerified", table: "Users", type: "bit", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<string>(name: "Purpose", table: "OtpVerifications", type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "PasswordReset");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "EmailVerified", table: "Users");
        migrationBuilder.DropColumn(name: "Purpose", table: "OtpVerifications");
    }
}
