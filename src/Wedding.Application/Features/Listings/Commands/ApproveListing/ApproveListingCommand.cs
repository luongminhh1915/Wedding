using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Listings.Commands.ApproveListing;

public record ApproveListingCommand(Guid ListingId, Guid? RevisionId = null) : IRequest;
public class ApproveListingCommandHandler(IApplicationDbContext db, ICurrentUserService actor) : IRequestHandler<ApproveListingCommand>
{
    public Task Handle(ApproveListingCommand request, CancellationToken ct) => db.ExecuteSerializableAsync(async token =>
    {
        var staff = await ListingPublication.RequireStaff(db, actor, token);
        var listing = await db.Listings.SingleOrDefaultAsync(x => x.Id == request.ListingId, token) ?? throw new NotFoundException("Dịch vụ không tồn tại.");
        var latest = await db.ListingRevisions.Where(x => x.ListingId == listing.Id).OrderByDescending(x => x.Sequence).FirstOrDefaultAsync(token);
        if (request.RevisionId.HasValue && latest?.Id != request.RevisionId) throw new DomainException("Phiên bản đã thay đổi. Tải lại hàng chờ trước khi duyệt.");
        if (latest != null)
        {
            if (latest.Status != "Pending") throw new DomainException("Phiên bản không còn chờ duyệt.");
            await ListingPublication.CheckSubmission(db, listing, token);
            await ListingPublication.Apply(db, listing, latest, token);
            latest.Review(true, staff.Id, null);
        }
        else if (listing.Status != ListingStatus.PendingApproval) throw new DomainException("Dịch vụ không ở trạng thái chờ duyệt.");
        else await ListingPublication.CheckSubmission(db, listing, token);
        var moderationHidden = listing.Status == ListingStatus.Hidden;
        listing.Approve();
        await ListingServicePricing.Synchronize(db, listing, "Approved", token);
        if (moderationHidden) listing.SetStatus(ListingStatus.Hidden);
        var vendorUserId = await db.Vendors.Where(x => x.Id == listing.VendorId).Select(x => x.UserId).SingleAsync(token);
        db.Notifications.Add(Notification.Create(vendorUserId, "InApp", "", $"Dịch vụ {listing.Title} đã được phê duyệt. Trạng thái kinh doanh của bạn vẫn được giữ nguyên.", $"listing-review:{latest?.Id ?? listing.Id}:approved"));
        db.AuditLogs.Add(AuditLog.Create(staff.Id, actor.Role, "Listing.Approve", nameof(Listing), listing.Id.ToString(), latest?.Id.ToString()));
        await db.SaveChangesAsync(token); return Unit.Value;
    }, ct);
}
