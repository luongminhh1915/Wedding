using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Leads.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Leads.Commands.SendLead;

public record SendLeadCommand(
    Guid? ListingId,
    Guid? VendorId,
    string? PhoneNumber,
    DateTime? WeddingDate,
    int? EstimatedGuests,
    decimal? EstimatedBudget,
    string? Notes
) : IRequest<SendLeadResponseDto>;

public class SendLeadCommandValidator : AbstractValidator<SendLeadCommand>
{
    public SendLeadCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.ListingId.HasValue || x.VendorId.HasValue)
            .WithMessage("Cần cung cấp thông tin bài đăng (ListingId) hoặc nhà cung cấp (VendorId).");

        RuleFor(x => x.EstimatedGuests)
            .GreaterThan(0).When(x => x.EstimatedGuests.HasValue)
            .WithMessage("Số lượng khách dự kiến phải lớn hơn 0.");

        RuleFor(x => x.EstimatedBudget)
            .GreaterThan(0).When(x => x.EstimatedBudget.HasValue)
            .WithMessage("Ngân sách dự kiến phải lớn hơn 0.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Ghi chú không được vượt quá 1000 ký tự.");
    }
}

public class SendLeadCommandHandler : IRequestHandler<SendLeadCommand, SendLeadResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SendLeadCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<SendLeadResponseDto> Handle(SendLeadCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để gửi yêu cầu tư vấn.");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Người dùng không tồn tại.");

        Guid vendorId;
        Guid? listingId = request.ListingId;

        if (request.ListingId.HasValue)
        {
            var listing = await _context.Listings
                .FirstOrDefaultAsync(l => l.Id == request.ListingId.Value, cancellationToken)
                ?? throw new NotFoundException("Dịch vụ cưới không tồn tại.");

            vendorId = listing.VendorId;
        }
        else if (request.VendorId.HasValue)
        {
            vendorId = request.VendorId.Value;
            var vendorExists = await _context.Vendors
                .AnyAsync(v => v.Id == vendorId, cancellationToken);

            if (!vendorExists)
                throw new NotFoundException("Nhà cung cấp dịch vụ không tồn tại.");
        }
        else
        {
            throw new DomainException("Không xác định được nhà cung cấp dịch vụ.");
        }

        // Lấy số điện thoại từ request hoặc tài khoản khách hàng
        var phone = !string.IsNullOrWhiteSpace(request.PhoneNumber)
            ? request.PhoneNumber.Trim()
            : user.PhoneNumber;

        if (string.IsNullOrWhiteSpace(phone))
            throw new DomainException("Vui lòng cung cấp số điện thoại liên hệ để nhận tư vấn.");

        // BR-002: Khởi tạo Lead (MaskedPhoneNumber tự động sinh: ví dụ 0987***123)
        // BR-003: SLA tiếp nhận 2h tính từ thời điểm tạo
        var lead = Lead.Create(
            customerId: userId,
            vendorId: vendorId,
            listingId: listingId,
            rawPhone: phone,
            weddingDate: request.WeddingDate,
            estimatedGuests: request.EstimatedGuests,
            estimatedBudget: request.EstimatedBudget,
            notes: request.Notes
        );

        // BR-004: Tự động sinh mã Voucher 8 ký tự độc nhất (VD: WVIP8899), có hạn 30 ngày
        string voucherCode;
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        do
        {
            var randomPart = new string(Enumerable.Repeat(chars, 4)
                .Select(s => s[Random.Shared.Next(s.Length)]).ToArray());
            voucherCode = $"WVIP{randomPart}";
        } while (await _context.Vouchers.AnyAsync(v => v.Code == voucherCode, cancellationToken));

        var voucher = Voucher.Create(
            leadId: lead.Id,
            customerId: userId,
            customCode: voucherCode,
            discountValue: 500000m // Ưu đãi tiêu chuẩn 500,000đ từ Sàn
        );

        _context.Leads.Add(lead);
        _context.Vouchers.Add(voucher);
        await _context.SaveChangesAsync(cancellationToken);

        var voucherSummary = new VoucherSummaryDto(
            Id: voucher.Id,
            Code: voucher.Code,
            DiscountValue: voucher.DiscountValue,
            DiscountPercent: voucher.DiscountPercent,
            Status: voucher.Status.ToString(),
            ExpiresAt: voucher.ExpiresAt
        );

        return new SendLeadResponseDto(
            LeadId: lead.Id,
            Status: lead.Status.ToString(),
            SlaDeadline: lead.SlaDeadline,
            Voucher: voucherSummary,
            Message: $"Yêu cầu tư vấn đã được gửi thành công. Bạn nhận được mã ưu đãi {voucher.Code} trị giá 500.000đ (có hạn trong 30 ngày)!"
        );
    }
}
