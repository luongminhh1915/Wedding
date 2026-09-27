using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Vouchers.DTOs;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Vouchers.Queries.GetCustomerVouchers;

public record GetCustomerVouchersQuery : IRequest<List<VoucherDto>>;

public class GetCustomerVouchersQueryHandler : IRequestHandler<GetCustomerVouchersQuery, List<VoucherDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetCustomerVouchersQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<VoucherDto>> Handle(GetCustomerVouchersQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để xem danh sách ưu đãi.");

        var vouchers = await _context.Vouchers
            .Include(v => v.Customer)
            .Include(v => v.Lead)
                .ThenInclude(l => l.Vendor)
            .Where(v => v.CustomerId == userId)
            .OrderByDescending(v => v.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;

        return vouchers.Select(v => new VoucherDto(
            Id: v.Id,
            Code: v.Code,
            LeadId: v.LeadId,
            CustomerId: v.CustomerId,
            CustomerName: v.Customer.FullName,
            VendorId: v.Lead.VendorId,
            VendorBrandName: v.Lead.Vendor.BrandName,
            DiscountValue: v.DiscountValue,
            DiscountPercent: v.DiscountPercent,
            Status: v.Status.ToString(),
            IssuedAt: v.IssuedAt,
            ExpiresAt: v.ExpiresAt,
            RedeemedAt: v.RedeemedAt,
            IsExpired: v.ExpiresAt < now
        )).ToList();
    }
}
