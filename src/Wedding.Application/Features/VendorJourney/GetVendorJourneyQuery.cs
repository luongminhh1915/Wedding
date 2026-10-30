using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.VendorJourney;

/// <summary>
/// Read-only shop milestones, not permission to publish or confirmation that money has moved.
/// Approved service/combo counts describe moderation only; availability has additional guards.
/// </summary>
public record VendorJourneySummaryDto(string VendorName, bool IsVerified,
    string? LatestVerificationStatus, string? TaxProfileStatus, bool PolicyAccepted, bool AllowInCombos,
    int ListingCount, int PublicListingCount, int PendingListingCount, int RejectedListingCount,
    int ServiceItemCount, int ApprovedServiceItemCount, int ComboCount, int ApprovedComboCount,
    int PendingComboCount, int NewLeadCount, int ActiveLeadCount, int QuoteCount, int ContractCount,
    int PendingOrderChangeCount, int UpcomingAppointmentCount);

public record GetVendorJourneyQuery : IRequest<VendorJourneySummaryDto>;

public class GetVendorJourneyQueryHandler(IApplicationDbContext db, ICurrentUserService actor)
    : IRequestHandler<GetVendorJourneyQuery, VendorJourneySummaryDto>
{
    public async Task<VendorJourneySummaryDto> Handle(GetVendorJourneyQuery request, CancellationToken ct)
    {
        if (!actor.UserId.HasValue || actor.UserId == Guid.Empty || actor.Role != nameof(UserRole.VendorOwner))
            throw new DomainException("Chỉ nhà cung cấp được xem hành trình của shop mình.");

        var now = DateTime.UtcNow;
        // One bounded SQL projection. Never materialize customer details, documents, contracts or child collections.
        // Ownership and the current persisted actor/shop status constrain the entire projection, not just the UI.
        var result = await db.Vendors.AsNoTracking()
            .Where(vendor => vendor.UserId == actor.UserId && vendor.IsActive && vendor.User.IsActive &&
                vendor.User.Role == UserRole.VendorOwner)
            .Select(vendor => new
            {
                VendorName = vendor.BrandName,
                vendor.IsVerified,
                LatestVerificationStatus = db.VendorVerifications.Where(item => item.VendorId == vendor.Id)
                    .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
                    .Select(item => (VerificationStatus?)item.Status).FirstOrDefault(),
                TaxProfileStatus = db.VendorTaxProfiles.Where(item => item.VendorId == vendor.Id)
                    .Select(item => (VerificationStatus?)item.Status).FirstOrDefault(),
                PolicyAccepted = vendor.PartnershipPolicyAcceptedAt != null &&
                    vendor.PartnershipPolicyVersion != null && vendor.PartnershipPolicyVersion.Trim() != "",
                vendor.AllowInCombos,
                ListingCount = db.Listings.Count(item => item.VendorId == vendor.Id),
                PublicListingCount = db.Listings.Count(item => item.VendorId == vendor.Id &&
                    item.Status == ListingStatus.Active && item.IsActive),
                // Count listings, not revision rows. A rejected old version stops being an action item
                // after a newer draft/submission; a live baseline may still have a pending proposal.
                PendingListingCount = db.Listings.Count(item => item.VendorId == vendor.Id &&
                    ((db.ListingRevisions.Where(revision => revision.ListingId == item.Id)
                        .OrderByDescending(revision => revision.Sequence).Select(revision => revision.Status).FirstOrDefault()
                        ?? (item.Status == ListingStatus.PendingApproval ? "Pending" : "")) == "Pending")),
                RejectedListingCount = db.Listings.Count(item => item.VendorId == vendor.Id &&
                    ((db.ListingRevisions.Where(revision => revision.ListingId == item.Id)
                        .OrderByDescending(revision => revision.Sequence).Select(revision => revision.Status).FirstOrDefault()
                        ?? (item.Status == ListingStatus.Rejected ? "Rejected" : "")) == "Rejected")),
                ServiceItemCount = db.ServiceItems.Count(item => item.VendorId == vendor.Id),
                ApprovedServiceItemCount = db.ServiceItems.Count(item => item.VendorId == vendor.Id && item.ModerationStatus == "Approved"),
                ComboCount = db.ComboTemplates.Count(item => item.OwnerVendorId == vendor.Id),
                ApprovedComboCount = db.ComboTemplates.Count(item => item.OwnerVendorId == vendor.Id && item.ModerationStatus == "Approved"),
                PendingComboCount = db.ComboTemplates.Count(item => item.OwnerVendorId == vendor.Id &&
                    ((db.ComboTemplateRevisions.Where(revision => revision.ComboTemplateId == item.Id)
                        .OrderByDescending(revision => revision.Sequence).Select(revision => revision.Status).FirstOrDefault()
                        ?? item.ModerationStatus) == "Pending")),
                NewLeadCount = db.Leads.Count(item => item.VendorId == vendor.Id && item.Status == LeadStatus.New),
                ActiveLeadCount = db.Leads.Count(item => item.VendorId == vendor.Id &&
                    (item.Status == LeadStatus.Accepted || item.Status == LeadStatus.Contacted)),
                QuoteCount = db.Quotes.Count(item => item.VendorId == vendor.Id),
                ContractCount = db.BookingContracts.Count(item => item.VendorId == vendor.Id),
                PendingOrderChangeCount = db.OrderChangeRequests.Count(item => item.VendorId == vendor.Id && item.Status == "PendingVendor"),
                UpcomingAppointmentCount = db.Appointments.Count(item => item.VendorId == vendor.Id && item.StartAt >= now &&
                    (item.Status == AppointmentStatus.Pending || item.Status == AppointmentStatus.AwaitingDeposit ||
                     item.Status == AppointmentStatus.RescheduleProposed || item.Status == AppointmentStatus.Confirmed ||
                     item.Status == AppointmentStatus.CheckedIn))
            }).SingleOrDefaultAsync(ct)
            ?? throw new DomainException("Hồ sơ nhà cung cấp đang hoạt động không tồn tại.");

        return new(result.VendorName, result.IsVerified, result.LatestVerificationStatus?.ToString(),
            result.TaxProfileStatus?.ToString(), result.PolicyAccepted, result.AllowInCombos,
            result.ListingCount, result.PublicListingCount, result.PendingListingCount, result.RejectedListingCount,
            result.ServiceItemCount, result.ApprovedServiceItemCount, result.ComboCount, result.ApprovedComboCount,
            result.PendingComboCount, result.NewLeadCount, result.ActiveLeadCount, result.QuoteCount,
            result.ContractCount, result.PendingOrderChangeCount, result.UpcomingAppointmentCount);
    }
}
