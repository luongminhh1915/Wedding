using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wedding.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommissionPolicyBasisAndAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommissionAdjustmentRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommissionVersionSnapshot = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalRateSnapshot = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    OriginalAmountSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OutstandingAmountSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ProposedRate = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DecidedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionAdjustmentRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommissionAdjustmentRequests_BookingContracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "BookingContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionAdjustmentRequests_Commissions_CommissionId",
                        column: x => x.CommissionId,
                        principalTable: "Commissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionAdjustmentRequests_Users_DecidedById",
                        column: x => x.DecidedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionAdjustmentRequests_Users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionAdjustmentRequests_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommissionComponentBasisApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComboTemplateRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    BasisKind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BasisDefinition = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ExcludedCostsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionComponentBasisApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommissionComponentBasisApprovals_ComboTemplateRevisions_ComboTemplateRevisionId",
                        column: x => x.ComboTemplateRevisionId,
                        principalTable: "ComboTemplateRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionComponentBasisApprovals_ServiceItems_ServiceItemId",
                        column: x => x.ServiceItemId,
                        principalTable: "ServiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionComponentBasisApprovals_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionComponentBasisApprovals_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommissionPolicyRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ServiceItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ComboTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ComboTemplateRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Rate = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    BasisKind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PolicyVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionPolicyRules", x => x.Id);
                    table.CheckConstraint("CK_CommissionPolicyRules_TargetScope", "([Scope] = 'System' AND [CategoryId] IS NULL AND [VendorId] IS NULL AND [ServiceItemId] IS NULL AND [ComboTemplateId] IS NULL AND [ComboTemplateRevisionId] IS NULL AND [Rate] IS NOT NULL AND [BasisKind] <> 'ComboComponents')\r\nOR ([Scope] = 'Category' AND [CategoryId] IS NOT NULL AND [VendorId] IS NULL AND [ServiceItemId] IS NULL AND [ComboTemplateId] IS NULL AND [ComboTemplateRevisionId] IS NULL AND [Rate] IS NOT NULL AND [BasisKind] <> 'ComboComponents')\r\nOR ([Scope] = 'Vendor' AND [CategoryId] IS NULL AND [VendorId] IS NOT NULL AND [ServiceItemId] IS NULL AND [ComboTemplateId] IS NULL AND [ComboTemplateRevisionId] IS NULL AND [Rate] IS NOT NULL AND [BasisKind] <> 'ComboComponents')\r\nOR ([Scope] = 'VendorCategory' AND [CategoryId] IS NOT NULL AND [VendorId] IS NOT NULL AND [ServiceItemId] IS NULL AND [ComboTemplateId] IS NULL AND [ComboTemplateRevisionId] IS NULL AND [Rate] IS NOT NULL AND [BasisKind] <> 'ComboComponents')\r\nOR ([Scope] = 'Service' AND [CategoryId] IS NULL AND [VendorId] IS NULL AND [ServiceItemId] IS NOT NULL AND [ComboTemplateId] IS NULL AND [ComboTemplateRevisionId] IS NULL AND [Rate] IS NOT NULL AND [BasisKind] <> 'ComboComponents')\r\nOR ([Scope] = 'Combo' AND [CategoryId] IS NULL AND [VendorId] IS NULL AND [ServiceItemId] IS NULL AND [ComboTemplateId] IS NOT NULL AND [ComboTemplateRevisionId] IS NOT NULL AND [Rate] IS NULL AND [BasisKind] = 'ComboComponents')");
                    table.ForeignKey(
                        name: "FK_CommissionPolicyRules_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionPolicyRules_ComboTemplateRevisions_ComboTemplateRevisionId",
                        column: x => x.ComboTemplateRevisionId,
                        principalTable: "ComboTemplateRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionPolicyRules_ComboTemplates_ComboTemplateId",
                        column: x => x.ComboTemplateId,
                        principalTable: "ComboTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionPolicyRules_ServiceItems_ServiceItemId",
                        column: x => x.ServiceItemId,
                        principalTable: "ServiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionPolicyRules_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionPolicyRules_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionPolicyRules_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommissionAdjustmentRequests_CommissionId",
                table: "CommissionAdjustmentRequests",
                column: "CommissionId",
                unique: true,
                filter: "[Status] = 'Submitted'");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionAdjustmentRequests_ContractId",
                table: "CommissionAdjustmentRequests",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionAdjustmentRequests_DecidedById",
                table: "CommissionAdjustmentRequests",
                column: "DecidedById");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionAdjustmentRequests_RequestedById",
                table: "CommissionAdjustmentRequests",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionAdjustmentRequests_Status",
                table: "CommissionAdjustmentRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionAdjustmentRequests_VendorId",
                table: "CommissionAdjustmentRequests",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionComponentBasisApprovals_ApprovedById",
                table: "CommissionComponentBasisApprovals",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionComponentBasisApprovals_ComboTemplateRevisionId_ServiceItemId_Sequence",
                table: "CommissionComponentBasisApprovals",
                columns: new[] { "ComboTemplateRevisionId", "ServiceItemId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommissionComponentBasisApprovals_CreatedById",
                table: "CommissionComponentBasisApprovals",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionComponentBasisApprovals_ServiceItemId",
                table: "CommissionComponentBasisApprovals",
                column: "ServiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionComponentBasisApprovals_Status",
                table: "CommissionComponentBasisApprovals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionPolicyRules_ApprovedById",
                table: "CommissionPolicyRules",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionPolicyRules_CategoryId",
                table: "CommissionPolicyRules",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionPolicyRules_ComboTemplateId",
                table: "CommissionPolicyRules",
                column: "ComboTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionPolicyRules_ComboTemplateRevisionId",
                table: "CommissionPolicyRules",
                column: "ComboTemplateRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionPolicyRules_CreatedById",
                table: "CommissionPolicyRules",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionPolicyRules_EffectiveFrom_EffectiveTo",
                table: "CommissionPolicyRules",
                columns: new[] { "EffectiveFrom", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_CommissionPolicyRules_Scope_CategoryId_VendorId_ServiceItemId_ComboTemplateId_ComboTemplateRevisionId",
                table: "CommissionPolicyRules",
                columns: new[] { "Scope", "CategoryId", "VendorId", "ServiceItemId", "ComboTemplateId", "ComboTemplateRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CommissionPolicyRules_ServiceItemId",
                table: "CommissionPolicyRules",
                column: "ServiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionPolicyRules_Status",
                table: "CommissionPolicyRules",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionPolicyRules_VendorId",
                table: "CommissionPolicyRules",
                column: "VendorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [CommissionAdjustmentRequests])
                   OR EXISTS (SELECT 1 FROM [CommissionComponentBasisApprovals])
                   OR EXISTS (SELECT 1 FROM [CommissionPolicyRules])
                    THROW 51000, 'Commission workflow history exists; preserve it and create a forward migration instead of rolling back.', 1;
                """);

            migrationBuilder.DropTable(
                name: "CommissionAdjustmentRequests");

            migrationBuilder.DropTable(
                name: "CommissionComponentBasisApprovals");

            migrationBuilder.DropTable(
                name: "CommissionPolicyRules");
        }
    }
}
