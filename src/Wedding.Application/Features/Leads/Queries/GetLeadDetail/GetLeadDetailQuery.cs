using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Leads.DTOs;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Leads.Queries.GetLeadDetail;

public record GetLeadDetailQuery(Guid Id) : IRequest<LeadDto>;

public class GetLeadDetailQueryHandler : IRequestHandler<GetLeadDetailQuery, LeadDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetLeadDetailQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<LeadDto> Handle(GetLeadDetailQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để xem thông tin yêu cầu tư vấn.");

        var lead = await _context.Leads
            .Include(l => l.Customer)
            .Include(l => l.Vendor)
            .Include(l => l.Listing)
            .Include(l => l.Voucher)
            .FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Yêu cầu tư vấn không tồn tại.");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Người dùng không tồn tại.");

        var isCustomerOwner = lead.CustomerId == userId;
        var isVendorOwner = lead.Vendor.UserId == userId;
        var isAdminOrMod = user.Role == UserRole.SuperAdmin || user.Role == UserRole.Moderator;

        if (!isCustomerOwner && !isVendorOwner && !isAdminOrMod)
        {
            throw new DomainException("Bạn không có quyền truy cập thông tin yêu cầu tư vấn này.");
        }

        // BR-002 (Smart Privacy):
        // Nếu là Vendor và khách chưa mở khóa số điện thoại -> Mask số
        // Nếu là Customer, Admin hoặc đã mở khóa -> Xem số đầy đủ
        string displayPhone;
        if (isVendorOwner && !lead.IsPhoneUnlocked)
        {
            displayPhone = lead.MaskedPhoneNumber;
        }
        else
        {
            displayPhone = lead.RawPhoneNumber;
        }

        return new LeadDto(
            Id: lead.Id,
            CustomerId: lead.CustomerId,
            CustomerName: lead.Customer.FullName,
            VendorId: lead.VendorId,
            VendorBrandName: lead.Vendor.BrandName,
            ListingId: lead.ListingId,
            ListingTitle: lead.Listing?.Title,
            PhoneNumber: displayPhone,
            IsPhoneUnlocked: lead.IsPhoneUnlocked,
            WeddingDate: lead.WeddingDate,
            EstimatedGuests: lead.EstimatedGuests,
            EstimatedBudget: lead.EstimatedBudget,
            Notes: lead.Notes,
            Status: lead.Status.ToString(),
            SlaDeadline: lead.SlaDeadline,
            AcceptedAt: lead.AcceptedAt,
            CreatedAt: lead.CreatedAt,
            Voucher: lead.Voucher != null ? new VoucherSummaryDto(
                Id: lead.Voucher.Id,
                Code: lead.Voucher.Code,
                DiscountValue: lead.Voucher.DiscountValue,
                DiscountPercent: lead.Voucher.DiscountPercent,
                Status: lead.Voucher.Status.ToString(),
                ExpiresAt: lead.Voucher.ExpiresAt
            ) : null
        );
    }
}
