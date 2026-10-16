using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Planner;
using Wedding.Application.Features.Invitations.Commands.SaveInvitation;
using Wedding.Application.Features.Invitations.Commands.SubmitRsvp;
using Wedding.Application.Features.Invitations.Queries.GetPublicInvitation;
using Wedding.Application.Features.WeddingTools.Commands.SaveBudgetItem;
using Wedding.Application.Features.WeddingTools.Queries.GetBudget;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public class PlannerTests
{
    [Fact] public async Task WizardCreates35Tasks14BudgetsAndIsIdempotent()
    {
        await using var f = await Fixture.Create();
        var handler = new SaveWeddingProfileCommandHandler(f.Db, new Current(f.Owner.Id));
        var command = new SaveWeddingProfileCommand("Minh", "Anh", DateTime.UtcNow.AddDays(100), "HCM", 300000000, 300, "Classic", null, true);
        await handler.Handle(command, default); await handler.Handle(command, default);
        Assert.Equal(35, await f.Db.ChecklistTasks.CountAsync()); Assert.Equal(14, await f.Db.BudgetItems.CountAsync());
        Assert.Equal(300000000, (await f.Db.BudgetItems.ToListAsync()).Sum(x => x.PlannedCost));
        Assert.All(await f.Db.ChecklistTasks.ToListAsync(), t => { Assert.NotNull(t.WeddingProfileId); Assert.True(t.DueDate >= DateTime.UtcNow.Date); });
    }
    [Fact] public async Task ViewerCannotWriteWhileEditorSharesOwnersBudget()
    {
        await using var f = await Fixture.Create(); var profile = await f.Profile();
        f.Db.WeddingMembers.Add(WeddingMember.Create(profile.Id, f.Partner.Id, "Viewer")); await f.Db.SaveChangesAsync();
        var current = new Current(f.Partner.Id);
        var save = new SaveBudgetItemCommandHandler(f.Db, current);
        await Assert.ThrowsAsync<DomainException>(() => save.Handle(new(null, "Venue", 1000000), default));
        var member = await f.Db.WeddingMembers.SingleAsync(); member.SetRole("Editor"); await f.Db.SaveChangesAsync();
        await save.Handle(new(null, "Venue", 1000000), default);
        var budget = await new GetBudgetQueryHandler(f.Db, new Current(f.Owner.Id)).Handle(new(), default);
        Assert.Single(budget.Items); Assert.Equal(f.Owner.Id, (await f.Db.BudgetItems.SingleAsync()).CustomerId);
    }
    [Fact] public async Task ViewerIsDeniedAtEverySharedWriteBoundary()
    {
        await using var f = await Fixture.Create(); var profile = await f.Profile();
        f.Db.WeddingMembers.Add(WeddingMember.Create(profile.Id, f.Partner.Id, "Viewer")); await f.Db.SaveChangesAsync();
        var viewer = new Current(f.Partner.Id);
        await Assert.ThrowsAsync<DomainException>(() => new SaveWeddingProfileCommandHandler(f.Db, viewer).Handle(new("Changed", "Names", DateTime.UtcNow.AddDays(10), "Hanoi", 1, 1, "Classic", null, false), default));
        await Assert.ThrowsAsync<DomainException>(() => new SaveWeddingGuestsHandler(f.Db, viewer).Handle(new([new(null, "Guest")]), default));
        await Assert.ThrowsAsync<DomainException>(() => new SaveSeatTableHandler(f.Db, viewer).Handle(new(null, "Bàn", 10), default));
        await Assert.ThrowsAsync<DomainException>(() => new UpdateWeddingBudgetHandler(f.Db, viewer).Handle(new(999), default));
        await Assert.ThrowsAsync<DomainException>(() => new SaveInvitationCommandHandler(f.Db, viewer).Handle(new("partner-write", "Minh", "Anh", DateTime.UtcNow.AddDays(10), "Venue", "Address", null, null, null, null, "navy-cream", null), default));
        Assert.Empty(await f.Db.WeddingGuests.ToListAsync()); Assert.Empty(await f.Db.WeddingInvitations.ToListAsync());
        Assert.Equal("Minh", (await f.Db.WeddingProfiles.SingleAsync()).GroomName);
    }
    [Fact] public async Task JoiningAndLeavingNeverOverwritesPrivateWedding()
    {
        await using var f = await Fixture.Create(); var shared = await f.Profile();
        var personal = WeddingProfile.Create(f.Partner.Id); personal.Update("Private", "Wedding", null, "City", 100, 1, "Classic", null); f.Db.WeddingProfiles.Add(personal);
        f.Db.WeddingMembers.Add(WeddingMember.Create(shared.Id, f.Partner.Id, "Editor")); await f.Db.SaveChangesAsync();
        var user = new Current(f.Partner.Id); var scope = await WeddingAccess.ResolveAsync(f.Db, user, default);
        Assert.Equal(shared.Id, scope.ProfileId); Assert.Equal(f.Owner.Id, scope.OwnerId);
        var membership = await f.Db.WeddingMembers.SingleAsync(); await new UpdateWeddingMemberHandler(f.Db, user).Handle(new(membership.Id, null), default);
        var restored = await WeddingAccess.ResolveAsync(f.Db, user, default); Assert.Equal(personal.Id, restored.ProfileId); Assert.Equal("Owner", restored.Role);
        Assert.Equal(2, await f.Db.WeddingProfiles.CountAsync());
    }
    [Fact] public async Task ExistingRsvpBackfillsOnceWhenWeddingProfileIsCreatedLater()
    {
        await using var f = await Fixture.Create(); var invitation = WeddingInvitation.Create(f.Owner.Id, "before-wizard", "Minh", "Anh", DateTime.UtcNow.AddDays(100), "Venue", "Address", null, null); f.Db.WeddingInvitations.Add(invitation); await f.Db.SaveChangesAsync();
        await new SubmitRsvpCommandHandler(f.Db).Handle(new("before-wizard", "Early guest", null, "Attending", 1), default);
        Assert.Empty(await f.Db.WeddingGuests.ToListAsync());
        var save = new SaveWeddingProfileCommandHandler(f.Db, new Current(f.Owner.Id)); var command = new SaveWeddingProfileCommand("Minh", "Anh", DateTime.UtcNow.AddDays(100), "HCM", 100, 10, "Classic", null, false);
        await save.Handle(command, default); await save.Handle(command, default);
        Assert.Single(await f.Db.WeddingGuests.ToListAsync()); Assert.Equal(2, (await f.Db.WeddingGuests.SingleAsync()).PartySize);
    }
    [Fact] public async Task PersistedPlanSurvivesNewDbContextAndEnsureCreated()
    {
        await using var f = await Fixture.Create(); var save = new SaveWeddingProfileCommandHandler(f.Db, new Current(f.Owner.Id));
        await save.Handle(new("Minh", "Anh", DateTime.UtcNow.AddDays(100), "HCM", 100000000, 10, "Classic", null, true), default);
        await using var reopened = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(f.Connection).Options);
        Assert.False(await reopened.Database.EnsureCreatedAsync()); Assert.Equal(35, await reopened.ChecklistTasks.CountAsync());
        Assert.Equal(14, await reopened.BudgetItems.CountAsync()); Assert.Equal(2, await reopened.Users.CountAsync());
    }
    [Fact] public async Task SavingUnchangedProfilePreservesManuallyEditedTaskDueDate()
    {
        await using var f = await Fixture.Create(); var handler = new SaveWeddingProfileCommandHandler(f.Db, new Current(f.Owner.Id));
        var command = new SaveWeddingProfileCommand("Minh", "Anh", DateTime.UtcNow.AddDays(100), "City", 100000000, 10, "Classic", null, true);
        await handler.Handle(command, default); var task = await f.Db.ChecklistTasks.FirstAsync(); var chosenDue = DateTime.UtcNow.Date.AddDays(33);
        task.Update(task.Title, task.Milestone, chosenDue, task.Notes); await f.Db.SaveChangesAsync();
        await handler.Handle(command, default); Assert.Equal(chosenDue, (await f.Db.ChecklistTasks.SingleAsync(t => t.Id == task.Id)).DueDate);
    }
    [Fact] public async Task InvitationTimeRetainsVietnamOffsetAfterDatabaseRoundtrip()
    {
        await using var f = await Fixture.Create();
        var utc = new DateTime(2027, 12, 20, 11, 0, 0, DateTimeKind.Utc); // 18:00 in Vietnam.
        var inv = WeddingInvitation.Create(f.Owner.Id, "offset-test", "Minh", "Anh", utc, "Venue", "Address", null, null); f.Db.WeddingInvitations.Add(inv); await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear(); var response = await new GetPublicInvitationQueryHandler(f.Db).Handle(new("offset-test"), default);
        Assert.Equal(TimeSpan.FromHours(7), response.EventDate.Offset); Assert.Equal(18, response.EventDate.Hour); Assert.Equal(utc, response.EventDate.UtcDateTime);
    }
    [Fact] public async Task JoinIsSingleUseAndExpiredInvitesAreDenied()
    {
        await using var f = await Fixture.Create(); var profile = await f.Profile();
        var invite = WeddingJoinInvitation.Create(profile.Id, "Editor", f.Partner.Email); f.Db.WeddingJoinInvitations.Add(invite); await f.Db.SaveChangesAsync();
        var join = new JoinWeddingHandler(f.Db, new Current(f.Partner.Id));
        await join.Handle(new(invite.Code), default);
        await Assert.ThrowsAsync<DomainException>(() => join.Handle(new(invite.Code), default));
        var expired = WeddingJoinInvitation.Create(profile.Id, "Viewer", null); f.Db.WeddingJoinInvitations.Add(expired);
        f.Db.Entry(expired).Property(x => x.ExpiresAt).CurrentValue = DateTime.UtcNow.AddDays(-1); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => join.Handle(new(expired.Code), default));
    }
    [Fact] public async Task SeatingNeverOverfillsAndPreservesUnassignedLargeGroups()
    {
        await using var f = await Fixture.Create(); var profile = await f.Profile(); var current = new Current(f.Owner.Id);
        var table = SeatTable.Create(profile.Id, "Bàn 1", 3); f.Db.SeatTables.Add(table); await f.Db.SaveChangesAsync();
        var save = new SaveWeddingGuestsHandler(f.Db, current);
        await save.Handle(new([new(null, "Gia đình A", PartySize: 2, Status: "Attending", TableId: table.Id)]), default);
        await Assert.ThrowsAsync<DomainException>(() => save.Handle(new([new(null, "Gia đình B", PartySize: 2, Status: "Attending", TableId: table.Id)]), default));
        Assert.Single(await f.Db.WeddingGuests.ToListAsync());
        await save.Handle(new([new(null, "Gia đình C", PartySize: 2, Status: "Attending")]), default);
        var count = await new AutoAssignTablesHandler(f.Db, current).Handle(new(), default);
        Assert.Equal(0, count); Assert.Single(await f.Db.WeddingGuests.Where(g => g.TableId == null).ToListAsync());
    }
    [Fact] public async Task ReservedSlugIsRejectedAndHiddenInvitationCannotBeReadOrRsvped()
    {
        await using var f = await Fixture.Create();
        var save = new SaveInvitationCommandHandler(f.Db, new Current(f.Owner.Id));
        await Assert.ThrowsAsync<DomainException>(() => save.Handle(new("planner", "Minh", "Anh", DateTime.UtcNow.AddDays(10), "Venue", "Address", null, null, null, null, "navy-cream", null), default));
        var invitation = WeddingInvitation.Create(f.Owner.Id, "private-couple", "Minh", "Anh", DateTime.UtcNow.AddDays(10), "Venue", "Address", null, null); invitation.SetPublished(false);
        f.Db.WeddingInvitations.Add(invitation); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => new GetPublicInvitationQueryHandler(f.Db).Handle(new("private-couple"), default));
        await Assert.ThrowsAsync<DomainException>(() => new SubmitRsvpCommandHandler(f.Db).Handle(new("private-couple", "Guest", null, "Attending"), default));
    }
    [Fact] public async Task RsvpUsesIdempotencyKeyAndSynchronizesPlannerGuestOnce()
    {
        await using var f = await Fixture.Create(); await f.Profile();
        f.Db.WeddingInvitations.Add(WeddingInvitation.Create(f.Owner.Id, "couple", "Minh", "Anh", DateTime.UtcNow.AddDays(10), "Venue", "Address", null, null)); await f.Db.SaveChangesAsync();
        var submit = new SubmitRsvpCommandHandler(f.Db); var r = new SubmitRsvpCommand("couple", "Guest", null, "Tentative", 1, "Happy wedding", SubmissionKey: Guid.NewGuid().ToString());
        await submit.Handle(r, default); await submit.Handle(r, default);
        Assert.Single(await f.Db.GuestRSVPs.ToListAsync()); Assert.Single(await f.Db.WeddingGuests.ToListAsync());
        var guest = await f.Db.WeddingGuests.SingleAsync(); Assert.Equal("Maybe", guest.Status); Assert.Equal(2, guest.PartySize);
    }
    [Fact] public void All16TemplatesHaveStableDistinctIdsAndKeys()
    { var templates = InvitationTemplate.Catalog; Assert.Equal(16, templates.Length); Assert.Equal(16, templates.Select(t => t.Id).Distinct().Count()); Assert.Equal(16, templates.Select(t => t.Key).Distinct().Count()); }
    [Fact] public async Task OnboardingCompletionIsDatabaseBackedAndOnlyCompletesAfterSubmittingWizard()
    {
        await using var f = await Fixture.Create(); var user = new Current(f.Owner.Id); var status = new OnboardingStatusHandler(f.Db, user);
        Assert.False((await status.Handle(new(), default)).Completed);
        var save = new SaveWeddingProfileCommandHandler(f.Db, user); var draft = new SaveWeddingProfileCommand("Minh", "Anh", DateTime.UtcNow.AddDays(100), "HCM", 100000000, 100, "Classic", null, false);
        await save.Handle(draft, default); Assert.False((await status.Handle(new(), default)).Completed);
        await save.Handle(draft with { GeneratePlan = true }, default); var completed = await status.Handle(new(), default); Assert.True(completed.Completed); Assert.NotNull(completed.CompletedAt);
        f.Db.ChangeTracker.Clear(); Assert.True((await status.Handle(new(), default)).Completed);
    }
    [Fact] public async Task JoinedPartnerUsesOwnersPersistedCompletionWithoutCreatingAnotherProfile()
    {
        await using var f = await Fixture.Create(); var profile = await f.Profile(); profile.CompleteOnboarding();
        f.Db.WeddingMembers.Add(WeddingMember.Create(profile.Id, f.Partner.Id, "Viewer")); await f.Db.SaveChangesAsync();
        var status = await new OnboardingStatusHandler(f.Db, new Current(f.Partner.Id)).Handle(new(), default);
        Assert.True(status.Completed); Assert.Equal(profile.Id, status.WeddingProfileId); Assert.Equal("Viewer", status.Role);
        Assert.Single(await f.Db.WeddingProfiles.ToListAsync());
    }
    [Fact] public async Task AcceptedViewerMembershipUsesExistingWeddingEvenBeforeOwnersFinalWizardSubmission()
    {
        await using var f = await Fixture.Create(); var existing = await f.Profile();
        f.Db.WeddingMembers.Add(WeddingMember.Create(existing.Id, f.Partner.Id, "Viewer")); await f.Db.SaveChangesAsync();
        Assert.False((await new OnboardingStatusHandler(f.Db, new Current(f.Owner.Id)).Handle(new(), default)).Completed);
        Assert.True((await new OnboardingStatusHandler(f.Db, new Current(f.Partner.Id)).Handle(new(), default)).Completed);
        Assert.Single(await f.Db.WeddingProfiles.ToListAsync());
    }

    private sealed record Current(Guid Id) : ICurrentUserService { public Guid? UserId => Id; public string? Email => null; public string? Role => "Customer"; }
    private sealed class Fixture : IAsyncDisposable
    {
        public ApplicationDbContext Db = null!; public SqliteConnection Connection = null!; public User Owner = null!; public User Partner = null!;
        public static async Task<Fixture> Create()
        {
            var f = new Fixture(); f.Connection = new SqliteConnection("Data Source=:memory:"); await f.Connection.OpenAsync();
            f.Db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(f.Connection).Options); await f.Db.Database.EnsureCreatedAsync();
            f.Owner = User.Create("Owner", "owner@example.com", "0900000000", "hash", UserRole.Customer); f.Partner = User.Create("Partner", "partner@example.com", "0900000001", "hash", UserRole.Customer);
            f.Db.Users.AddRange(f.Owner, f.Partner); await f.Db.SaveChangesAsync(); return f;
        }
        public async Task<WeddingProfile> Profile() { var p = WeddingProfile.Create(Owner.Id); p.Update("Minh", "Anh", DateTime.UtcNow.AddDays(100), "HCM", 100000000, 100, "Classic", null); Db.WeddingProfiles.Add(p); await Db.SaveChangesAsync(); return p; }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }
}
