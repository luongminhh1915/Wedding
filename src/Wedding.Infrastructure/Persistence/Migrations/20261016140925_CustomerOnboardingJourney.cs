using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wedding.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerOnboardingJourney : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OnboardingCompletedAt",
                table: "WeddingProfiles",
                type: "datetime2",
                nullable: true);
            // Existing couples have already completed the previous setup flow. Never reset their plans.
            // An idempotent script can put ALTER TABLE and this operation in one
            // batch. Defer column binding until the preceding ALTER has executed.
            migrationBuilder.Sql("EXEC(N'UPDATE WeddingProfiles SET OnboardingCompletedAt = CreatedAt WHERE OnboardingCompletedAt IS NULL AND LEN(LTRIM(RTRIM(GroomName))) > 0 AND LEN(LTRIM(RTRIM(BrideName))) > 0;');");
            // The additive journey migration used CLR zero defaults for pre-existing appointment rows.
            migrationBuilder.Sql("EXEC(N'UPDATE AppointmentTypes SET MinBookingHours = CASE WHEN MinBookingHours < 12 THEN 12 ELSE MinBookingHours END, MaxBookingDays = CASE WHEN MaxBookingDays <= 0 OR MaxBookingDays > 90 THEN 90 ELSE MaxBookingDays END, MaxAttendees = CASE WHEN MaxAttendees <= 0 THEN 10 ELSE MaxAttendees END, FreeCancellationHours = CASE WHEN FreeCancellationHours < 24 THEN 24 ELSE FreeCancellationHours END;');");
            migrationBuilder.Sql("EXEC(N'UPDATE Appointments SET ResponseDueAt = DATEADD(hour, 24, CreatedAt) WHERE ResponseDueAt < ''2000-01-01'';');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OnboardingCompletedAt",
                table: "WeddingProfiles");
        }
    }
}
