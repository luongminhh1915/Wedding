using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wedding.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FacebookCustomerIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FacebookSubject",
                table: "Users",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_FacebookSubject",
                table: "Users",
                column: "FacebookSubject",
                unique: true,
                filter: "[FacebookSubject] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [Users] WHERE [FacebookSubject] IS NOT NULL)
                BEGIN
                    THROW 51000, 'Facebook account links exist; preserve them and use a forward migration instead of rolling back.', 1;
                END;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Users_FacebookSubject",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FacebookSubject",
                table: "Users");
        }
    }
}
