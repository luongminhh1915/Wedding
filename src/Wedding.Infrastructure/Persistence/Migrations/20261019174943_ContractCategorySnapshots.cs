using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wedding.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ContractCategorySnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CommissionBusinessGroupIdSnapshot",
                table: "BookingContracts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommissionBusinessGroupNameSnapshot",
                table: "BookingContracts",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CommissionCategoryIdSnapshot",
                table: "BookingContracts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommissionCategoryNameSnapshot",
                table: "BookingContracts",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommissionPolicyVersionSnapshot",
                table: "BookingContracts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommissionRateSourceSnapshot",
                table: "BookingContracts",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommissionBusinessGroupIdSnapshot",
                table: "BookingContracts");

            migrationBuilder.DropColumn(
                name: "CommissionBusinessGroupNameSnapshot",
                table: "BookingContracts");

            migrationBuilder.DropColumn(
                name: "CommissionCategoryIdSnapshot",
                table: "BookingContracts");

            migrationBuilder.DropColumn(
                name: "CommissionCategoryNameSnapshot",
                table: "BookingContracts");

            migrationBuilder.DropColumn(
                name: "CommissionPolicyVersionSnapshot",
                table: "BookingContracts");

            migrationBuilder.DropColumn(
                name: "CommissionRateSourceSnapshot",
                table: "BookingContracts");
        }
    }
}
