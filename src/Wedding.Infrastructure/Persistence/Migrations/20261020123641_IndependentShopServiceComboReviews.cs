using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wedding.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IndependentShopServiceComboReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_BookingContractId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_CustomerId_ListingId",
                table: "Reviews");

            migrationBuilder.AlterColumn<Guid>(
                name: "ListingId",
                table: "Reviews",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "ComboOrderId",
                table: "Reviews",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ComboTemplateId",
                table: "Reviews",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetType",
                table: "Reviews",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Service");

            migrationBuilder.AddColumn<Guid>(
                name: "ComboTemplateRevisionId",
                table: "ComboOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComboTemplateSnapshotJson",
                table: "ComboOrders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReviewEditRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BeforeRating = table.Column<int>(type: "int", nullable: false),
                    BeforeContent = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    BeforePhotosJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    BeforeQualityRating = table.Column<int>(type: "int", nullable: true),
                    BeforeServiceRating = table.Column<int>(type: "int", nullable: true),
                    BeforePunctualityRating = table.Column<int>(type: "int", nullable: true),
                    BeforeValueRating = table.Column<int>(type: "int", nullable: true),
                    AfterRating = table.Column<int>(type: "int", nullable: false),
                    AfterContent = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    AfterPhotosJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    AfterQualityRating = table.Column<int>(type: "int", nullable: true),
                    AfterServiceRating = table.Column<int>(type: "int", nullable: true),
                    AfterPunctualityRating = table.Column<int>(type: "int", nullable: true),
                    AfterValueRating = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewEditRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewEditRequests_Reviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "Reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_BookingContractId_ListingId",
                table: "Reviews",
                columns: new[] { "BookingContractId", "ListingId" },
                unique: true,
                filter: "[TargetType] = 'Service' AND [BookingContractId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_BookingContractId_VendorId",
                table: "Reviews",
                columns: new[] { "BookingContractId", "VendorId" },
                unique: true,
                filter: "[TargetType] = 'Shop' AND [BookingContractId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ComboOrderId_ComboTemplateId",
                table: "Reviews",
                columns: new[] { "ComboOrderId", "ComboTemplateId" },
                unique: true,
                filter: "[TargetType] = 'Combo' AND [ComboOrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ComboOrderId_VendorId",
                table: "Reviews",
                columns: new[] { "ComboOrderId", "VendorId" },
                unique: true,
                filter: "[TargetType] = 'Shop' AND [ComboOrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ComboTemplateId",
                table: "Reviews",
                column: "ComboTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_CustomerId_ListingId",
                table: "Reviews",
                columns: new[] { "CustomerId", "ListingId" },
                unique: true,
                filter: "[TargetType] = 'Service' AND [BookingContractId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_TargetType_ComboTemplateId_Status_CreatedAt",
                table: "Reviews",
                columns: new[] { "TargetType", "ComboTemplateId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_TargetType_ListingId_Status_CreatedAt",
                table: "Reviews",
                columns: new[] { "TargetType", "ListingId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_TargetType_VendorId_Status_CreatedAt",
                table: "Reviews",
                columns: new[] { "TargetType", "VendorId", "Status", "CreatedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reviews_TargetReferences",
                table: "Reviews",
                sql: "([TargetType] = 'Service' AND [ListingId] IS NOT NULL AND [ComboTemplateId] IS NULL AND [ComboOrderId] IS NULL) OR ([TargetType] = 'Shop' AND [ListingId] IS NULL AND [ComboTemplateId] IS NULL AND (([BookingContractId] IS NOT NULL AND [ComboOrderId] IS NULL) OR ([BookingContractId] IS NULL AND [ComboOrderId] IS NOT NULL))) OR ([TargetType] = 'Combo' AND [ListingId] IS NULL AND [BookingContractId] IS NULL AND [ComboTemplateId] IS NOT NULL AND [ComboOrderId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ComboOrders_ComboTemplateRevisionId",
                table: "ComboOrders",
                column: "ComboTemplateRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewEditRequests_ReviewId_AttemptNumber",
                table: "ReviewEditRequests",
                columns: new[] { "ReviewId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewEditRequests_ReviewId_Status",
                table: "ReviewEditRequests",
                columns: new[] { "ReviewId", "Status" },
                unique: true,
                filter: "[Status] IN ('Pending', 'Approved')");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewEditRequests_Status_RequestedAt",
                table: "ReviewEditRequests",
                columns: new[] { "Status", "RequestedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ComboOrders_ComboTemplateRevisions_ComboTemplateRevisionId",
                table: "ComboOrders",
                column: "ComboTemplateRevisionId",
                principalTable: "ComboTemplateRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_ComboOrders_ComboOrderId",
                table: "Reviews",
                column: "ComboOrderId",
                principalTable: "ComboOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_ComboTemplates_ComboTemplateId",
                table: "Reviews",
                column: "ComboTemplateId",
                principalTable: "ComboTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Existing reviews map to Service, so the old Vendor cache no longer represents Shop ratings.
            migrationBuilder.Sql(
                "UPDATE [Vendors] SET [RatingAvg] = 0, [TotalReviews] = 0, [IsTopRated] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [Reviews] WHERE [TargetType] <> 'Service' OR [ListingId] IS NULL)
                    THROW 51000, 'Cannot downgrade while non-Service reviews exist.', 1;
                IF EXISTS (SELECT 1 FROM [Reviews] GROUP BY [CustomerId], [ListingId] HAVING COUNT(*) > 1)
                    THROW 51000, 'Cannot downgrade while repeated Service reviews would violate the legacy key.', 1;
                IF EXISTS (SELECT 1 FROM [ReviewEditRequests])
                    THROW 51000, 'Cannot downgrade while review edit history exists.', 1;
                IF EXISTS (SELECT 1 FROM [ComboOrders] WHERE [ComboTemplateRevisionId] IS NOT NULL OR [ComboTemplateSnapshotJson] IS NOT NULL)
                    THROW 51000, 'Cannot downgrade while ComboOrder version snapshots exist.', 1;

                ;WITH [ReviewRatings] AS
                (
                    SELECT [VendorId],
                        COUNT(*) AS [TotalReviews],
                        ROUND(
                            SUM(
                                (CASE WHEN [QualityRating] IS NULL THEN CAST([Rating] AS decimal(10, 2))
                                    ELSE (CAST([QualityRating] AS decimal(10, 2)) + [ServiceRating] +
                                        [PunctualityRating] + [ValueRating]) / 4.0 END) *
                                CASE WHEN [IsVerifiedBuyer] = 1 THEN 1.0 ELSE 0.5 END
                            ) /
                            SUM(CASE WHEN [IsVerifiedBuyer] = 1 THEN 1.0 ELSE 0.5 END),
                            1
                        ) AS [RatingAvg]
                    FROM [Reviews]
                    WHERE [TargetType] = 'Service' AND [Status] = 'Approved'
                    GROUP BY [VendorId]
                )
                UPDATE [vendor]
                SET [RatingAvg] = COALESCE([ratings].[RatingAvg], 0),
                    [TotalReviews] = COALESCE([ratings].[TotalReviews], 0),
                    [IsTopRated] = CASE WHEN COALESCE([ratings].[TotalReviews], 0) >= 50
                        AND COALESCE([ratings].[RatingAvg], 0) >= 4.8 THEN 1 ELSE 0 END
                FROM [Vendors] AS [vendor]
                LEFT JOIN [ReviewRatings] AS [ratings] ON [ratings].[VendorId] = [vendor].[Id];
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_ComboOrders_ComboTemplateRevisions_ComboTemplateRevisionId",
                table: "ComboOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_ComboOrders_ComboOrderId",
                table: "Reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_ComboTemplates_ComboTemplateId",
                table: "Reviews");

            migrationBuilder.DropTable(
                name: "ReviewEditRequests");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_BookingContractId_ListingId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_BookingContractId_VendorId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_ComboOrderId_ComboTemplateId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_ComboOrderId_VendorId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_ComboTemplateId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_CustomerId_ListingId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_TargetType_ComboTemplateId_Status_CreatedAt",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_TargetType_ListingId_Status_CreatedAt",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_TargetType_VendorId_Status_CreatedAt",
                table: "Reviews");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reviews_TargetReferences",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_ComboOrders_ComboTemplateRevisionId",
                table: "ComboOrders");

            migrationBuilder.DropColumn(
                name: "ComboOrderId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ComboTemplateId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "TargetType",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ComboTemplateRevisionId",
                table: "ComboOrders");

            migrationBuilder.DropColumn(
                name: "ComboTemplateSnapshotJson",
                table: "ComboOrders");

            migrationBuilder.AlterColumn<Guid>(
                name: "ListingId",
                table: "Reviews",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_BookingContractId",
                table: "Reviews",
                column: "BookingContractId",
                unique: true,
                filter: "[BookingContractId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_CustomerId_ListingId",
                table: "Reviews",
                columns: new[] { "CustomerId", "ListingId" },
                unique: true);
        }
    }
}
