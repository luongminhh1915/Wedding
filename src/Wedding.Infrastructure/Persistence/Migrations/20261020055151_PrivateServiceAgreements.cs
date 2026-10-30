using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wedding.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrivateServiceAgreements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrivateServiceAgreements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OriginalTotal = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    ItemsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 40000, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Terms = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Response = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RespondedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivateServiceAgreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrivateServiceAgreements_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PrivateServiceAgreements_Users_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PrivateServiceAgreements_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrivateServiceAgreements_CreatedById",
                table: "PrivateServiceAgreements",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PrivateServiceAgreements_CustomerId",
                table: "PrivateServiceAgreements",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PrivateServiceAgreements_Kind_TargetId_Sequence",
                table: "PrivateServiceAgreements",
                columns: new[] { "Kind", "TargetId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrivateServiceAgreements_VendorId",
                table: "PrivateServiceAgreements",
                column: "VendorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrivateServiceAgreements");
        }
    }
}
