using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Listings.Commands.CreateListing;

public record CreateListingCommand(
    Guid CategoryId,
    string Title,
    decimal MinPrice,
    decimal MaxPrice,
    string Description,
    string Location,
    List<string>? ImageUrls = null,
    ServiceDetailsDto? ServiceDetails = null,
    ServicePricingDto? Pricing = null
) : IRequest<Guid>;

public class CreateListingCommandValidator : AbstractValidator<CreateListingCommand>
{
    public CreateListingCommandValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("Ngành hàng không được để trống.");
        RuleFor(x => x.Title).NotEmpty().WithMessage("Tiêu đề bài đăng không được để trống.").MaximumLength(200);
        RuleFor(x => x.MinPrice).GreaterThanOrEqualTo(0).WithMessage("Giá tối thiểu không hợp lệ.");
        RuleFor(x => x.MaxPrice).GreaterThanOrEqualTo(x => x.MinPrice)
            .WithMessage("Giá tối đa phải lớn hơn hoặc bằng giá tối thiểu.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("Mô tả không được để trống.").MaximumLength(4000);
        RuleFor(x => x.Location).NotEmpty().WithMessage("Địa điểm không được để trống.").MaximumLength(200);
        RuleFor(x => x.ImageUrls)
            .NotNull().WithMessage("Bắt buộc phải có ít nhất 1 hình ảnh dịch vụ.")
            .Must(x => x != null && x.Count >= 1).WithMessage("Bắt buộc phải có ít nhất 1 hình ảnh dịch vụ.")
            .Must(x => x != null && x.Count <= 10).WithMessage("Số lượng ảnh tối đa là 10 bức ảnh.");
        RuleForEach(x => x.ImageUrls).Must(ListingImageReferences.IsValid)
            .WithMessage("Ảnh cần đường dẫn HTTP(S) hoặc ảnh dịch vụ đã tải lên hợp lệ, tối đa 500 ký tự.");
        RuleFor(x => x.ServiceDetails).Custom((details, validation) =>
        {
            try { ListingServiceDetails.NormalizeAndSerialize(details); }
            catch (DomainException error) { validation.AddFailure(error.Message); }
        });
    }
}

public class CreateListingCommandHandler : IRequestHandler<CreateListingCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CreateListingCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public Task<Guid> Handle(CreateListingCommand request, CancellationToken cancellationToken) => _context.ExecuteSerializableAsync(async ct =>
    {
        var validation = new CreateListingCommandValidator().Validate(request);
        if (!validation.IsValid) throw new DomainException(validation.Errors[0].ErrorMessage);
        var imageUrls = ListingImageReferences.NormalizeRequired(request.ImageUrls);
        var serviceDetailsJson = ListingServiceDetails.NormalizeAndSerialize(request.ServiceDetails);
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Không xác định được người dùng.");
        if (_currentUserService.Role != nameof(UserRole.VendorOwner))
            throw new DomainException("Chỉ nhà cung cấp được tạo dịch vụ của mình.");

        var vendor = await _context.Vendors
            .FirstOrDefaultAsync(v => v.UserId == userId && v.IsActive && v.User.IsActive && v.User.Role == UserRole.VendorOwner, ct)
            ?? throw new DomainException("Hồ sơ nhà cung cấp không tồn tại.");

        await ListingCategoryAccess.RequireForCreateAsync(_context, vendor.Id, request.CategoryId, ct);

        // Sinh slug tự động từ tiêu đề
        var baseSlug = GenerateSlug(request.Title);
        var slug = baseSlug;
        var counter = 1;
        while (await _context.Listings.AnyAsync(l => l.Slug == slug, ct))
        {
            slug = $"{baseSlug}-{counter++}";
        }

        var listing = Listing.Create(
            vendor.Id,
            request.CategoryId,
            request.Title,
            slug,
            request.MinPrice,
            request.MaxPrice,
            request.Description,
            request.Location
        );
        if (request.ServiceDetails != null) listing.SetServiceDetailsJson(serviceDetailsJson);

        if (request.Pricing != null)
        {
            var pricing = await ListingServicePricing.Prepare(_context, listing, request.Pricing, ct);
            await ListingServicePricing.Apply(_context, listing, pricing!, ct);
        }

        _context.Listings.Add(listing);

        if (imageUrls.Count > 0)
        {
            for (int i = 0; i < imageUrls.Count; i++)
            {
                var media = ListingMedia.Create(
                    listing.Id,
                    imageUrls[i],
                    thumbnailUrl: null,
                    mediaType: "image",
                    displayOrder: i,
                    isFeatured: i == 0
                );
                _context.ListingMedias.Add(media);
            }
        }

        await _context.SaveChangesAsync(ct);

        return listing.Id;
    }, cancellationToken);

    private static string GenerateSlug(string phrase)
    {
        string str = phrase.ToLowerInvariant().Trim();
        str = System.Text.RegularExpressions.Regex.Replace(str, @"\s+", "-");
        str = System.Text.RegularExpressions.Regex.Replace(str, @"[^a-z0-9\-]", "");
        return str.Trim('-');
    }
}
