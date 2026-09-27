using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Listings.Commands.UpdateListing;

public record UpdateListingCommand(
    Guid ListingId,
    string Title,
    decimal MinPrice,
    decimal MaxPrice,
    string Description,
    string Location
) : IRequest;

public class UpdateListingCommandValidator : AbstractValidator<UpdateListingCommand>
{
    public UpdateListingCommandValidator()
    {
        RuleFor(x => x.ListingId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().WithMessage("Tiêu đề không được để trống.").MaximumLength(200);
        RuleFor(x => x.MinPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxPrice).GreaterThanOrEqualTo(x => x.MinPrice)
            .WithMessage("Giá tối đa phải lớn hơn hoặc bằng giá tối thiểu.");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(5000);
        RuleFor(x => x.Location).NotEmpty().MaximumLength(300);
    }
}

public class UpdateListingCommandHandler : IRequestHandler<UpdateListingCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UpdateListingCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task Handle(UpdateListingCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Không xác định được người dùng.");

        var vendor = await _context.Vendors
            .FirstOrDefaultAsync(v => v.UserId == userId, cancellationToken)
            ?? throw new DomainException("Hồ sơ nhà cung cấp không tồn tại.");

        var listing = await _context.Listings
            .FirstOrDefaultAsync(l => l.Id == request.ListingId && l.VendorId == vendor.Id, cancellationToken)
            ?? throw new NotFoundException("Bài đăng", request.ListingId);

        // Chỉ cho phép chỉnh sửa khi đang ở trạng thái Draft hoặc Rejected
        if (listing.Status != Domain.Enums.ListingStatus.Draft && listing.Status != Domain.Enums.ListingStatus.Rejected)
            throw new DomainException("Chỉ có thể sửa bài đăng ở trạng thái Nháp hoặc Bị từ chối.");

        listing.Update(request.Title, request.MinPrice, request.MaxPrice, request.Description, request.Location);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
