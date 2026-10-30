using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wedding.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChangePasswordSecurityVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SecurityVersion",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecurityVersion",
                table: "Users");
        }
    }
}
