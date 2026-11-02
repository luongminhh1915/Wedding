using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wedding.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerOrderLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerOrderStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevisionOfId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupersededById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevisionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    WithdrawalReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerOrderStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerOrderStates_Users_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderChangeRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ProposedChanges = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RequiresFinance = table.Column<bool>(type: "bit", nullable: false),
                    VendorReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VendorReviewedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VendorResponse = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FinanceReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinanceReviewedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FinanceResponse = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderChangeRequests_Users_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderChangeRequests_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerOrderStates_CustomerId_Kind_EntityId",
                table: "CustomerOrderStates",
                columns: new[] { "CustomerId", "Kind", "EntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderChangeRequests_CustomerId_Kind_EntityId",
                table: "OrderChangeRequests",
                columns: new[] { "CustomerId", "Kind", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderChangeRequests_VendorId_Status",
                table: "OrderChangeRequests",
                columns: new[] { "VendorId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerOrderStates");

            migrationBuilder.DropTable(
                name: "OrderChangeRequests");
        }
    }
}
