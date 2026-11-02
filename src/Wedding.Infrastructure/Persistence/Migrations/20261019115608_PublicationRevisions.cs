using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wedding.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PublicationRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Listings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentJson",
                table: "ComboTemplates",
                type: "nvarchar(max)",
                maxLength: 20000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "ComboTemplates",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidFromUtc",
                table: "ComboTemplates",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidUntilUtc",
                table: "ComboTemplates",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ComboTemplateRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComboTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 100000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByRole = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedByRole = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SupersededAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupersededFromStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComboTemplateRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComboTemplateRevisions_ComboTemplates_ComboTemplateId",
                        column: x => x.ComboTemplateId,
                        principalTable: "ComboTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ListingRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    ContentJson = table.Column<string>(type: "nvarchar(max)", maxLength: 160000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListingRevisions_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComboTemplateRevisions_ComboTemplateId_Sequence",
                table: "ComboTemplateRevisions",
                columns: new[] { "ComboTemplateId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListingRevisions_ListingId_Sequence",
                table: "ListingRevisions",
                columns: new[] { "ListingId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListingRevisions_Status_SubmittedAt",
                table: "ListingRevisions",
                columns: new[] { "Status", "SubmittedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComboTemplateRevisions");

            migrationBuilder.DropTable(
                name: "ListingRevisions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "ContentJson",
                table: "ComboTemplates");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "ComboTemplates");

            migrationBuilder.DropColumn(
                name: "ValidFromUtc",
                table: "ComboTemplates");

            migrationBuilder.DropColumn(
                name: "ValidUntilUtc",
                table: "ComboTemplates");
        }
    }
}
