using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Listings.Commands.CreateListing;

public record CreateListingCommand(
    Guid CategoryId,
    string Title,
    decimal MinPrice,
    decimal MaxPrice,
    string Description,
    string Location,
    List<string>? ImageUrls = null
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
        RuleFor(x => x.Description).NotEmpty().WithMessage("Mô tả không được để trống.").MaximumLength(5000);
        RuleFor(x => x.Location).NotEmpty().WithMessage("Địa điểm không được để trống.").MaximumLength(300);
        RuleFor(x => x.ImageUrls)
            .NotNull().WithMessage("Bắt buộc phải có ít nhất 1 hình ảnh dịch vụ.")
            .Must(x => x != null && x.Count >= 1).WithMessage("Bắt buộc phải có ít nhất 1 hình ảnh dịch vụ.")
            .Must(x => x != null && x.Count <= 10).WithMessage("Số lượng ảnh tối đa là 10 bức ảnh.");
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

    public async Task<Guid> Handle(CreateListingCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Không xác định được người dùng.");

        var vendor = await _context.Vendors
            .FirstOrDefaultAsync(v => v.UserId == userId, cancellationToken)
            ?? throw new DomainException("Hồ sơ nhà cung cấp không tồn tại.");

        var categoryExists = await _context.Categories
            .AnyAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (!categoryExists)
            throw new DomainException("Ngành hàng được chọn không tồn tại.");

        // Sinh slug tự động từ tiêu đề
        var baseSlug = GenerateSlug(request.Title);
        var slug = baseSlug;
        var counter = 1;
        while (await _context.Listings.AnyAsync(l => l.Slug == slug, cancellationToken))
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

        _context.Listings.Add(listing);

        if (request.ImageUrls != null && request.ImageUrls.Count > 0)
        {
            for (int i = 0; i < request.ImageUrls.Count; i++)
            {
                var media = ListingMedia.Create(
                    listing.Id,
                    request.ImageUrls[i],
                    thumbnailUrl: null,
                    mediaType: "image",
                    displayOrder: i,
                    isFeatured: i == 0
                );
                _context.ListingMedias.Add(media);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return listing.Id;
    }

    private static string GenerateSlug(string phrase)
    {
        string str = phrase.ToLowerInvariant().Trim();
        str = System.Text.RegularExpressions.Regex.Replace(str, @"\s+", "-");
        str = System.Text.RegularExpressions.Regex.Replace(str, @"[^a-z0-9\-]", "");
        return str.Trim('-');
    }
}
