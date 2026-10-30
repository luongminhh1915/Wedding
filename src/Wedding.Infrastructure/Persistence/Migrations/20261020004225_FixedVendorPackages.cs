using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wedding.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixedVendorPackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PricingServiceItemId",
                table: "Listings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ComboPrice",
                table: "ComboTemplates",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalPrice",
                table: "ComboTemplates",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PricingMode",
                table: "ComboTemplates",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Dynamic");

            migrationBuilder.AddColumn<string>(
                name: "ComponentSnapshotJson",
                table: "ComboTemplateItems",
                type: "nvarchar(max)",
                maxLength: 100000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComponentSnapshotJson",
                table: "ComboOrderItems",
                type: "nvarchar(max)",
                maxLength: 100000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PricingServiceItemId",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "ComboPrice",
                table: "ComboTemplates");

            migrationBuilder.DropColumn(
                name: "OriginalPrice",
                table: "ComboTemplates");

            migrationBuilder.DropColumn(
                name: "PricingMode",
                table: "ComboTemplates");

            migrationBuilder.DropColumn(
                name: "ComponentSnapshotJson",
                table: "ComboTemplateItems");

            migrationBuilder.DropColumn(
                name: "ComponentSnapshotJson",
                table: "ComboOrderItems");
        }
    }
}
