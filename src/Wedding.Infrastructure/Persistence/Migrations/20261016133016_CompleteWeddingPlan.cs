using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Wedding.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteWeddingPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM Reviews GROUP BY CustomerId, ListingId HAVING COUNT(*) > 1) THROW 51001, 'Duplicate historic reviews need manual review before migration. No records were deleted.', 1;");
            // EF idempotent scripts wrap this operation in IF NOT EXISTS. Trigger DDL
            // must start its own SQL batch, so execute it dynamically inside that guard.
            migrationBuilder.Sql("EXEC(N'CREATE OR ALTER TRIGGER [dbo].[TR_AuditLogs_AppendOnly] ON [dbo].[AuditLogs] INSTEAD OF UPDATE, DELETE AS BEGIN SET NOCOUNT ON; THROW 51002, ''Audit logs are append-only; retain at least 24 months.'', 1; END;');");
            migrationBuilder.DropIndex(
                name: "IX_Reviews_CustomerId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_GuestRSVPs_InvitationId",
                table: "GuestRSVPs");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_CustomerId",
                table: "Appointments");

            migrationBuilder.AddColumn<DateTime>(
                name: "EngagementDate",
                table: "WeddingProfiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventsJson",
                table: "WeddingProfiles",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "PlanningRole",
                table: "WeddingProfiles",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Couple");

            migrationBuilder.AddColumn<string>(
                name: "PrioritiesJson",
                table: "WeddingProfiles",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ContentJson",
                table: "WeddingInvitations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "Lang",
                table: "WeddingInvitations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "vi");

            migrationBuilder.AddColumn<Guid>(
                name: "TemplateId",
                table: "WeddingInvitations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowInCombos",
                table: "Vendors",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTopRated",
                table: "Vendors",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxComboDiscount",
                table: "Vendors",
                type: "decimal(8,5)",
                precision: 8,
                scale: 5,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "GoogleSubject",
                table: "Users",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PhoneVerifiedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModerationFlag",
                table: "Reviews",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PunctualityRating",
                table: "Reviews",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QualityRating",
                table: "Reviews",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Reviews",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServiceRating",
                table: "Reviews",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValueRating",
                table: "Reviews",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Subtotal",
                table: "Quotes",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatAmount",
                table: "Quotes",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatRate",
                table: "Quotes",
                type: "decimal(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "OtpCodes",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AddColumn<string>(
                name: "CancellationPolicy",
                table: "Listings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ComboOrderId",
                table: "Leads",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Leads",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Wishes",
                table: "GuestRSVPs",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DietaryPreference",
                table: "GuestRSVPs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmissionKey",
                table: "GuestRSVPs",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "GuestRSVPs",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "CreditAmount",
                table: "CommissionSettlements",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DisputeReason",
                table: "CommissionSettlements",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "CommissionSettlements",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "Commissions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReleaseAt",
                table: "Commissions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SettlementId",
                table: "Commissions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "Commissions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "ModerationStatus",
                table: "ComboTemplates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerVendorId",
                table: "ComboTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "ComboTemplates",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SingleVendorDiscount",
                table: "ComboTemplates",
                type: "decimal(8,5)",
                precision: 8,
                scale: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AllowedItemIdsJson",
                table: "ComboTemplateItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ServiceItemId",
                table: "ComboTemplateItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PriceLockUntil",
                table: "ComboOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "ComboOrders",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "BudgetItemId",
                table: "ComboOrderItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationPolicySnapshot",
                table: "ComboOrderItems",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "ComboOrderItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SelectionJson",
                table: "ComboOrderItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ServiceItemId",
                table: "ComboOrderItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "ComboOrderItems",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ContractId",
                table: "ChecklistTasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Owner",
                table: "ChecklistTasks",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Shared");

            migrationBuilder.AddColumn<Guid>(
                name: "WeddingProfileId",
                table: "ChecklistTasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContractId",
                table: "BudgetItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Owner",
                table: "BudgetItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Shared");

            migrationBuilder.AddColumn<Guid>(
                name: "WeddingProfileId",
                table: "BudgetItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AppointmentDepositCredit",
                table: "BookingContracts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CancellationPolicySnapshot",
                table: "BookingContracts",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RejectReason",
                table: "BookingContracts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RevisionCount",
                table: "BookingContracts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "BookingContracts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<TimeSpan>(
                name: "BreakEnd",
                table: "AppointmentTypes",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "BreakStart",
                table: "AppointmentTypes",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BufferAfterMinutes",
                table: "AppointmentTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BufferBeforeMinutes",
                table: "AppointmentTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FreeCancellationHours",
                table: "AppointmentTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "AppointmentTypes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxAttendees",
                table: "AppointmentTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxBookingDays",
                table: "AppointmentTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MeetingMode",
                table: "AppointmentTypes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "MinBookingHours",
                table: "AppointmentTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                table: "Appointments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "CustomerReminder24At",
                table: "Appointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CustomerReminder2At",
                table: "Appointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DepositConfirmedAt",
                table: "Appointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DepositReportedAt",
                table: "Appointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProposedById",
                table: "Appointments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProposedStartAt",
                table: "Appointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundDue",
                table: "Appointments",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "RefundReference",
                table: "Appointments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundedAt",
                table: "Appointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RescheduleCount",
                table: "Appointments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResponseDueAt",
                table: "Appointments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "VendorReminder1At",
                table: "Appointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VendorSlaViolation",
                table: "Appointments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AppointmentAvailabilityExceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    OpenTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    CloseTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentAvailabilityExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppointmentAvailabilityExceptions_AppointmentTypes_AppointmentTypeId",
                        column: x => x.AppointmentTypeId,
                        principalTable: "AppointmentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppointmentEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppointmentEvents_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArticleListingPins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleListingPins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleListingPins_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArticleListingPins_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BundleDiscountRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MinItems = table.Column<int>(type: "int", nullable: false),
                    Percent = table.Column<decimal>(type: "decimal(8,5)", precision: 8, scale: 5, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BundleDiscountRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommissionCreditNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AppliedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionCreditNotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EInvoiceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SettlementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    LookupUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VatRate = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    RecordedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EInvoiceRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmergencyMatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReplacementListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewLeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyMatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InvitationTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PrimaryColor = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    BackgroundColor = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Motif = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsPremium = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvitationTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DeduplicationKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    DeliveryError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Gateway = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UnmatchedReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SettlementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PremiumOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatchedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatchNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PremiumOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvitationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentReference = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PremiumOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SeatTables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WeddingProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeatTables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeatTables_WeddingProfiles_WeddingProfileId",
                        column: x => x.WeddingProfileId,
                        principalTable: "WeddingProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MinGuests = table.Column<int>(type: "int", nullable: false),
                    MaxGuests = table.Column<int>(type: "int", nullable: false),
                    Regions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CancellationPolicy = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ModerationStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReviewNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    MaxBookingsPerDay = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceItems_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceItems_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VendorTaxProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegalName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    TaxNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BankCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BankAccountNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BankAccountName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AgreementAccepted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorTaxProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorTaxProfiles_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeddingJoinInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WeddingProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false),
                    JoinedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingJoinInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingJoinInvitations_WeddingProfiles_WeddingProfileId",
                        column: x => x.WeddingProfileId,
                        principalTable: "WeddingProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeddingMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WeddingProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingMembers_WeddingProfiles_WeddingProfileId",
                        column: x => x.WeddingProfileId,
                        principalTable: "WeddingProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WeddingGuests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WeddingProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Group = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Side = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PartySize = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Dietary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TableId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RsvpId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingGuests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeddingGuests_SeatTables_TableId",
                        column: x => x.TableId,
                        principalTable: "SeatTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeddingGuests_WeddingProfiles_WeddingProfileId",
                        column: x => x.WeddingProfileId,
                        principalTable: "WeddingProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItemAddOns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    PerUnit = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemAddOns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemAddOns_ServiceItems_ServiceItemId",
                        column: x => x.ServiceItemId,
                        principalTable: "ServiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemVariants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PriceDelta = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemVariants_ServiceItems_ServiceItemId",
                        column: x => x.ServiceItemId,
                        principalTable: "ServiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "InvitationTemplates",
                columns: new[] { "Id", "BackgroundColor", "Category", "CreatedAt", "IsPremium", "Key", "Motif", "Name", "PrimaryColor", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("80000000-0000-0000-0000-000000000001"), "#f3f5ec", "Nature", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, "rustic", "❧", "Lá xanh ngày vui", "#456451", null },
                    { new Guid("80000000-0000-0000-0000-000000000002"), "#faf8f0", "Minimal", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, "minimalist", "♡", "Lời hẹn tinh khôi", "#25334c", null },
                    { new Guid("80000000-0000-0000-0000-000000000003"), "#faf6e9", "Luxury", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, "luxury", "✦", "Ánh vàng thanh lịch", "#745e27", null },
                    { new Guid("80000000-0000-0000-0000-000000000004"), "#f2efe4", "Classic", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, "navy-cream", "∞", "Wedding Match", "#25334c", null },
                    { new Guid("80000000-0000-0000-0000-000000000005"), "#fbf0f1", "Romantic", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, "rose-letter", "❀", "Thư tình hồng", "#955f6b", null },
                    { new Guid("80000000-0000-0000-0000-000000000006"), "#f3f5ed", "Nature", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, "sage-garden", "❦", "Khu vườn bình yên", "#52674f", null },
                    { new Guid("80000000-0000-0000-0000-000000000007"), "#faf1e8", "Boho", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, "terracotta", "☼", "Nắng ấm", "#945a42", null },
                    { new Guid("80000000-0000-0000-0000-000000000008"), "#202d44", "Luxury", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, "midnight", "✧", "Dạ tiệc sao", "#e0c468", null },
                    { new Guid("80000000-0000-0000-0000-000000000009"), "#f6f0fa", "Romantic", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, "lavender", "❁", "Hẹn ước lavender", "#6d5a8d", null },
                    { new Guid("80000000-0000-0000-0000-000000000010"), "#edf5f4", "Nature", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, "ocean", "≈", "Lời hẹn bên biển", "#376477", null },
                    { new Guid("80000000-0000-0000-0000-000000000011"), "#fff4e8", "Traditional", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, "tradition", "囍", "Hỷ sự", "#9b3b36", null },
                    { new Guid("80000000-0000-0000-0000-000000000012"), "#faf9f6", "Minimal", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, "black-tie", "&", "Black Tie", "#242424", null },
                    { new Guid("80000000-0000-0000-0000-000000000013"), "#f7f5e9", "Nature", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, "olive", "❧", "Cành olive", "#646c3f", null },
                    { new Guid("80000000-0000-0000-0000-000000000014"), "#fff7f3", "Romantic", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, "blossom", "✿", "Mùa hoa nở", "#a85b68", null },
                    { new Guid("80000000-0000-0000-0000-000000000015"), "#f4ead7", "Vintage", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, "vintage", "❦", "Chuyện đôi mình", "#7a6144", null },
                    { new Guid("80000000-0000-0000-0000-000000000016"), "#f2f1eb", "Modern", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, "modern-arch", "⌒", "Vòm hạnh phúc", "#475471", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_GoogleSubject",
                table: "Users",
                column: "GoogleSubject",
                unique: true,
                filter: "[GoogleSubject] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_CustomerId_ListingId",
                table: "Reviews",
                columns: new[] { "CustomerId", "ListingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuestRSVPs_InvitationId_SubmissionKey",
                table: "GuestRSVPs",
                columns: new[] { "InvitationId", "SubmissionKey" },
                unique: true,
                filter: "[SubmissionKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionSettlements_PaymentReference",
                table: "CommissionSettlements",
                column: "PaymentReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistTasks_ContractId",
                table: "ChecklistTasks",
                column: "ContractId",
                unique: true,
                filter: "[ContractId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetItems_ContractId",
                table: "BudgetItems",
                column: "ContractId",
                unique: true,
                filter: "[ContractId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CustomerId_Status",
                table: "Appointments",
                columns: new[] { "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentAvailabilityExceptions_AppointmentTypeId_Date",
                table: "AppointmentAvailabilityExceptions",
                columns: new[] { "AppointmentTypeId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentEvents_AppointmentId_CreatedAt",
                table: "AppointmentEvents",
                columns: new[] { "AppointmentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleListingPins_ArticleId_ListingId",
                table: "ArticleListingPins",
                columns: new[] { "ArticleId", "ListingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArticleListingPins_ListingId",
                table: "ArticleListingPins",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_BundleDiscountRules_MinItems",
                table: "BundleDiscountRules",
                column: "MinItems",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommissionCreditNotes_ContractId",
                table: "CommissionCreditNotes",
                column: "ContractId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EInvoiceRecords_SettlementId",
                table: "EInvoiceRecords",
                column: "SettlementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyMatches_ContractId",
                table: "EmergencyMatches",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_InvitationTemplates_Key",
                table: "InvitationTemplates",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemAddOns_ServiceItemId",
                table: "ItemAddOns",
                column: "ServiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemVariants_ServiceItemId",
                table: "ItemVariants",
                column: "ServiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Channel_SentAt_NextAttemptAt",
                table: "Notifications",
                columns: new[] { "Channel", "SentAt", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_DeduplicationKey",
                table: "Notifications",
                columns: new[] { "UserId", "DeduplicationKey" },
                unique: true,
                filter: "[DeduplicationKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_ReadAt",
                table: "Notifications",
                columns: new[] { "UserId", "ReadAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_TransactionId",
                table: "PaymentTransactions",
                column: "TransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PremiumOrders_CustomerId_Status",
                table: "PremiumOrders",
                columns: new[] { "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PremiumOrders_PaymentReference",
                table: "PremiumOrders",
                column: "PaymentReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeatTables_WeddingProfileId",
                table: "SeatTables",
                column: "WeddingProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceItems_ListingId",
                table: "ServiceItems",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceItems_VendorId_ModerationStatus",
                table: "ServiceItems",
                columns: new[] { "VendorId", "ModerationStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_VendorTaxProfiles_VendorId",
                table: "VendorTaxProfiles",
                column: "VendorId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingGuests_TableId",
                table: "WeddingGuests",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingGuests_WeddingProfileId_Name",
                table: "WeddingGuests",
                columns: new[] { "WeddingProfileId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_WeddingJoinInvitations_Code",
                table: "WeddingJoinInvitations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingJoinInvitations_WeddingProfileId",
                table: "WeddingJoinInvitations",
                column: "WeddingProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingMembers_UserId",
                table: "WeddingMembers",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeddingMembers_WeddingProfileId",
                table: "WeddingMembers",
                column: "WeddingProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_AuditLogs_AppendOnly];");
            migrationBuilder.DropTable(
                name: "AppointmentAvailabilityExceptions");

            migrationBuilder.DropTable(
                name: "AppointmentEvents");

            migrationBuilder.DropTable(
                name: "ArticleListingPins");

            migrationBuilder.DropTable(
                name: "BundleDiscountRules");

            migrationBuilder.DropTable(
                name: "CommissionCreditNotes");

            migrationBuilder.DropTable(
                name: "EInvoiceRecords");

            migrationBuilder.DropTable(
                name: "EmergencyMatches");

            migrationBuilder.DropTable(
                name: "InvitationTemplates");

            migrationBuilder.DropTable(
                name: "ItemAddOns");

            migrationBuilder.DropTable(
                name: "ItemVariants");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "PaymentTransactions");

            migrationBuilder.DropTable(
                name: "PremiumOrders");

            migrationBuilder.DropTable(
                name: "VendorTaxProfiles");

            migrationBuilder.DropTable(
                name: "WeddingGuests");

            migrationBuilder.DropTable(
                name: "WeddingJoinInvitations");

            migrationBuilder.DropTable(
                name: "WeddingMembers");

            migrationBuilder.DropTable(
                name: "ServiceItems");

            migrationBuilder.DropTable(
                name: "SeatTables");

            migrationBuilder.DropIndex(
                name: "IX_Users_GoogleSubject",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_CustomerId_ListingId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_GuestRSVPs_InvitationId_SubmissionKey",
                table: "GuestRSVPs");

            migrationBuilder.DropIndex(
                name: "IX_CommissionSettlements_PaymentReference",
                table: "CommissionSettlements");

            migrationBuilder.DropIndex(
                name: "IX_ChecklistTasks_ContractId",
                table: "ChecklistTasks");

            migrationBuilder.DropIndex(
                name: "IX_BudgetItems_ContractId",
                table: "BudgetItems");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_CustomerId_Status",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "EngagementDate",
                table: "WeddingProfiles");

            migrationBuilder.DropColumn(
                name: "EventsJson",
                table: "WeddingProfiles");

            migrationBuilder.DropColumn(
                name: "PlanningRole",
                table: "WeddingProfiles");

            migrationBuilder.DropColumn(
                name: "PrioritiesJson",
                table: "WeddingProfiles");

            migrationBuilder.DropColumn(
                name: "ContentJson",
                table: "WeddingInvitations");

            migrationBuilder.DropColumn(
                name: "Lang",
                table: "WeddingInvitations");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "WeddingInvitations");

            migrationBuilder.DropColumn(
                name: "AllowInCombos",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "IsTopRated",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "MaxComboDiscount",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "GoogleSubject",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PhoneVerifiedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ModerationFlag",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "PunctualityRating",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "QualityRating",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ServiceRating",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ValueRating",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "Subtotal",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "VatAmount",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "VatRate",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "CancellationPolicy",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "ComboOrderId",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "SubmissionKey",
                table: "GuestRSVPs");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "GuestRSVPs");

            migrationBuilder.DropColumn(
                name: "CreditAmount",
                table: "CommissionSettlements");

            migrationBuilder.DropColumn(
                name: "DisputeReason",
                table: "CommissionSettlements");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "CommissionSettlements");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "Commissions");

            migrationBuilder.DropColumn(
                name: "ReleaseAt",
                table: "Commissions");

            migrationBuilder.DropColumn(
                name: "SettlementId",
                table: "Commissions");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Commissions");

            migrationBuilder.DropColumn(
                name: "ModerationStatus",
                table: "ComboTemplates");

            migrationBuilder.DropColumn(
                name: "OwnerVendorId",
                table: "ComboTemplates");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "ComboTemplates");

            migrationBuilder.DropColumn(
                name: "SingleVendorDiscount",
                table: "ComboTemplates");

            migrationBuilder.DropColumn(
                name: "AllowedItemIdsJson",
                table: "ComboTemplateItems");

            migrationBuilder.DropColumn(
                name: "ServiceItemId",
                table: "ComboTemplateItems");

            migrationBuilder.DropColumn(
                name: "PriceLockUntil",
                table: "ComboOrders");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "ComboOrders");

            migrationBuilder.DropColumn(
                name: "BudgetItemId",
                table: "ComboOrderItems");

            migrationBuilder.DropColumn(
                name: "CancellationPolicySnapshot",
                table: "ComboOrderItems");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "ComboOrderItems");

            migrationBuilder.DropColumn(
                name: "SelectionJson",
                table: "ComboOrderItems");

            migrationBuilder.DropColumn(
                name: "ServiceItemId",
                table: "ComboOrderItems");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "ComboOrderItems");

            migrationBuilder.DropColumn(
                name: "ContractId",
                table: "ChecklistTasks");

            migrationBuilder.DropColumn(
                name: "Owner",
                table: "ChecklistTasks");

            migrationBuilder.DropColumn(
                name: "WeddingProfileId",
                table: "ChecklistTasks");

            migrationBuilder.DropColumn(
                name: "ContractId",
                table: "BudgetItems");

            migrationBuilder.DropColumn(
                name: "Owner",
                table: "BudgetItems");

            migrationBuilder.DropColumn(
                name: "WeddingProfileId",
                table: "BudgetItems");

            migrationBuilder.DropColumn(
                name: "AppointmentDepositCredit",
                table: "BookingContracts");

            migrationBuilder.DropColumn(
                name: "CancellationPolicySnapshot",
                table: "BookingContracts");

            migrationBuilder.DropColumn(
                name: "RejectReason",
                table: "BookingContracts");

            migrationBuilder.DropColumn(
                name: "RevisionCount",
                table: "BookingContracts");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "BookingContracts");

            migrationBuilder.DropColumn(
                name: "BreakEnd",
                table: "AppointmentTypes");

            migrationBuilder.DropColumn(
                name: "BreakStart",
                table: "AppointmentTypes");

            migrationBuilder.DropColumn(
                name: "BufferAfterMinutes",
                table: "AppointmentTypes");

            migrationBuilder.DropColumn(
                name: "BufferBeforeMinutes",
                table: "AppointmentTypes");

            migrationBuilder.DropColumn(
                name: "FreeCancellationHours",
                table: "AppointmentTypes");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "AppointmentTypes");

            migrationBuilder.DropColumn(
                name: "MaxAttendees",
                table: "AppointmentTypes");

            migrationBuilder.DropColumn(
                name: "MaxBookingDays",
                table: "AppointmentTypes");

            migrationBuilder.DropColumn(
                name: "MeetingMode",
                table: "AppointmentTypes");

            migrationBuilder.DropColumn(
                name: "MinBookingHours",
                table: "AppointmentTypes");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "CustomerReminder24At",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "CustomerReminder2At",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "DepositConfirmedAt",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "DepositReportedAt",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ProposedById",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ProposedStartAt",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "RefundDue",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "RefundReference",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "RefundedAt",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "RescheduleCount",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ResponseDueAt",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "VendorReminder1At",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "VendorSlaViolation",
                table: "Appointments");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "OtpCodes",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "Wishes",
                table: "GuestRSVPs",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DietaryPreference",
                table: "GuestRSVPs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_CustomerId",
                table: "Reviews",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestRSVPs_InvitationId",
                table: "GuestRSVPs",
                column: "InvitationId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CustomerId",
                table: "Appointments",
                column: "CustomerId");
        }
    }
}
