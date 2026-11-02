using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wedding.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminVendorContactAndOperatingAreas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_SocialEmail",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "OperatingRegions",
                table: "Vendors",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoginName",
                table: "Users",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_LoginName",
                table: "Users",
                column: "LoginName",
                unique: true,
                filter: "[LoginName] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_SocialEmail",
                table: "Users",
                sql: "[Email] IS NOT NULL OR ([Role] = 'Customer' AND ([GoogleSubject] IS NOT NULL OR [FacebookSubject] IS NOT NULL)) OR ([Role] = 'VendorOwner' AND [LoginName] IS NOT NULL AND [LoginName] <> '')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [Users] WHERE [LoginName] IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM [Vendors] WHERE [OperatingRegions] IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM [Users] WHERE [Email] IS NULL AND [Role] = 'VendorOwner')
                    THROW 51000, 'Cannot roll back while vendor login names, operating areas or vendors without contact email exist.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Users_LoginName",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_SocialEmail",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "OperatingRegions",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "LoginName",
                table: "Users");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_SocialEmail",
                table: "Users",
                sql: "[Email] IS NOT NULL OR ([Role] = 'Customer' AND ([GoogleSubject] IS NOT NULL OR [FacebookSubject] IS NOT NULL))");
        }
    }
}
