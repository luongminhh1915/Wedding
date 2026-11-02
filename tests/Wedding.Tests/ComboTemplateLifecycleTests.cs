using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Combos;
using Wedding.Application.Features.Contracts.Commands.ConfirmContract;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public class ComboTemplateLifecycleTests
{
    [Fact]
    public async Task DraftIsPersistedPrivatelyAndSubmitCreatesANewImmutablePendingSnapshot()
    {
        await using var f = await Fixture.Create();
        var saved = await f.Save(f.Command with { Name = "Private draft title", SubmitForApproval = false });
        Assert.Equal("Draft", saved.ModerationStatus); Assert.False(saved.IsActive);
        Assert.NotNull(saved.LatestRevision); Assert.Equal("Draft", saved.LatestRevision.Status);
        var first = await f.Db.ComboTemplateRevisions.AsNoTracking().SingleAsync();
        Assert.Equal(f.Owner.Id, first.CreatedById); Assert.Equal("VendorOwner", first.CreatedByRole);
        Assert.Null(first.SubmittedAt);
        var preview = await f.Preview(saved.Id);
        Assert.Equal("Private draft title", preview.Name); Assert.Equal(2, preview.Items.Count);
        Assert.Equal("Synthetic terms", preview.Content?.Terms);
        await f.AssertNotPublic(saved.Id);
        await f.Submit(saved.Id);
        var own = await f.Own(saved.Id); Assert.Equal("Pending", own.LatestRevision?.Status);
        var revisions = await f.Db.ComboTemplateRevisions.AsNoTracking().OrderBy(item => item.Sequence).ToListAsync();
        Assert.Equal(2, revisions.Count); Assert.Equal(new[] { 1, 2 }, revisions.Select(item => item.Sequence));
        Assert.Equal(first.SnapshotJson, revisions[0].SnapshotJson);
        Assert.Equal(first.SnapshotJson, revisions[1].SnapshotJson);
        Assert.NotNull(revisions[1].SubmittedAt); await f.AssertNotPublic(saved.Id);
    }

    [Fact]
    public async Task RejectRequiresReasonAndResubmissionPreservesRejectedReviewAndSnapshotHistory()
    {
        await using var f = await Fixture.Create();
        var saved = await f.Save(f.Command);
        var id = saved.LatestRevision!.Id;
        var before = await f.TemplateSnapshot(saved.Id);
        foreach (var note in new[] { null, "", "  ", new string('a', 2001) })
        {
            await Assert.ThrowsAsync<DomainException>(() => f.Review(saved.Id, false, note, id));
            Assert.Equal(before, await f.TemplateSnapshot(saved.Id));
        }
        await f.Review(saved.Id, false, "  Add the missing deliverables  ", id);
        var rejected = await f.Db.ComboTemplateRevisions.AsNoTracking().SingleAsync();
        Assert.Equal("Rejected", rejected.Status); Assert.Equal("Add the missing deliverables", rejected.ReviewNote);
        Assert.Equal(f.Moderator.Id, rejected.ReviewedById); Assert.Equal("Moderator", rejected.ReviewedByRole);
        Assert.NotNull(rejected.ReviewedAt); await f.AssertNotPublic(saved.Id);
        await f.Submit(saved.Id);
        var history = await f.Db.ComboTemplateRevisions.AsNoTracking().OrderBy(item => item.Sequence).ToListAsync();
        Assert.Equal(2, history.Count); Assert.Equal("Pending", history[1].Status);
        Assert.Equal("Rejected", history[0].SupersededFromStatus); Assert.NotNull(history[0].SupersededAt);
        Assert.Equal(rejected.ReviewNote, history[0].ReviewNote); Assert.Equal(rejected.ReviewedAt, history[0].ReviewedAt);
        Assert.Equal(rejected.SnapshotJson, history[0].SnapshotJson); Assert.Equal(rejected.SnapshotJson, history[1].SnapshotJson);
        var historic = await f.Preview(saved.Id, history[0].Id);
        Assert.Equal("Synthetic combo", historic.Name); Assert.Equal(2, historic.Items.Count);
        await f.Review(saved.Id, true, "Reviewed and approved", history[1].Id);
        Assert.Equal("Approved", (await f.Own(saved.Id)).ModerationStatus);
        Assert.Contains((await f.Builder()).Templates, item => item.Id == saved.Id);
    }

    [Theory]
    [InlineData("VendorOwner")] [InlineData("Moderator")] [InlineData("SuperAdmin")]
    public async Task EverySaveStagesAProposalAndNeverOverwritesPublishedContentBeforeExplicitApproval(string role)
    {
        await using var f = await Fixture.Create();
        var published = await f.Publish(); var before = await f.PublishedSnapshot(published.Id);
        var originalRevision = await f.Db.ComboTemplateRevisions.AsNoTracking().SingleAsync();
        var actor = role == "VendorOwner" ? f.VendorActor : role == "Moderator" ? f.ModeratorActor : f.AdminActor;
        var proposal = await f.Save(f.Command with { Id = published.Id, Name = "Unapproved replacement", IsActive = false,
            Items = [f.Slot(f.Venue), f.Slot(f.PhotoAlternative)], Content = new(["https://images.example.invalid/proposed.jpg"], "Unapproved terms") }, actor);
        Assert.Equal("Approved", proposal.ModerationStatus); Assert.Equal("Pending", proposal.LatestRevision?.Status);
        Assert.Equal("Unapproved replacement", proposal.LatestRevision?.Proposal?.Name);
        Assert.Equal(before, await f.PublishedSnapshot(published.Id));
        var live = Assert.Single((await f.Builder()).Templates, item => item.Id == published.Id);
        Assert.Equal("Synthetic combo", live.Name); Assert.Equal("Synthetic terms", live.Content?.Terms);
        Assert.Null(live.LatestRevision); Assert.True(live.RevisionHistory == null || live.RevisionHistory.Count == 0);
        Assert.DoesNotContain("Unapproved replacement", JsonSerializer.Serialize(live));
        Assert.Equal("Unapproved replacement", (await f.Preview(published.Id)).Name);
        await f.Review(published.Id, true, "Explicit approval", proposal.LatestRevision!.Id);
        var current = await f.Own(published.Id);
        // Reviewing new content does not silently toggle an existing published combo.
        Assert.Equal("Unapproved replacement", current.Name); Assert.True(current.IsActive);
        Assert.Equal(f.PhotoAlternative.Id, current.Items.Single(item => item.CategoryId == f.Photo.CategoryId).ServiceItemId);
        Assert.Equal("Unapproved terms", current.Content?.Terms);
        var original = await f.Db.ComboTemplateRevisions.AsNoTracking().SingleAsync(item => item.Id == originalRevision.Id);
        Assert.Equal("Approved", original.Status); Assert.Equal(originalRevision.SnapshotJson, original.SnapshotJson);
    }

    [Fact]
    public async Task StaleReviewCannotApproveSupersededOrAlreadyReviewedRevision()
    {
        await using var f = await Fixture.Create();
        var first = await f.Save(f.Command);
        var next = await f.Save(f.Command with { Id = first.Id, Name = "Latest pending revision" });
        var before = await f.TemplateSnapshot(first.Id);
        await Assert.ThrowsAsync<DomainException>(() => f.Review(first.Id, true, null, first.LatestRevision!.Id));
        Assert.Equal(before, await f.TemplateSnapshot(first.Id));
        await f.Review(first.Id, true, null, next.LatestRevision!.Id);
        var approved = await f.TemplateSnapshot(first.Id);
        await Assert.ThrowsAsync<DomainException>(() => f.Review(first.Id, false, "Too late", next.LatestRevision!.Id));
        Assert.Equal(approved, await f.TemplateSnapshot(first.Id));
    }

    [Fact]
    public async Task RejectingPublishedChangesKeepsLiveContentAndRetainsReasonWhileOwnerRevisesAndResubmits()
    {
        await using var f = await Fixture.Create(); var published = await f.Publish();
        var before = await f.PublishedSnapshot(published.Id);
        var saved = await f.Save(f.Command with { Id = published.Id, Name = "Rejected proposed title", Content = new(Terms: "Rejected proposed terms") });
        await f.Review(published.Id, false, "Provide clearer deliverables", saved.LatestRevision!.Id);
        Assert.Equal(before, await f.PublishedSnapshot(published.Id));
        Assert.Equal("Synthetic combo", Assert.Single((await f.Builder()).Templates, item => item.Id == published.Id).Name);
        var own = await f.Own(published.Id); Assert.Equal("Approved", own.ModerationStatus); Assert.Equal("Rejected", own.LatestRevision!.Status);
        Assert.Equal("Provide clearer deliverables", own.LatestRevision.ReviewNote);
        var notification = await f.Db.Notifications.AsNoTracking().OrderByDescending(item => item.CreatedAt).FirstAsync();
        Assert.Equal(f.Owner.Id, notification.UserId); Assert.Equal("InApp", notification.Channel);
        Assert.Contains("Provide clearer deliverables", notification.Message); Assert.Null(notification.ContextKind); Assert.Null(notification.ContextId);
        var amended = await f.Save(f.Command with { Id = published.Id, Name = "Amended proposal", SubmitForApproval = false });
        Assert.Equal(before, await f.PublishedSnapshot(published.Id));
        var old = await f.Db.ComboTemplateRevisions.AsNoTracking().SingleAsync(item => item.Id == saved.LatestRevision.Id);
        Assert.Equal("Superseded", old.Status); Assert.Equal("Rejected", old.SupersededFromStatus); Assert.Equal("Provide clearer deliverables", old.ReviewNote);
        await f.Submit(published.Id);
        var pending = (await f.Own(published.Id)).LatestRevision!; Assert.Equal("Pending", pending.Status);
        Assert.True(pending.Sequence > amended.LatestRevision!.Sequence);
        await f.Review(published.Id, true, RevisionId: pending.Id);
        Assert.Equal("Amended proposal", (await f.Own(published.Id)).Name);
        Assert.Equal("Approved", (await f.Db.ComboTemplateRevisions.AsNoTracking().OrderBy(item => item.Sequence).FirstAsync()).Status);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task LegacyPendingCanBeExplicitlyReviewedWithoutInventingRevisionHistory(bool approve)
    {
        await using var f = await Fixture.Create();
        var legacy = ComboTemplate.Create("Legacy pending combo", "Popular", "Modern", "Original legacy text", null, 0);
        legacy.SetScope(f.Vendor.Id, "HCM", .03m, false);
        foreach (var service in new[] { f.Venue, f.Photo })
        {
            var slot = ComboTemplateItem.Create(legacy.Id, service.CategoryId, service.ListingId, false);
            slot.ConfigureItems(service.Id, "[]"); legacy.Items.Add(slot);
        }
        f.Db.ComboTemplates.Add(legacy); await f.Db.SaveChangesAsync();
        await f.AssertNotPublic(legacy.Id);
        await f.Review(legacy.Id, approve, "Explicit legacy review");
        var reloaded = await f.Own(legacy.Id);
        Assert.Equal(approve ? "Approved" : "Rejected", reloaded.ModerationStatus); Assert.Equal(approve, reloaded.IsActive);
        Assert.Equal("Original legacy text", reloaded.Description); Assert.Equal(2, reloaded.Items.Count);
        Assert.Empty(await f.Db.ComboTemplateRevisions.AsNoTracking().ToListAsync());
        Assert.Equal("Explicit legacy review", (await f.Db.ComboTemplates.AsNoTracking().SingleAsync()).ReviewNote);
        if (approve) Assert.Contains((await f.Builder()).Templates, item => item.Id == legacy.Id);
        else await f.AssertNotPublic(legacy.Id);
    }

    [Fact]
    public async Task ApprovalRevalidatesDependenciesAndRollsBackWithoutChangingThePublishedVersion()
    {
        await using var f = await Fixture.Create();
        var published = await f.Publish();
        var next = await f.Save(f.Command with { Id = published.Id, Name = "Pending alternative", Items = [f.Slot(f.Venue), f.Slot(f.PhotoAlternative)] });
        var alternative = await f.Db.ServiceItems.SingleAsync(item => item.Id == f.PhotoAlternative.Id);
        f.Db.Entry(alternative).Property(item => item.IsActive).CurrentValue = false; await f.Db.SaveChangesAsync();
        var before = await f.TemplateSnapshot(published.Id);
        await Assert.ThrowsAsync<DomainException>(() => f.Review(published.Id, true, null, next.LatestRevision!.Id));
        Assert.Equal(before, await f.TemplateSnapshot(published.Id));
        Assert.Equal("Synthetic combo", Assert.Single((await f.Builder()).Templates, item => item.Id == published.Id).Name);
    }

    [Fact]
    public async Task ActivityIsIndependentOfModerationAndCannotActivateAnUnapprovedTemplate()
    {
        await using var f = await Fixture.Create();
        var pending = await f.Save(f.Command);
        await Assert.ThrowsAsync<DomainException>(() => f.Activity(pending.Id, true));
        await f.Review(pending.Id, true);
        var snapshot = await f.Db.ComboTemplateRevisions.AsNoTracking().SingleAsync();
        await f.Activity(pending.Id, false);
        var inactive = await f.Own(pending.Id); Assert.Equal("Approved", inactive.ModerationStatus); Assert.False(inactive.IsActive);
        await f.AssertNotPublic(pending.Id);
        await f.Activity(pending.Id, true);
        Assert.True((await f.Own(pending.Id)).IsActive);
        var history = await f.Db.ComboTemplateRevisions.AsNoTracking().SingleAsync();
        Assert.Equal(snapshot.SnapshotJson, history.SnapshotJson); Assert.Equal("Approved", history.Status);
        await new ArchiveComboTemplateCommandHandler(f.Db, f.VendorActor, new Audit()).Handle(new(pending.Id), default);
        Assert.Equal("Approved", (await f.Own(pending.Id)).ModerationStatus); await f.AssertNotPublic(pending.Id);
    }

    [Theory]
    [InlineData("expired")] [InlineData("future")] [InlineData("inactive-service")]
    [InlineData("inactive-listing")] [InlineData("inactive-option")]
    [InlineData("inactive-vendor")] [InlineData("inactive-owner")]
    public async Task PublicBuilderCatalogAndDirectPricingRespectValidityAndDependencyAvailability(string condition)
    {
        await using var f = await Fixture.Create();
        var content = f.Command.Content;
        if (condition == "expired") content = new(ValidFromUtc: DateTime.UtcNow.AddDays(-2), ValidUntilUtc: DateTime.UtcNow.AddDays(-1));
        if (condition == "future") content = new(ValidFromUtc: DateTime.UtcNow.AddDays(1), ValidUntilUtc: DateTime.UtcNow.AddDays(2));
        var command = f.Command with { Content = content };
        if (condition == "inactive-option") command = command with { Items = [f.Slot(f.Venue), f.Slot(f.Photo) with { AllowedItemIds = [f.PhotoAlternative.Id] }] };
        var published = await f.Publish(command);
        if (condition == "inactive-service") f.Db.Entry(await f.Db.ServiceItems.SingleAsync(item => item.Id == f.Venue.Id)).Property(item => item.IsActive).CurrentValue = false;
        if (condition == "inactive-listing") f.Db.Entry(await f.Db.Listings.SingleAsync(item => item.Id == f.Venue.ListingId)).Property(item => item.IsActive).CurrentValue = false;
        if (condition == "inactive-option") f.Db.Entry(await f.Db.ServiceItems.SingleAsync(item => item.Id == f.PhotoAlternative.Id)).Property(item => item.IsActive).CurrentValue = false;
        if (condition == "inactive-vendor") f.Db.Entry(await f.Db.Vendors.SingleAsync(item => item.Id == f.Vendor.Id)).Property(item => item.IsActive).CurrentValue = false;
        if (condition == "inactive-owner") (await f.Db.Users.SingleAsync(item => item.Id == f.Owner.Id)).SetStatus(false);
        await f.Db.SaveChangesAsync();
        var before = await f.FinancialSnapshot(); await f.AssertNotPublic(published.Id);
        Assert.Equal(before, await f.FinancialSnapshot());
        if (condition is "expired" or "inactive-service")
            await Assert.ThrowsAsync<DomainException>(() => f.Activity(published.Id, true));
    }

    [Fact]
    public async Task VendorCannotReadOrModifyAnotherVendorsProposalHistoryOrSubmitItsRevision()
    {
        await using var f = await Fixture.Create();
        var saved = await f.Save(f.Command with { SubmitForApproval = false }); var before = await f.TemplateSnapshot(saved.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => f.Preview(saved.Id, actor: f.OtherVendorActor));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Save(f.Command with { Id = saved.Id,
            Items = [f.Slot(f.ForeignPhoto)] }, f.OtherVendorActor));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Submit(saved.Id, f.OtherVendorActor));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Activity(saved.Id, false, f.OtherVendorActor));
        Assert.Equal(before, await f.TemplateSnapshot(saved.Id));
        var own = await new GetAllComboTemplatesQueryHandler(f.Db, f.OtherVendorActor).Handle(new(true), default);
        Assert.Empty(own);
        await Assert.ThrowsAsync<DomainException>(() => new GetAllComboTemplatesQueryHandler(f.Db, f.OtherVendorActor).Handle(new(false), default));
    }

    [Theory]
    [InlineData("Customer")] [InlineData("Finance")] [InlineData("CustomerCare")]
    public async Task NonCatalogRolesCannotSaveSubmitPreviewReviewOrToggleActivity(string role)
    {
        await using var f = await Fixture.Create();
        var saved = await f.Save(f.Command with { SubmitForApproval = false });
        var user = f.Users.Single(item => item.Role.ToString() == role); var actor = new Current(user.Id, role);
        var before = await f.TemplateSnapshot(saved.Id);
        await Assert.ThrowsAsync<DomainException>(() => f.Save(f.Command with { Id = saved.Id }, actor));
        await Assert.ThrowsAsync<DomainException>(() => f.Submit(saved.Id, actor));
        await Assert.ThrowsAsync<DomainException>(() => f.Activity(saved.Id, false, actor));
        await Assert.ThrowsAsync<DomainException>(() => f.Review(saved.Id, true, actor: actor));
        await Assert.ThrowsAsync<DomainException>(() => f.Preview(saved.Id, actor: actor));
        Assert.Equal(before, await f.TemplateSnapshot(saved.Id));
    }

    [Fact]
    public async Task ClaimedStaffRoleWithoutActiveMatchingDatabaseIdentityNeverApproves()
    {
        await using var f = await Fixture.Create(); var saved = await f.Save(f.Command); var before = await f.TemplateSnapshot(saved.Id);
        foreach (var actor in new[] { f.VendorActor, new Current(null, "SuperAdmin"), new Current(Guid.Empty, "Moderator"),
            new Current(Guid.NewGuid(), "SuperAdmin"), new Current(f.Owner.Id, "SuperAdmin"), new Current(f.Customer.Id, "Moderator") })
            await Assert.ThrowsAsync<DomainException>(() => f.Review(saved.Id, true, actor: actor));
        (await f.Db.Users.SingleAsync(item => item.Id == f.Moderator.Id)).SetStatus(false); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => f.Review(saved.Id, true));
        Assert.Equal(before, await f.TemplateSnapshot(saved.Id));
    }

    [Theory]
    [InlineData("inactive-owner")] [InlineData("inactive-shop")] [InlineData("changed-role")]
    public async Task DisabledOrRoleChangedVendorCannotStageOrSubmitAnotherRevision(string condition)
    {
        await using var f = await Fixture.Create(); var saved = await f.Save(f.Command with { SubmitForApproval = false });
        if (condition == "inactive-owner") f.Owner.SetStatus(false);
        if (condition == "inactive-shop") f.Db.Entry(f.Vendor).Property(item => item.IsActive).CurrentValue = false;
        if (condition == "changed-role") f.Db.Entry(f.Owner).Property(item => item.Role).CurrentValue = UserRole.Customer;
        await f.Db.SaveChangesAsync(); var before = await f.TemplateSnapshot(saved.Id);
        await Assert.ThrowsAsync<DomainException>(() => f.Save(f.Command with { Id = saved.Id }));
        await Assert.ThrowsAsync<DomainException>(() => f.Submit(saved.Id));
        Assert.Equal(before, await f.TemplateSnapshot(saved.Id));
    }

    [Fact]
    public async Task SnapshotAuthorsAndContentsAreImmutableAndRevisionsCannotBeDeleted()
    {
        await using var f = await Fixture.Create(); await f.Save(f.Command);
        foreach (var property in new[] { "SnapshotJson", "CreatedById", "CreatedByRole", "CreatedAt", "ComboTemplateId", "Sequence" })
        {
            await using var attempt = f.OpenContext(); var revision = await attempt.ComboTemplateRevisions.SingleAsync();
            var value = attempt.Entry(revision).Property(property);
            value.CurrentValue = property switch
            {
                "SnapshotJson" => "{\"tampered\":true}", "CreatedByRole" => "SuperAdmin", "CreatedAt" => revision.CreatedAt.AddDays(1),
                "Sequence" => revision.Sequence + 1, _ => Guid.NewGuid()
            };
            await Assert.ThrowsAsync<InvalidOperationException>(() => attempt.SaveChangesAsync());
        }
        await using var deletion = f.OpenContext(); deletion.ComboTemplateRevisions.Remove(await deletion.ComboTemplateRevisions.SingleAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => deletion.SaveChangesAsync());
        Assert.Single(await f.Db.ComboTemplateRevisions.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task FailureAfterSavingRollsBackProposalReviewAndPublishedItemsAsOneTransaction()
    {
        await using var f = await Fixture.Create(); var published = await f.Publish(); var before = await f.TemplateSnapshot(published.Id);
        await using (var failing = new FailingAfterSaveContext(f.Options))
            await Assert.ThrowsAsync<InvalidOperationException>(() => new SaveComboTemplateCommandHandler(failing, new Audit(), f.VendorActor)
                .Handle(f.Command with { Id = published.Id, Name = "Must roll back" }, default));
        Assert.Equal(before, await f.TemplateSnapshot(published.Id));
        var pending = await f.Save(f.Command with { Id = published.Id, Name = "Awaiting review" }); before = await f.TemplateSnapshot(published.Id);
        var notificationCount = await f.Db.Notifications.CountAsync();
        await using (var failing = new FailingAfterSaveContext(f.Options))
            await Assert.ThrowsAsync<InvalidOperationException>(() => new ReviewComboTemplateCommandHandler(failing, new Audit(), f.ModeratorActor)
                .Handle(new(published.Id, true, RevisionId: pending.LatestRevision!.Id), default));
        Assert.Equal(before, await f.TemplateSnapshot(published.Id));
        Assert.Equal(notificationCount, await f.Db.Notifications.CountAsync());
    }

    [Fact]
    public async Task OmittedContentInheritsLatestProposalAndExplicitEmptyContentClearsOnlyTheNewProposal()
    {
        await using var f = await Fixture.Create(); var published = await f.Publish();
        var before = await f.PublishedSnapshot(published.Id);
        var metadata = new ComboTemplateContentDto(["https://images.example.invalid/proposed%20gallery.jpg"], "Replacement terms",
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(10));
        var first = await f.Save(f.Command with { Id = published.Id, Content = metadata, SubmitForApproval = false });
        var inherited = await f.Save(f.Command with { Id = published.Id, Name = "Inherited metadata", Content = null, SubmitForApproval = false });
        Assert.Equal(JsonSerializer.Serialize(first.LatestRevision!.Proposal!.Content), JsonSerializer.Serialize(inherited.LatestRevision!.Proposal!.Content));
        Assert.Equal(before, await f.PublishedSnapshot(published.Id));
        var history = await f.Db.ComboTemplateRevisions.AsNoTracking().OrderBy(item => item.Sequence).ToListAsync();
        Assert.Equal("Approved", history[0].Status); Assert.Equal("Superseded", history[1].Status);
        Assert.Equal("Draft", history[1].SupersededFromStatus);
        Assert.All((await f.Own(published.Id)).RevisionHistory!, item => Assert.Null(item.Proposal));
        var cleared = await f.Save(f.Command with { Id = published.Id, Content = new() });
        Assert.Empty(cleared.LatestRevision!.Proposal!.Content!.GalleryUrls!);
        Assert.Equal("", cleared.LatestRevision.Proposal.Content.Terms);
        Assert.Null(cleared.LatestRevision.Proposal.Content.ValidFromUtc); Assert.Null(cleared.LatestRevision.Proposal.Content.ValidUntilUtc);
        Assert.Equal(before, await f.PublishedSnapshot(published.Id));
        await f.Review(published.Id, true, RevisionId: cleared.LatestRevision.Id);
        var current = await f.Own(published.Id); Assert.Empty(current.Content!.GalleryUrls!); Assert.Equal("", current.Content.Terms);
        var old = await f.Preview(published.Id, first.LatestRevision.Id);
        Assert.Equal("Replacement terms", old.Content!.Terms); Assert.Single(old.Content.GalleryUrls!);
    }

    [Theory]
    [InlineData("gallery-limit")] [InlineData("gallery-credentials")] [InlineData("gallery-javascript")]
    [InlineData("gallery-traversal")] [InlineData("terms-limit")] [InlineData("dates-reversed")]
    [InlineData("image-unsafe")] [InlineData("name-limit")] [InlineData("style-limit")]
    [InlineData("tier-invalid")] [InlineData("description-limit")] [InlineData("region-limit")]
    [InlineData("duplicate-category")] [InlineData("foreign-option")] [InlineData("options-limit")]
    public async Task InvalidMetadataOrItemsNeverChangesPublishedFieldsItemsOrRevisionHistory(string invalid)
    {
        await using var f = await Fixture.Create(); var published = await f.Publish();
        var before = await f.TemplateSnapshot(published.Id);
        var command = f.Command with { Id = published.Id, Name = "Should never persist" };
        command = invalid switch
        {
            "gallery-limit" => command with { Content = new(Enumerable.Range(0, 11).Select(index => $"https://images.example.invalid/{index}.jpg").ToList()) },
            "gallery-credentials" => command with { Content = new(["https://user:password@images.example.invalid/one.jpg"]) },
            "gallery-javascript" => command with { Content = new(["javascript:alert(1)"]) },
            "gallery-traversal" => command with { Content = new(["https://images.example.invalid/%252e%252e/private.jpg"]) },
            "terms-limit" => command with { Content = new(Terms: new string('t', 4001)) },
            "dates-reversed" => command with { Content = new(ValidFromUtc: DateTime.UtcNow.AddDays(2), ValidUntilUtc: DateTime.UtcNow.AddDays(1)) },
            "image-unsafe" => command with { ImageUrl = "file:///C:/private.jpg" },
            "name-limit" => command with { Name = new string('n', 151) },
            "style-limit" => command with { Style = new string('s', 51) },
            "tier-invalid" => command with { Tier = "Unrecognized" },
            "description-limit" => command with { Description = new string('d', 4001) },
            "region-limit" => command with { Region = new string('r', 1001) },
            "duplicate-category" => command with { Items = [f.Slot(f.Photo), f.Slot(f.PhotoAlternative)] },
            "foreign-option" => command with { Items = [f.Slot(f.Venue), f.Slot(f.Photo) with { AllowedItemIds = [f.ForeignPhoto.Id] }] },
            "options-limit" => command with { Items = [f.Slot(f.Photo) with { AllowedItemIds = Enumerable.Repeat(f.PhotoAlternative.Id, 101).ToList() }] },
            _ => throw new InvalidOperationException("Unknown validation fixture.")
        };
        await Assert.ThrowsAsync<DomainException>(() => f.Save(command));
        Assert.Equal(before, await f.TemplateSnapshot(published.Id));
    }

    [Fact]
    public async Task PreviewUsesSelectedRevisionServicesWithoutLeakingARevisionFromAnotherTemplate()
    {
        await using var f = await Fixture.Create();
        var first = await f.Save(f.Command with { SubmitForApproval = false });
        var second = await f.Save(f.Command with { Name = "Different template", SubmitForApproval = false });
        var preview = await f.Preview(first.Id, first.LatestRevision!.Id);
        Assert.NotNull(preview.PreviewOptions); Assert.Equal(2, preview.PreviewOptions.Count);
        Assert.All(preview.PreviewOptions, item => Assert.Equal(f.Vendor.Id, item.VendorId));
        Assert.Equal(new[] { f.Venue.Id, f.Photo.Id }.Order(), preview.PreviewOptions.Select(item => item.ServiceItemId).Order());
        await Assert.ThrowsAsync<NotFoundException>(() => f.Preview(first.Id, second.LatestRevision!.Id));
        Assert.Equal(first.LatestRevision.Id, (await f.Preview(first.Id)).LatestRevision!.Id);
    }

    [Fact]
    public async Task PublishingNewProposalNeverRepricesSubmittedOrderConfirmedContractOrPaidCommission()
    {
        await using var f = await Fixture.Create(); var published = await f.Publish();
        var order = await new SubmitComboOrderCommandHandler(f.Db, f.CustomerActor, new Notify()).Handle(new(published.Id, new(),
            f.Date, 100, f.Customer.PhoneNumber, "Synthetic RFQ", "HCM", f.Selections), default);
        var storedOrder = await f.Db.ComboOrders.SingleAsync();
        Assert.Equal(published.LatestRevision!.Id, storedOrder.ComboTemplateRevisionId);
        Assert.Contains(published.LatestRevision.Id.ToString(), storedOrder.ComboTemplateSnapshotJson);
        var frozenTemplateSnapshot = storedOrder.ComboTemplateSnapshotJson;
        var line = order.Items.Single(item => item.ServiceItemId == f.Venue.Id);
        var lead = await f.Db.Leads.SingleAsync(item => item.Id == line.LeadId);
        var contract = BookingContract.Create(lead.Id, null, f.Vendor.Id, f.Customer.Id, line.Price - line.DiscountShare,
            100000, null, f.Date); f.Db.BookingContracts.Add(contract); await f.Db.SaveChangesAsync();
        await new ConfirmContractCommandHandler(f.Db, f.CustomerActor).Handle(new(contract.Id), default);
        var paid = await f.Db.Commissions.SingleAsync(item => item.ContractId == contract.Id && item.Period == CommissionPeriod.Period1_Deposit);
        paid.MarkPaid("SYNTHETIC-COMBO-UNCHANGED-RECEIPT"); await f.Db.SaveChangesAsync();
        var before = await f.FinancialSnapshot();
        var revised = await f.Save(f.Command with { Id = published.Id, Name = "Newly published price configuration",
            SingleVendorDiscount = .05m, Items = [f.Slot(f.Venue), f.Slot(f.PhotoAlternative)], Content = new(Terms: "New future terms") });
        Assert.Equal(before, await f.FinancialSnapshot());
        await f.Review(published.Id, true, RevisionId: revised.LatestRevision!.Id);
        Assert.Equal(published.LatestRevision.Id, storedOrder.ComboTemplateRevisionId);
        Assert.Equal(frozenTemplateSnapshot, storedOrder.ComboTemplateSnapshotJson);
        Assert.Equal(before, await f.FinancialSnapshot());
        Assert.Equal(.08m, (await f.Db.BookingContracts.AsNoTracking().SingleAsync()).CommissionRateSnapshot);
    }

    [Fact]
    public async Task NewOrdersRequireAnApprovedRevisionForAnExactComboSnapshot()
    {
        await using var f = await Fixture.Create();
        var published = await f.Publish();
        var unversioned = ComboTemplate.Create("Synthetic legacy public combo", "Popular", "Modern",
            "Published without revision history", null, 0);
        unversioned.SetScope(f.Vendor.Id, "HCM", .03m, true);
        unversioned.Publish(f.Vendor.Id, "HCM", .03m, "{}", null, null, true, null);
        foreach (var item in published.Items)
        {
            var slot = ComboTemplateItem.Create(unversioned.Id, item.CategoryId, item.ListingId, item.IsOptional);
            if (item.ServiceItemId.HasValue)
                slot.ConfigureItems(item.ServiceItemId.Value, JsonSerializer.Serialize(item.AllowedItemIds ?? []));
            unversioned.Items.Add(slot);
        }
        f.Db.ComboTemplates.Add(unversioned);
        await f.Db.SaveChangesAsync();

        var handler = new SubmitComboOrderCommandHandler(f.Db, f.CustomerActor, new Notify());
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new(unversioned.Id, new(),
            f.Date, 100, f.Customer.PhoneNumber, "Synthetic RFQ", "HCM", f.Selections), default));
        Assert.Empty(await f.Db.ComboOrders.ToListAsync());
    }

    [SqlServerFact]
    public async Task SqlUpgradeKeepsPublishedLegacyComboItemsLockedOrderAndConfirmedFinancialSnapshots()
    {
        var database = $"WeddingPlanTest_ComboLifecycle_{Guid.NewGuid():N}";
        var source = Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER");
        var builder = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(source) || source == "1"
            ? "Server=localhost;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5" : source)
            { InitialCatalog = database };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(builder.ConnectionString).Options;
        await using var db = new ApplicationDbContext(options);
        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261019053813_ListingServiceDetails");
            var ownerId = Guid.NewGuid(); var customerId = Guid.NewGuid(); var moderatorId = Guid.NewGuid(); var vendorId = Guid.NewGuid();
            var listingId = Guid.NewGuid(); var serviceId = Guid.NewGuid(); var templateId = Guid.NewGuid(); var slotId = Guid.NewGuid();
            var orderId = Guid.NewGuid(); var lineId = Guid.NewGuid(); var leadId = Guid.NewGuid(); var contractId = Guid.NewGuid();
            var contractVersion = Guid.NewGuid(); var commissionId = Guid.NewGuid(); var commissionVersion = Guid.NewGuid();
            var categoryId = Guid.Parse("11111111-1111-1111-1111-111111111103");
            var createdAt = DateTime.UtcNow.AddDays(-10); var confirmedAt = createdAt.AddDays(1); var weddingDate = DateTime.UtcNow.AddDays(60);
            var priceLock = createdAt.AddDays(7); const string cancellation = "Original accepted legacy cancellation terms";
            const string selection = "{\"legacySelection\":\"immutable\"}";
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO Users (Id,FullName,Email,PhoneNumber,PasswordHash,SecurityVersion,Role,IsActive,CreatedAt)
                VALUES ({ownerId},{"Synthetic legacy combo owner"},{"legacy-owner@combo.example.invalid"},{"0901234567"},{"unchanged-combo-owner-hash"},{3},{"VendorOwner"},{true},{createdAt});
                INSERT INTO Users (Id,FullName,Email,PhoneNumber,PasswordHash,SecurityVersion,Role,IsActive,CreatedAt)
                VALUES ({customerId},{"Synthetic legacy combo customer"},{"legacy-customer@combo.example.invalid"},{"0901234569"},{"unchanged-combo-customer-hash"},{2},{"Customer"},{true},{createdAt});
                INSERT INTO Users (Id,FullName,Email,PhoneNumber,PasswordHash,SecurityVersion,Role,IsActive,CreatedAt)
                VALUES ({moderatorId},{"Synthetic combo moderator"},{"moderator@combo.example.invalid"},{"0901234568"},{"unchanged-combo-moderator-hash"},{1},{"Moderator"},{true},{createdAt});
                INSERT INTO Vendors (Id,UserId,BrandName,Slug,ContactPerson,Hotline,City,CommissionRate,RatingAvg,TotalReviews,IsActive,IsVerified,IsTopRated,AllowInCombos,MaxComboDiscount,CreatedAt)
                VALUES ({vendorId},{ownerId},{"Synthetic legacy combo shop"},{"synthetic-legacy-combo-shop"},{"Legacy owner"},{"0901234567"},{"HCM"},{.08m},{0m},{0},{true},{true},{false},{true},{.10m},{createdAt});
                INSERT INTO Listings (Id,VendorId,CategoryId,Title,Slug,MinPrice,MaxPrice,Description,Location,Status,ViewCount,FavoriteCount,CancellationPolicy,CreatedAt)
                VALUES ({listingId},{vendorId},{categoryId},{"Original legacy combo listing"},{"original-legacy-combo-listing"},{1000000m},{1000000m},{"Original legacy description"},{"HCM"},{"Active"},{17},{5},{cancellation},{createdAt});
                INSERT INTO ServiceItems (Id,VendorId,ListingId,CategoryId,Name,BasePrice,Unit,MinGuests,MaxGuests,Regions,CancellationPolicy,ModerationStatus,IsActive,MaxBookingsPerDay,CreatedAt)
                VALUES ({serviceId},{vendorId},{listingId},{categoryId},{"Original legacy combo service"},{1000000m},{"Package"},{1},{1000},{"HCM"},{cancellation},{"Approved"},{true},{10},{createdAt});
                INSERT INTO ComboTemplates (Id,Name,Tier,Style,Description,ImageUrl,IsActive,SortOrder,CreatedAt,OwnerVendorId,SingleVendorDiscount,ModerationStatus,Region)
                VALUES ({templateId},{"Original published legacy combo"},{"Popular"},{"Modern"},{"Original approved description"},{"https://images.example.invalid/legacy-combo.jpg"},{true},{7},{createdAt},{vendorId},{.03m},{"Approved"},{"HCM"});
                INSERT INTO ComboTemplateItems (Id,ComboTemplateId,CategoryId,ListingId,IsOptional,ServiceItemId,AllowedItemIdsJson,CreatedAt)
                VALUES ({slotId},{templateId},{categoryId},{listingId},{false},{serviceId},{"[]"},{createdAt});
                INSERT INTO ComboOrders (Id,Code,CustomerId,ComboTemplateId,WeddingDate,GuestCount,Note,Subtotal,DiscountPercent,DiscountAmount,Total,Region,PriceLockUntil,Status,CreatedAt)
                VALUES ({orderId},{"LEGACY-COMBO-ORDER"},{customerId},{templateId},{weddingDate},{100},{"Original accepted order note"},{1000000m},{.03m},{30000m},{970000m},{"HCM"},{priceLock},{"Completed"},{createdAt});
                INSERT INTO Leads (Id,CustomerId,VendorId,ListingId,RawPhoneNumber,MaskedPhoneNumber,IsPhoneUnlocked,Status,SlaDeadline,CreatedAt,Source)
                VALUES ({leadId},{customerId},{vendorId},{listingId},{"0901234569"},{"0901***569"},{true},{"Contracted"},{createdAt.AddHours(2)},{createdAt},{"Combo"});
                INSERT INTO ComboOrderItems (Id,ComboOrderId,ListingId,VendorId,CategoryId,Title,Price,DiscountShare,LeadId,ServiceItemId,SelectionJson,CancellationPolicySnapshot,Quantity,Unit,Status,CreatedAt)
                VALUES ({lineId},{orderId},{listingId},{vendorId},{categoryId},{"Original accepted line"},{1000000m},{30000m},{leadId},{serviceId},{selection},{cancellation},{1},{"Package"},{"Contracted"},{createdAt});
                INSERT INTO BookingContracts (Id,ContractCode,LeadId,VendorId,CustomerId,ContractValue,DepositAmount,WeddingDate,Status,VerificationDeadline,ConfirmedAt,CommissionRateSnapshot,CancellationPolicySnapshot,RevisionCount,Version,AppointmentDepositCredit,CreatedAt)
                VALUES ({contractId},{"LEGACY-COMBO-CONTRACT"},{leadId},{vendorId},{customerId},{970000m},{200000m},{weddingDate},{"Confirmed"},{createdAt.AddHours(72)},{confirmedAt},{.08m},{cancellation},{3},{contractVersion},{10000m},{createdAt});
                INSERT INTO Commissions (Id,ContractId,VendorId,Period,CommissionRate,CommissionAmount,DueDate,Status,PaidAt,PaymentReferenceCode,PaidAmount,Version,CreatedAt)
                VALUES ({commissionId},{contractId},{vendorId},{"Period1_Deposit"},{.08m},{38800m},{createdAt.AddDays(2)},{"Paid"},{confirmedAt},{"SYNTHETIC-LEGACY-COMBO-RECEIPT"},{38800m},{commissionVersion},{createdAt});
                """);
            await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges()); Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            var template = await db.ComboTemplates.AsNoTracking().Include(item => item.Items).SingleAsync(item => item.Id == templateId);
            Assert.Equal("Original published legacy combo", template.Name); Assert.Equal("Original approved description", template.Description);
            Assert.Equal("https://images.example.invalid/legacy-combo.jpg", template.ImageUrl); Assert.Equal("Popular", template.Tier);
            Assert.Equal("Modern", template.Style); Assert.Equal("Approved", template.ModerationStatus); Assert.True(template.IsActive);
            Assert.Equal(7, template.SortOrder); Assert.Equal(.03m, template.SingleVendorDiscount); Assert.Equal(vendorId, template.OwnerVendorId);
            Assert.Equal("HCM", template.Region); Assert.Equal(createdAt, template.CreatedAt); Assert.Null(template.ContentJson);
            Assert.Null(template.ValidFromUtc); Assert.Null(template.ValidUntilUtc); Assert.Null(template.ReviewNote);
            var slot = Assert.Single(template.Items); Assert.Equal(slotId, slot.Id); Assert.Equal(serviceId, slot.ServiceItemId);
            Assert.Equal(listingId, slot.ListingId); Assert.Equal(categoryId, slot.CategoryId); Assert.False(slot.IsOptional);
            Assert.Equal("[]", slot.AllowedItemIdsJson); Assert.Empty(await db.ComboTemplateRevisions.AsNoTracking().ToListAsync());
            var published = Assert.Single((await new GetComboBuilderQueryHandler(db).Handle(new(null), default)).Templates, item => item.Id == templateId);
            Assert.Null(published.LatestRevision); Assert.Null(published.RevisionHistory);
            var initialFinancial = await StoredFinancialSnapshot();

            var command = new SaveComboTemplateCommand(templateId, "New proposed legacy combo", "Popular", "Modern", "Future description", null,
                false, 8, [new(categoryId, Guid.NewGuid(), false, serviceId, [])], "HCM", .05m, Content: new(Terms: "New future terms"));
            var saved = await new SaveComboTemplateCommandHandler(db, new Audit(), new Current(ownerId, "VendorOwner")).Handle(command, default);
            Assert.Equal("Original published legacy combo", saved.Name); Assert.Equal("Pending", saved.LatestRevision!.Status);
            Assert.Equal(listingId, Assert.Single(saved.LatestRevision.Proposal!.Items).ListingId);
            Assert.Equal(initialFinancial, await StoredFinancialSnapshot());
            await new ReviewComboTemplateCommandHandler(db, new Audit(), new Current(moderatorId, "Moderator"))
                .Handle(new(templateId, true, RevisionId: saved.LatestRevision.Id), default);
            Assert.Equal(initialFinancial, await StoredFinancialSnapshot());
            await using var reload = new ApplicationDbContext(options);
            var revised = await reload.ComboTemplates.AsNoTracking().SingleAsync(item => item.Id == templateId);
            Assert.Equal("New proposed legacy combo", revised.Name); Assert.True(revised.IsActive); Assert.Equal(.05m, revised.SingleVendorDiscount);
            var revision = Assert.Single(await reload.ComboTemplateRevisions.AsNoTracking().ToListAsync()); Assert.Equal("Approved", revision.Status);
            Assert.Equal(ownerId, revision.CreatedById); Assert.Equal(moderatorId, revision.ReviewedById);

            async Task<string> StoredFinancialSnapshot()
            {
                await using var read = new ApplicationDbContext(options);
                var owner = await read.Users.AsNoTracking().SingleAsync(item => item.Id == ownerId);
                var customer = await read.Users.AsNoTracking().SingleAsync(item => item.Id == customerId);
                var vendor = await read.Vendors.AsNoTracking().SingleAsync(item => item.Id == vendorId);
                Assert.Equal("unchanged-combo-owner-hash", owner.PasswordHash); Assert.Equal(3, owner.SecurityVersion);
                Assert.Equal("unchanged-combo-customer-hash", customer.PasswordHash); Assert.Equal(2, customer.SecurityVersion);
                Assert.Equal(.08m, vendor.CommissionRate); Assert.Null(vendor.PartnershipPolicyAcceptedAt);
                Assert.Empty(await read.VendorBusinessGroups.Where(item => item.VendorId == vendorId).ToListAsync());
                var order = await read.ComboOrders.AsNoTracking().Include(item => item.Items).SingleAsync(item => item.Id == orderId);
                Assert.Equal(1000000m, order.Subtotal); Assert.Equal(.03m, order.DiscountPercent); Assert.Equal(30000m, order.DiscountAmount);
                Assert.Equal(970000m, order.Total); Assert.Equal(priceLock, order.PriceLockUntil); Assert.Equal(ComboOrderStatus.Completed, order.Status);
                var line = Assert.Single(order.Items); Assert.Equal(lineId, line.Id); Assert.Equal(1000000m, line.Price); Assert.Equal(30000m, line.DiscountShare);
                Assert.Equal(selection, line.SelectionJson); Assert.Equal(cancellation, line.CancellationPolicySnapshot); Assert.Equal(1, line.Quantity);
                Assert.Equal("Package", line.Unit); Assert.Equal(ComboItemStatus.Contracted, line.Status);
                var contract = await read.BookingContracts.AsNoTracking().SingleAsync(item => item.Id == contractId);
                Assert.Equal(.08m, contract.CommissionRateSnapshot); Assert.Equal(970000m, contract.ContractValue); Assert.Equal(200000m, contract.DepositAmount);
                Assert.Equal(confirmedAt, contract.ConfirmedAt); Assert.Equal(contractVersion, contract.Version); Assert.Equal(3, contract.RevisionCount);
                Assert.Equal(10000m, contract.AppointmentDepositCredit); Assert.Equal(cancellation, contract.CancellationPolicySnapshot);
                Assert.Equal(ContractStatus.Confirmed, contract.Status);
                var commission = await read.Commissions.AsNoTracking().SingleAsync(item => item.Id == commissionId);
                Assert.Equal(.08m, commission.CommissionRate); Assert.Equal(38800m, commission.CommissionAmount); Assert.Equal(38800m, commission.PaidAmount);
                Assert.Equal(commissionVersion, commission.Version); Assert.Equal(confirmedAt, commission.PaidAt); Assert.Equal(CommissionStatus.Paid, commission.Status);
                Assert.Equal("SYNTHETIC-LEGACY-COMBO-RECEIPT", commission.PaymentReferenceCode);
                return JsonSerializer.Serialize(new { owner.PasswordHash, owner.SecurityVersion, VendorRate = vendor.CommissionRate,
                    order.Subtotal, order.DiscountAmount, order.DiscountPercent, order.Total, order.PriceLockUntil,
                    line.Price, line.DiscountShare, line.SelectionJson, line.CancellationPolicySnapshot,
                    ContractRate = contract.CommissionRateSnapshot, contract.ContractValue, contract.DepositAmount, contract.ConfirmedAt, contract.Version,
                    CommissionVersion = commission.Version, commission.CommissionAmount, commission.PaidAmount, commission.PaidAt, commission.Status, commission.PaymentReferenceCode });
            }
        }
        finally
        {
            Assert.StartsWith("WeddingPlanTest_ComboLifecycle_", database);
            await db.Database.EnsureDeletedAsync();
        }
    }

    private sealed record Current(Guid? UserId, string? Role) : ICurrentUserService { public string? Email => null; }
    private sealed class Audit : IAuditLogger { public Task LogAsync(string action, string entityType, string? entityId, object? data = null) => Task.CompletedTask; }
    private sealed class Notify : INotificationSender { public Task SendAsync(string channel, string recipient, string message, CancellationToken cancellationToken = default) => Task.CompletedTask; }
    private sealed class FailingAfterSaveContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
        { await base.SaveChangesAsync(ct); throw new InvalidOperationException("Synthetic failure before combo transaction commit."); }
    }

    private sealed class Fixture(SqliteConnection connection, ApplicationDbContext db) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public DbContextOptions<ApplicationDbContext> Options => new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        public ApplicationDbContext OpenContext() => new(Options);
        public List<User> Users { get; } = [];
        public User Owner { get; private set; } = null!; public User OtherOwner { get; private set; } = null!;
        public User Customer { get; private set; } = null!; public User Moderator { get; private set; } = null!; public User Admin { get; private set; } = null!;
        public Vendor Vendor { get; private set; } = null!; public Vendor OtherVendor { get; private set; } = null!;
        public ServiceItem Venue { get; private set; } = null!; public ServiceItem Photo { get; private set; } = null!;
        public ServiceItem PhotoAlternative { get; private set; } = null!; public ServiceItem ForeignPhoto { get; private set; } = null!;
        public Current VendorActor => new(Owner.Id, "VendorOwner"); public Current OtherVendorActor => new(OtherOwner.Id, "VendorOwner");
        public Current CustomerActor => new(Customer.Id, "Customer"); public Current ModeratorActor => new(Moderator.Id, "Moderator"); public Current AdminActor => new(Admin.Id, "SuperAdmin");
        public DateTime Date => DateTime.UtcNow.AddDays(30);
        public List<ComboSelection> Selections => [new(Venue.Id, null, null), new(Photo.Id, null, null)];
        public ComboTemplateItemDto Slot(ServiceItem item) => new(item.CategoryId, item.ListingId, false, item.Id, []);
        public SaveComboTemplateCommand Command => new(null, "Synthetic combo", "Popular", "Modern", "Synthetic description",
            "https://images.example.invalid/cover.jpg", true, 2, [Slot(Venue), Slot(Photo)], "HCM", .03m,
            Content: new(["https://images.example.invalid/gallery-one.jpg", "https://images.example.invalid/gallery-two.jpg"], "Synthetic terms"));
        public async Task<ComboTemplateDto> Save(SaveComboTemplateCommand command, Current? actor = null)
        {
            // This suite covers the retained dynamic revision workflow. New vendor packages are
            // fixed-only; represent an existing pre-upgrade template explicitly in the fixture.
            if (!command.Id.HasValue && command.PricingMode == "Dynamic")
            {
                var legacy = ComboTemplate.Create(command.Name, command.Tier, command.Style, command.Description, command.ImageUrl, command.SortOrder);
                legacy.StageUnpublished(Vendor.Id, "Draft"); Db.ComboTemplates.Add(legacy); await Db.SaveChangesAsync();
                command = command with { Id = legacy.Id };
            }
            return await new SaveComboTemplateCommandHandler(Db, new Audit(), actor ?? VendorActor).Handle(command, default);
        }
        public Task<ComboTemplateDto> Preview(Guid id, Guid? revisionId = null, Current? actor = null) =>
            new GetComboTemplatePreviewQueryHandler(Db, actor ?? VendorActor).Handle(new(id, revisionId), default);
        public async Task<ComboTemplateDto> Own(Guid id) => (await new GetAllComboTemplatesQueryHandler(Db, VendorActor).Handle(new(true), default)).Single(item => item.Id == id);
        public async Task Review(Guid id, bool approve, string? note = null, Guid? RevisionId = null, Current? actor = null)
        {
            var author = await Db.ComboTemplateRevisions.Where(revision => revision.ComboTemplateId == id).OrderByDescending(revision => revision.Sequence)
                .Select(revision => (Guid?)revision.CreatedById).FirstOrDefaultAsync();
            await new ReviewComboTemplateCommandHandler(Db, new Audit(), actor ?? (author == Moderator.Id ? AdminActor : ModeratorActor)).Handle(new(id, approve, note, RevisionId), default);
        }
        public async Task Submit(Guid id, Current? actor = null) => await new SubmitComboTemplateCommandHandler(Db, actor ?? VendorActor, new Audit()).Handle(new(id), default);
        public async Task Activity(Guid id, bool active, Current? actor = null) => await new SetComboTemplateActivityCommandHandler(Db, actor ?? VendorActor, new Audit()).Handle(new(id, active), default);
        public Task<ComboBuilderDto> Builder() => new GetComboBuilderQueryHandler(Db).Handle(new(null), default);
        public async Task<ComboTemplateDto> Publish(SaveComboTemplateCommand? command = null)
        { var saved = await Save(command ?? Command); await Review(saved.Id, true, RevisionId: saved.LatestRevision!.Id); return await Own(saved.Id); }
        public async Task AssertNotPublic(Guid id)
        {
            Assert.DoesNotContain((await Builder()).Templates, item => item.Id == id);
            var catalog = await new GetComboCatalogPageQueryHandler(Db).Handle(new(TemplateId: id), default);
            Assert.Null(catalog.SelectedTemplate); Assert.DoesNotContain(catalog.Builder.Templates, item => item.Id == id);
            await Assert.ThrowsAsync<DomainException>(() => ComboPricing.CalculateAsync(Db, new(id, "HCM", Date, 100, Selections), Customer.Id, default));
        }
        public async Task<string> PublishedSnapshot(Guid id)
        {
            await using var reload = OpenContext(); var template = await reload.ComboTemplates.AsNoTracking().Include(item => item.Items).SingleAsync(item => item.Id == id);
            return JsonSerializer.Serialize(new { template.Name, template.Tier, template.Style, template.Description, template.ImageUrl,
                template.IsActive, template.SortOrder, template.OwnerVendorId, template.SingleVendorDiscount, template.ModerationStatus,
                template.Region, template.ContentJson, template.ValidFromUtc, template.ValidUntilUtc,
                Items = template.Items.OrderBy(item => item.Id).Select(item => new { item.Id, item.CategoryId, item.ListingId, item.ServiceItemId, item.IsOptional, item.AllowedItemIdsJson }) });
        }
        public async Task<string> TemplateSnapshot(Guid id)
        {
            await using var reload = OpenContext();
            var revisions = await reload.ComboTemplateRevisions.AsNoTracking().Where(item => item.ComboTemplateId == id).OrderBy(item => item.Sequence).ToListAsync();
            return JsonSerializer.Serialize(new { Published = await PublishedSnapshot(id), Revisions = revisions.Select(item => new {
                item.Id, item.Sequence, item.SnapshotJson, item.Status, item.CreatedById, item.CreatedByRole, item.CreatedAt,
                item.SubmittedAt, item.ReviewedById, item.ReviewedByRole, item.ReviewedAt, item.ReviewNote, item.SupersededAt, item.SupersededFromStatus }) });
        }
        public async Task<string> FinancialSnapshot()
        {
            await using var reload = OpenContext();
            var orders = await reload.ComboOrders.AsNoTracking().Include(item => item.Items).OrderBy(item => item.Id).ToListAsync();
            var contracts = await reload.BookingContracts.AsNoTracking().OrderBy(item => item.Id).ToListAsync();
            var commissions = await reload.Commissions.AsNoTracking().OrderBy(item => item.Id).ToListAsync();
            return JsonSerializer.Serialize(new { Orders = orders.Select(item => new { item.Id, item.Subtotal, item.DiscountPercent,
                item.DiscountAmount, item.Total, item.PriceLockUntil, Items = item.Items.OrderBy(line => line.Id).Select(line => new {
                    line.Id, line.Price, line.DiscountShare, line.CancellationPolicySnapshot, line.Quantity, line.Unit, line.SelectionJson }) }),
                Contracts = contracts.Select(item => new { item.Id, item.ContractValue, item.DepositAmount, item.CommissionRateSnapshot, item.ConfirmedAt, item.Version }),
                Commissions = commissions.Select(item => new { item.Id, item.CommissionRate, item.CommissionAmount, item.PaidAmount, item.PaymentReferenceCode, item.Status, item.PaidAt, item.Version }) });
        }
        public static async Task<Fixture> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync(); var f = new Fixture(connection, db);
            User Add(string name, UserRole role) { var user = User.Create(name, $"{Guid.NewGuid():N}@combo-lifecycle.example.invalid", "0901234567", "synthetic-hash", role); f.Users.Add(user); db.Users.Add(user); return user; }
            f.Owner = Add("Synthetic owner", UserRole.VendorOwner); f.OtherOwner = Add("Other synthetic owner", UserRole.VendorOwner);
            f.Customer = Add("Synthetic couple", UserRole.Customer); f.Moderator = Add("Synthetic moderator", UserRole.Moderator); f.Admin = Add("Synthetic admin", UserRole.SuperAdmin);
            Add("Synthetic finance", UserRole.Finance); Add("Synthetic care", UserRole.CustomerCare);
            f.Vendor = Vendor.Create(f.Owner.Id, "Synthetic vendor", "synthetic-combo-lifecycle", "Owner", "0901234567", "HCM", .08m);
            f.OtherVendor = Vendor.Create(f.OtherOwner.Id, "Other synthetic vendor", "other-combo-lifecycle", "Other", "0901234568", "HCM", .08m);
            foreach (var vendor in new[] { f.Vendor, f.OtherVendor }) { vendor.SetVerified(true); vendor.ConfigureComboParticipation(true, .10m); db.Vendors.Add(vendor); }
            var venueCategory = await db.Categories.SingleAsync(item => item.Slug == "tiem-cuoi-venues"); var photoCategory = await db.Categories.SingleAsync(item => item.Slug == "quay-chup-photo-video");
            ServiceItem Item(Vendor vendor, Category category, string name, decimal price)
            {
                var listing = Listing.Create(vendor.Id, category.Id, name, "synthetic-" + Guid.NewGuid().ToString("N"), price, price, "Synthetic listing", "HCM"); listing.Approve(); db.Listings.Add(listing);
                var item = ServiceItem.Create(vendor.Id, listing.Id, category.Id, name, price, "Package", 1, 1000, "HCM", "Original service cancellation terms");
                item.SetDailyCapacity(10); item.Submit(); item.Review(true, null); db.ServiceItems.Add(item); return item;
            }
            f.Venue = Item(f.Vendor, venueCategory, "Synthetic venue", 1000000); f.Photo = Item(f.Vendor, photoCategory, "Synthetic photo", 2000000);
            f.PhotoAlternative = Item(f.Vendor, photoCategory, "Synthetic alternative photo", 2500000); f.ForeignPhoto = Item(f.OtherVendor, photoCategory, "Synthetic foreign photo", 3000000);
            await db.SaveChangesAsync(); return f;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
