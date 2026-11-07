using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wedding.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommissionNegotiationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommissionNegotiationRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ServiceItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CurrentRate = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    ProposedRate = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    CounterRate = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    ApprovedRate = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    ReasonCategory = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ReasonDetail = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CooperationPlan = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedModeratorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResultingPolicyRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionNegotiationRequests", x => x.Id);
                    table.CheckConstraint("CK_CommissionNegotiationRequests_TargetScope", "([Scope] = 'Vendor' AND [CategoryId] IS NULL AND [ServiceItemId] IS NULL)\nOR ([Scope] = 'VendorCategory' AND [CategoryId] IS NOT NULL AND [ServiceItemId] IS NULL)\nOR ([Scope] = 'Service' AND [ServiceItemId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_CommissionNegotiationRequests_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionNegotiationRequests_CommissionPolicyRules_ResultingPolicyRuleId",
                        column: x => x.ResultingPolicyRuleId,
                        principalTable: "CommissionPolicyRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionNegotiationRequests_ServiceItems_ServiceItemId",
                        column: x => x.ServiceItemId,
                        principalTable: "ServiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionNegotiationRequests_Users_AssignedModeratorId",
                        column: x => x.AssignedModeratorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionNegotiationRequests_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionNegotiationRequests_Users_DecidedById",
                        column: x => x.DecidedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionNegotiationRequests_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommissionNegotiationMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NegotiationRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderRole = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ProposedRateSnapshot = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    AttachmentUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionNegotiationMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommissionNegotiationMessages_CommissionNegotiationRequests_NegotiationRequestId",
                        column: x => x.NegotiationRequestId,
                        principalTable: "CommissionNegotiationRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommissionNegotiationMessages_Users_SenderId",
                        column: x => x.SenderId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommissionNegotiationMessages_CreatedAt",
                table: "CommissionNegotiationMessages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionNegotiationMessages_NegotiationRequestId",
                table: "CommissionNegotiationMessages",
                column: "NegotiationRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionNegotiationMessages_SenderId",
                table: "CommissionNegotiationMessages",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionNegotiationRequests_AssignedModeratorId",
                table: "CommissionNegotiationRequests",
                column: "AssignedModeratorId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionNegotiationRequests_CategoryId",
                table: "CommissionNegotiationRequests",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionNegotiationRequests_CreatedById",
                table: "CommissionNegotiationRequests",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionNegotiationRequests_DecidedById",
                table: "CommissionNegotiationRequests",
                column: "DecidedById");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionNegotiationRequests_ResultingPolicyRuleId",
                table: "CommissionNegotiationRequests",
                column: "ResultingPolicyRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionNegotiationRequests_Scope_CategoryId_ServiceItemId",
                table: "CommissionNegotiationRequests",
                columns: new[] { "Scope", "CategoryId", "ServiceItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommissionNegotiationRequests_ServiceItemId",
                table: "CommissionNegotiationRequests",
                column: "ServiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionNegotiationRequests_Status",
                table: "CommissionNegotiationRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionNegotiationRequests_VendorId",
                table: "CommissionNegotiationRequests",
                column: "VendorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [CommissionNegotiationRequests])
                    OR EXISTS (SELECT 1 FROM [CommissionNegotiationMessages])
                    THROW 51000, 'Negotiation requests and message history must be retained; populated foundation cannot be rolled back.', 1;
                """);

            migrationBuilder.DropTable(
                name: "CommissionNegotiationMessages");

            migrationBuilder.DropTable(
                name: "CommissionNegotiationRequests");
        }
    }
}
