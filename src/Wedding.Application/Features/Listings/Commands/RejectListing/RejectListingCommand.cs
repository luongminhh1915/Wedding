using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Listings.Commands.RejectListing;

public record RejectListingCommand(Guid ListingId, string Reason, Guid? RevisionId = null) : IRequest;
public class RejectListingCommandValidator : AbstractValidator<RejectListingCommand>
{
    public RejectListingCommandValidator() { RuleFor(x => x.ListingId).NotEmpty(); RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000); }
}
public class RejectListingCommandHandler(IApplicationDbContext db, ICurrentUserService actor) : IRequestHandler<RejectListingCommand>
{
    public Task Handle(RejectListingCommand request, CancellationToken ct) => db.ExecuteSerializableAsync(async token =>
    {
        var staff = await ListingPublication.RequireStaff(db, actor, token);
        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length > 1000) throw new DomainException("Lý do từ chối cần từ 1 đến 1000 ký tự.");
        var listing = await db.Listings.SingleOrDefaultAsync(x => x.Id == request.ListingId, token) ?? throw new NotFoundException("Dịch vụ không tồn tại.");
        var latest = await db.ListingRevisions.Where(x => x.ListingId == listing.Id).OrderByDescending(x => x.Sequence).FirstOrDefaultAsync(token);
        if (request.RevisionId.HasValue && latest?.Id != request.RevisionId) throw new DomainException("Phiên bản đã thay đổi. Tải lại hàng chờ trước khi từ chối.");
        if (latest != null) latest.Review(false, staff.Id, reason);
        else if (listing.Status != ListingStatus.PendingApproval) throw new DomainException("Dịch vụ không ở trạng thái chờ duyệt.");
        // Rejecting an edit never hides or rewrites the previously published version.
        if (listing.Status == ListingStatus.PendingApproval)
        {
            listing.Reject(reason);
            await ListingServicePricing.Synchronize(db, listing, "Rejected", token, reason);
        }
        var vendorUserId = await db.Vendors.Where(x => x.Id == listing.VendorId).Select(x => x.UserId).SingleAsync(token);
        db.Notifications.Add(Notification.Create(vendorUserId, "InApp", "", $"Dịch vụ {listing.Title} cần chỉnh sửa: {reason}", $"listing-review:{latest?.Id ?? listing.Id}:rejected"));
        db.AuditLogs.Add(AuditLog.Create(staff.Id, actor.Role, "Listing.Reject", nameof(Listing), listing.Id.ToString(), reason));
        await db.SaveChangesAsync(token); return Unit.Value;
    }, ct);
}
