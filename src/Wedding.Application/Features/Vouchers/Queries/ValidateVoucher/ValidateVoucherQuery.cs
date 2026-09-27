using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Vouchers.DTOs;
using Wedding.Domain.Enums;

namespace Wedding.Application.Features.Vouchers.Queries.ValidateVoucher;

public record ValidateVoucherQuery(
    string Code,
    Guid? VendorId = null
) : IRequest<ValidateVoucherResultDto>;

public class ValidateVoucherQueryHandler : IRequestHandler<ValidateVoucherQuery, ValidateVoucherResultDto>
{
    private readonly IApplicationDbContext _context;

    public ValidateVoucherQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ValidateVoucherResultDto> Handle(ValidateVoucherQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return new ValidateVoucherResultDto(false, "Mã ưu đãi không được để trống.", null);
        }

        var cleanCode = request.Code.Trim().ToUpperInvariant();

        var voucher = await _context.Vouchers
            .Include(v => v.Customer)
            .Include(v => v.Lead)
                .ThenInclude(l => l.Vendor)
            .FirstOrDefaultAsync(v => v.Code == cleanCode, cancellationToken);

        if (voucher == null)
        {
            return new ValidateVoucherResultDto(false, "Mã ưu đãi không tồn tại trên hệ thống.", null);
        }

        var now = DateTime.UtcNow;
        if (voucher.ExpiresAt < now || voucher.Status == VoucherStatus.Expired)
        {
            return new ValidateVoucherResultDto(false, "Mã ưu đãi đã hết hạn 30 ngày (BR-004).", null);
        }

        if (voucher.Status == VoucherStatus.Redeemed)
        {
            return new ValidateVoucherResultDto(false, "Mã ưu đãi đã được sử dụng trước đó.", null);
        }

        if (voucher.Status != VoucherStatus.Active)
        {
            return new ValidateVoucherResultDto(false, "Mã ưu đãi không ở trạng thái khả dụng.", null);
        }

        if (request.VendorId.HasValue && voucher.Lead.VendorId != request.VendorId.Value)
        {
            return new ValidateVoucherResultDto(false, "Mã ưu đãi này được cấp cho nhà cung cấp khác.", null);
        }

        var voucherDto = new VoucherDto(
            Id: voucher.Id,
            Code: voucher.Code,
            LeadId: voucher.LeadId,
            CustomerId: voucher.CustomerId,
            CustomerName: voucher.Customer.FullName,
            VendorId: voucher.Lead.VendorId,
            VendorBrandName: voucher.Lead.Vendor.BrandName,
            DiscountValue: voucher.DiscountValue,
            DiscountPercent: voucher.DiscountPercent,
            Status: voucher.Status.ToString(),
            IssuedAt: voucher.IssuedAt,
            ExpiresAt: voucher.ExpiresAt,
            RedeemedAt: voucher.RedeemedAt,
            IsExpired: false
        );

        return new ValidateVoucherResultDto(true, "Mã ưu đãi hợp lệ.", voucherDto);
    }
}
