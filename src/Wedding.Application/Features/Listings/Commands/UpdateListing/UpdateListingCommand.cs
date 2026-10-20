using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Listings.Commands.UpdateListing;

public record UpdateListingCommand(
    Guid ListingId,
    string Title,
    decimal MinPrice,
    decimal MaxPrice,
    string Description,
    string Location,
    List<string>? ImageUrls = null,
    ServiceDetailsDto? ServiceDetails = null,
    ServicePricingDto? Pricing = null
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
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Location).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ImageUrls).Must(urls => urls == null || urls.Count is >= 1 and <= 10)
            .WithMessage("Cần từ 1 đến 10 hình ảnh khi thay album.");
        RuleForEach(x => x.ImageUrls).Must(ListingImageReferences.IsValid)
            .WithMessage("Ảnh cần đường dẫn HTTP(S) hoặc ảnh dịch vụ đã tải lên hợp lệ, tối đa 500 ký tự.");
        RuleFor(x => x.ServiceDetails).Custom((details, validation) =>
        {
            try { ListingServiceDetails.NormalizeAndSerialize(details); }
            catch (DomainException error) { validation.AddFailure(error.Message); }
        });
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

    public Task Handle(UpdateListingCommand request, CancellationToken cancellationToken) => _context.ExecuteSerializableAsync(async ct =>
    {
        var validation = new UpdateListingCommandValidator().Validate(request);
        if (!validation.IsValid) throw new DomainException(validation.Errors[0].ErrorMessage);
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Không xác định được người dùng.");
        if (_currentUserService.Role != nameof(UserRole.VendorOwner))
            throw new DomainException("Chỉ nhà cung cấp được chỉnh sửa dịch vụ của mình.");

        var vendor = await _context.Vendors
            .FirstOrDefaultAsync(v => v.UserId == userId && v.IsActive && v.User.IsActive && v.User.Role == UserRole.VendorOwner, ct)
            ?? throw new DomainException("Hồ sơ nhà cung cấp không tồn tại.");

        var listing = await _context.Listings
            .Include(l => l.Media)
            .FirstOrDefaultAsync(l => l.Id == request.ListingId && l.VendorId == vendor.Id, ct)
            ?? throw new NotFoundException("Bài đăng", request.ListingId);

        // Published content stays intact until a distinct proposal is reviewed.
        if (listing.Status is ListingStatus.Active or ListingStatus.Hidden)
        {
            var latest = await _context.ListingRevisions.Where(x => x.ListingId == listing.Id)
                .OrderByDescending(x => x.Sequence).FirstOrDefaultAsync(ct);
            var previous = latest?.Status is "Draft" or "Pending" or "Rejected" ? ListingPublication.Read(latest) : null;
            var pricing = await ListingServicePricing.Prepare(_context, listing, request.Pricing, ct, previous?.Pricing);
            await ListingPublication.Append(_context, listing, userId, ListingPublication.Capture(listing, request, previous) with { Pricing = pricing }, false, ct);
            await _context.SaveChangesAsync(ct);
            return Unit.Value;
        }

        // Unpublished drafts/rejections retain the compatible direct-edit workflow.
        if (listing.Status != Domain.Enums.ListingStatus.Draft && listing.Status != Domain.Enums.ListingStatus.Rejected)
            throw new DomainException("Chỉ có thể sửa bài đăng ở trạng thái Nháp hoặc Bị từ chối.");

        // Null/omitted keeps existing media for older clients. A supplied list replaces the complete album.
        var imageUrls = request.ImageUrls == null ? null : ListingImageReferences.NormalizeRequired(request.ImageUrls);
        var serviceDetailsJson = ListingServiceDetails.NormalizeAndSerialize(request.ServiceDetails);
        var servicePricing = await ListingServicePricing.Prepare(_context, listing, request.Pricing, ct);
        listing.Update(request.Title, request.MinPrice, request.MaxPrice, request.Description, request.Location);
        if (servicePricing != null) await ListingServicePricing.Apply(_context, listing, servicePricing, ct);
        // Omitted/null preserves existing metadata for older or price-only clients. A supplied object replaces it completely.
        if (request.ServiceDetails != null) listing.SetServiceDetailsJson(serviceDetailsJson);
        if (imageUrls != null)
        {
            var currentMedia = await _context.ListingMedias.Where(media => media.ListingId == listing.Id).ToListAsync(ct);
            _context.ListingMedias.RemoveRange(currentMedia);
            for (var index = 0; index < imageUrls.Count; index++)
                _context.ListingMedias.Add(ListingMedia.Create(listing.Id, imageUrls[index], null, displayOrder: index, isFeatured: index == 0));
        }
        await _context.SaveChangesAsync(ct);
        return Unit.Value;
    }, cancellationToken);
}
