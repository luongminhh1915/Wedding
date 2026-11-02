using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Listings.Commands.SubmitForApproval;

public record SubmitForApprovalCommand(Guid ListingId) : IRequest;
public class SubmitForApprovalCommandHandler(IApplicationDbContext db, ICurrentUserService actor) : IRequestHandler<SubmitForApprovalCommand>
{
    public Task Handle(SubmitForApprovalCommand request, CancellationToken ct) => db.ExecuteSerializableAsync(async token =>
    {
        var vendor = await ListingPublication.RequireOwner(db, actor, token);
        var listing = await db.Listings.Include(x => x.Media).SingleOrDefaultAsync(x => x.Id == request.ListingId && x.VendorId == vendor.Id, token)
            ?? throw new NotFoundException("Dịch vụ không tồn tại.");
        await ListingPublication.CheckSubmission(db, listing, token);
        var latest = await db.ListingRevisions.Where(x => x.ListingId == listing.Id).OrderByDescending(x => x.Sequence).FirstOrDefaultAsync(token);
        if (listing.Status is ListingStatus.Active or ListingStatus.Hidden)
        {
            if (latest?.Status is not ("Draft" or "Rejected")) throw new DomainException("Chỉ gửi duyệt bản sửa nháp hoặc bị từ chối. Bản đang công khai được giữ nguyên.");
            await ListingPublication.Append(db, listing, actor.UserId!.Value, ListingPublication.Read(latest), true, token);
        }
        else
        {
            if (listing.Status is not (ListingStatus.Draft or ListingStatus.Rejected)) throw new DomainException("Dịch vụ đã được gửi duyệt.");
            var content = ListingPublication.Capture(listing) with { Pricing = await ListingServicePricing.Current(db, listing, token) };
            await ListingPublication.Append(db, listing, actor.UserId!.Value, content, true, token);
            listing.SubmitForApproval();
            await ListingServicePricing.Synchronize(db, listing, "Pending", token);
        }
        await db.SaveChangesAsync(token); return Unit.Value;
    }, ct);
}
