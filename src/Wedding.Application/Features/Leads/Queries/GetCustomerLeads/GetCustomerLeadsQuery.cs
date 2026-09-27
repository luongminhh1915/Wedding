using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Leads.DTOs;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Leads.Queries.GetCustomerLeads;

public record GetCustomerLeadsQuery(
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<List<LeadDto>>;

public class GetCustomerLeadsQueryHandler : IRequestHandler<GetCustomerLeadsQuery, List<LeadDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetCustomerLeadsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<LeadDto>> Handle(GetCustomerLeadsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để xem danh sách yêu cầu tư vấn.");

        var leads = await _context.Leads
            .Include(l => l.Customer)
            .Include(l => l.Vendor)
            .Include(l => l.Listing)
            .Include(l => l.Voucher)
            .Where(l => l.CustomerId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return leads.Select(l => new LeadDto(
            Id: l.Id,
            CustomerId: l.CustomerId,
            CustomerName: l.Customer.FullName,
            VendorId: l.VendorId,
            VendorBrandName: l.Vendor.BrandName,
            ListingId: l.ListingId,
            ListingTitle: l.Listing?.Title,
            PhoneNumber: l.RawPhoneNumber, // Khách hàng luôn thấy số điện thoại của chính mình
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
