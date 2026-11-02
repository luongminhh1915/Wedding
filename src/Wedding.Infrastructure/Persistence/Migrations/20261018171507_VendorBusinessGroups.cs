using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Wedding.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VendorBusinessGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PartnershipPolicyAcceptedAt",
                table: "Vendors",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartnershipPolicyVersion",
                table: "Vendors",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessGroupId",
                table: "Categories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresCommissionBasisApproval",
                table: "Categories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "BusinessGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DefaultCommissionRate = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    RequiresCommissionBasisApproval = table.Column<bool>(type: "bit", nullable: false),
                    PolicyVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VendorBusinessGroups",
                columns: table => new
                {
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorBusinessGroups", x => new { x.VendorId, x.BusinessGroupId });
                    table.ForeignKey(
                        name: "FK_VendorBusinessGroups_BusinessGroups_BusinessGroupId",
                        column: x => x.BusinessGroupId,
                        principalTable: "BusinessGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorBusinessGroups_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "BusinessGroups",
                columns: new[] { "Id", "Code", "CreatedAt", "DefaultCommissionRate", "Description", "DisplayOrder", "IsActive", "Name", "PolicyVersion", "RequiresCommissionBasisApproval", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("22222222-2222-2222-2222-222222222201"), "venue-banquet", new DateTime(2026, 10, 19, 0, 0, 0, 0, DateTimeKind.Utc), 0.03m, "Địa điểm tổ chức, sảnh tiệc và dịch vụ tiệc cho lễ cưới chính.", 1, true, "Địa điểm & Tiệc cưới", "2026-10-19-v1", false, null },
                    { new Guid("22222222-2222-2222-2222-222222222202"), "decor-invitations", new DateTime(2026, 10, 19, 0, 0, 0, 0, DateTimeKind.Utc), 0.08m, "Trang trí, thiết kế concept, thiệp cưới và quà cảm ơn.", 2, true, "Trang trí & Thiệp cưới", "2026-10-19-v1", false, null },
                    { new Guid("22222222-2222-2222-2222-222222222203"), "photo-bridal-makeup", new DateTime(2026, 10, 19, 0, 0, 0, 0, DateTimeKind.Utc), 0.10m, "Quay phim, chụp ảnh, trang phục cưới và trang điểm.", 3, true, "Quay chụp, Váy cưới & Makeup", "2026-10-19-v1", false, null },
                    { new Guid("22222222-2222-2222-2222-222222222204"), "full-service-planning", new DateTime(2026, 10, 19, 0, 0, 0, 0, DateTimeKind.Utc), 0.12m, "Đơn vị chịu trách nhiệm lên kế hoạch, quản lý, phối hợp và điều hành đám cưới. Không đồng nghĩa với combo nhiều dịch vụ. Mức 12% cần cơ sở tính phí tổ chức/quản lý được phê duyệt, không mặc định tính trên ngân sách cưới hay khoản chi hộ.", 4, true, "Tổ chức cưới trọn gói (A–Z)", "2026-10-19-v1", true, null },
                    { new Guid("22222222-2222-2222-2222-222222222205"), "other-events", new DateTime(2026, 10, 19, 0, 0, 0, 0, DateTimeKind.Utc), 0.03m, "Tiệc độc thân, đính hôn, kỷ niệm ngày cưới, tuần trăng mật và sự kiện liên quan ngoài lễ cưới chính.", 5, true, "Tiệc & Sự kiện khác", "2026-10-19-v1", false, null }
                });

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111101"),
                column: "BusinessGroupId",
                value: new Guid("22222222-2222-2222-2222-222222222201"));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111102"),
                column: "BusinessGroupId",
                value: new Guid("22222222-2222-2222-2222-222222222202"));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111103"),
                column: "BusinessGroupId",
                value: new Guid("22222222-2222-2222-2222-222222222203"));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111104"),
                column: "BusinessGroupId",
                value: new Guid("22222222-2222-2222-2222-222222222203"));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111105"),
                column: "BusinessGroupId",
                value: new Guid("22222222-2222-2222-2222-222222222203"));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111106"),
                column: "BusinessGroupId",
                value: new Guid("22222222-2222-2222-2222-222222222202"));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111107"),
                columns: new[] { "BusinessGroupId", "DefaultCommissionRate", "Description", "Name", "RequiresCommissionBasisApproval" },
                values: new object[] { new Guid("22222222-2222-2222-2222-222222222204"), 0.12m, "Lên kế hoạch, quản lý và điều phối đám cưới. Hoa hồng 12% cần cơ sở phí tổ chức/quản lý được duyệt; không tính mặc định trên chi hộ hoặc toàn ngân sách cưới.", "Tổ chức cưới trọn gói (A–Z) – Full-Service Wedding Planning", true });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "BusinessGroupId", "CreatedAt", "DefaultCommissionRate", "Description", "DisplayOrder", "Icon", "IsActive", "Name", "Slug", "UpdatedAt" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111108"), new Guid("22222222-2222-2222-2222-222222222205"), new DateTime(2026, 10, 19, 0, 0, 0, 0, DateTimeKind.Utc), 0.03m, "Tiệc độc thân, đính hôn, kỷ niệm ngày cưới, tuần trăng mật và sự kiện liên quan ngoài lễ cưới chính", 8, "PartyPopper", true, "Tiệc & Sự kiện khác", "tiec-su-kien-khac", null });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_BusinessGroupId",
                table: "Categories",
                column: "BusinessGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessGroups_Code",
                table: "BusinessGroups",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorBusinessGroups_BusinessGroupId",
                table: "VendorBusinessGroups",
                column: "BusinessGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_BusinessGroups_BusinessGroupId",
                table: "Categories",
                column: "BusinessGroupId",
                principalTable: "BusinessGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_BusinessGroups_BusinessGroupId",
                table: "Categories");

            migrationBuilder.DropTable(
                name: "VendorBusinessGroups");

            migrationBuilder.DropTable(
                name: "BusinessGroups");

            migrationBuilder.DropIndex(
                name: "IX_Categories_BusinessGroupId",
                table: "Categories");

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111108"));

            migrationBuilder.DropColumn(
                name: "PartnershipPolicyAcceptedAt",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "PartnershipPolicyVersion",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "BusinessGroupId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "RequiresCommissionBasisApproval",
                table: "Categories");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111107"),
                columns: new[] { "DefaultCommissionRate", "Description", "Name" },
                values: new object[] { 0.10m, "Lên kế hoạch, điều phối trọn gói toàn bộ đám cưới", "Wedding Planner trọn gói (Planning)" });
        }
    }
}
