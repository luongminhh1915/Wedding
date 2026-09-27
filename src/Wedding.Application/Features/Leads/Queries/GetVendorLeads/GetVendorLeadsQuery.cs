using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Leads.DTOs;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Leads.Queries.GetVendorLeads;

public record GetVendorLeadsQuery(
    string? Status = null,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<List<LeadDto>>;

public class GetVendorLeadsQueryHandler : IRequestHandler<GetVendorLeadsQuery, List<LeadDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetVendorLeadsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<LeadDto>> Handle(GetVendorLeadsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để xem hộp thư Lead.");

        var vendor = await _context.Vendors
            .FirstOrDefaultAsync(v => v.UserId == userId, cancellationToken)
            ?? throw new DomainException("Hồ sơ nhà cung cấp không tồn tại.");

        var query = _context.Leads
            .Include(l => l.Customer)
            .Include(l => l.Listing)
            .Include(l => l.Voucher)
            .Where(l => l.VendorId == vendor.Id)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<Domain.Enums.LeadStatus>(request.Status, true, out var leadStatus))
        {
            query = query.Where(l => l.Status == leadStatus);
        }

        var leads = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return leads.Select(l => new LeadDto(
            Id: l.Id,
            CustomerId: l.CustomerId,
            CustomerName: l.Customer.FullName,
            VendorId: l.VendorId,
            VendorBrandName: vendor.BrandName,
            ListingId: l.ListingId,
            ListingTitle: l.Listing?.Title,
            // BR-002 (Smart Privacy): Nếu khách chưa mở khóa số, Vendor chỉ nhìn thấy SĐT đã che ***
            PhoneNumber: l.IsPhoneUnlocked ? l.RawPhoneNumber : l.MaskedPhoneNumber,
            IsPhoneUnlocked: l.IsPhoneUnlocked,
            WeddingDate: l.WeddingDate,
            EstimatedGuests: l.EstimatedGuests,
            EstimatedBudget: l.EstimatedBudget,
            Notes: l.Notes,
            Status: l.Status.ToString(),
            SlaDeadline: l.SlaDeadline,
            AcceptedAt: l.AcceptedAt,
            CreatedAt: l.CreatedAt,
            Voucher: l.Voucher != null ? new VoucherSummaryDto(
                Id: l.Voucher.Id,
                Code: l.Voucher.Code,
                DiscountValue: l.Voucher.DiscountValue,
                DiscountPercent: l.Voucher.DiscountPercent,
                Status: l.Voucher.Status.ToString(),
                ExpiresAt: l.Voucher.ExpiresAt
            ) : null
        )).ToList();
    }
}
