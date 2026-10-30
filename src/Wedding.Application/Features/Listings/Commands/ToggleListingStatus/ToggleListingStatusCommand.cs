using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Listings.Commands.ToggleListingStatus;

public record ToggleListingStatusCommand(Guid ListingId, ListingStatus NewStatus) : IRequest;
public class ToggleListingStatusCommandHandler(IApplicationDbContext db, ICurrentUserService actor) : IRequestHandler<ToggleListingStatusCommand>
{
    public Task Handle(ToggleListingStatusCommand request, CancellationToken ct) => db.ExecuteSerializableAsync(async token =>
    {
        var staff = await ListingPublication.RequireStaff(db, actor, token);
        var listing = await db.Listings.SingleOrDefaultAsync(x => x.Id == request.ListingId, token) ?? throw new NotFoundException("Dịch vụ không tồn tại.");
        if (listing.Status is not (ListingStatus.Active or ListingStatus.Hidden) || request.NewStatus is not (ListingStatus.Active or ListingStatus.Hidden))
            throw new DomainException("Ẩn/hiện kiểm duyệt chỉ áp dụng cho bài đã xuất bản. Bản nháp phải qua hàng chờ và được duyệt.");
        listing.SetStatus(request.NewStatus);
        db.AuditLogs.Add(AuditLog.Create(staff.Id, actor.Role, "Listing.ModerationVisibility", nameof(Listing), listing.Id.ToString(), request.NewStatus.ToString()));
        await db.SaveChangesAsync(token); return Unit.Value;
    }, ct);
}
