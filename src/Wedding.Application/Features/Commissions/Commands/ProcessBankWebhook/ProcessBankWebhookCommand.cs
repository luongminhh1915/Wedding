using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Enums;

namespace Wedding.Application.Features.Commissions.Commands.ProcessBankWebhook;

public record ProcessBankWebhookCommand(
    string Gateway,
    string? TransactionDate,
    string? AccountNumber,
    decimal Amount,
    string Content,
    string ReferenceCode
) : IRequest<ProcessBankWebhookResult>;

public record ProcessBankWebhookResult(
    bool Success,
    int SettledCommissionsCount,
    decimal SettledAmount,
    string Message
);

public class ProcessBankWebhookCommandValidator : AbstractValidator<ProcessBankWebhookCommand>
{
    public ProcessBankWebhookCommandValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Số tiền thanh toán phải lớn hơn 0.");
        RuleFor(x => x.Content).NotEmpty().WithMessage("Nội dung chuyển khoản không được để trống.");
        RuleFor(x => x.ReferenceCode).NotEmpty().WithMessage("Mã giao dịch ngân hàng không được để trống.");
    }
}

public class ProcessBankWebhookCommandHandler : IRequestHandler<ProcessBankWebhookCommand, ProcessBankWebhookResult>
{
    private readonly IApplicationDbContext _context;

    public ProcessBankWebhookCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProcessBankWebhookResult> Handle(ProcessBankWebhookCommand request, CancellationToken cancellationToken)
    {
        var cleanContent = request.Content.Trim().ToUpperInvariant();
        var remainingAmount = request.Amount;
        var settledCount = 0;
        decimal settledTotal = 0;

        // 1. Kiểm tra xem mã giao dịch đã từng xử lý chưa (chống gạch nợ trùng lặp)
        var alreadyProcessed = await _context.Commissions
            .AnyAsync(c => c.PaymentReferenceCode == request.ReferenceCode && c.Status == CommissionStatus.Paid, cancellationToken);

        if (alreadyProcessed)
        {
            return new ProcessBankWebhookResult(true, 0, 0, "Giao dịch này đã được ghi nhận trước đó.");
        }

        // 2. Tìm kiếm theo cấu trúc định danh BR-007: 'HH <VendorCode> T<Month>'
        var vendors = await _context.Vendors.ToListAsync(cancellationToken);
        Domain.Entities.Vendor? matchedVendor = null;

        foreach (var v in vendors)
        {
            var shortCode = v.Id.ToString("N").Substring(0, 6).ToUpperInvariant();
            if (cleanContent.Contains($"HH {shortCode}") || cleanContent.Contains($"HH{shortCode}"))
            {
                matchedVendor = v;
                break;
            }
        }

        if (matchedVendor != null)
        {
            // Lấy các khoản hoa hồng chưa thanh toán của NCC này, ưu tiên khoản cũ trước
            var pendingCommissions = await _context.Commissions
                .Where(c => c.VendorId == matchedVendor.Id && 
                           (c.Status == CommissionStatus.Pending || c.Status == CommissionStatus.Overdue))
                .OrderBy(c => c.DueDate)
                .ToListAsync(cancellationToken);

            foreach (var comm in pendingCommissions)
            {
                if (remainingAmount >= comm.CommissionAmount)
                {
                    comm.MarkPaid(request.ReferenceCode);
                    remainingAmount -= comm.CommissionAmount;
                    settledTotal += comm.CommissionAmount;
                    settledCount++;
                }
                else
                {
                    break;
                }
            }
        }
        else
        {
            // Tìm theo mã Commission cụ thể nếu nội dung chứa mã 6 ký tự
            var allPending = await _context.Commissions
                .Where(c => c.Status == CommissionStatus.Pending || c.Status == CommissionStatus.Overdue)
                .ToListAsync(cancellationToken);

            foreach (var comm in allPending)
            {
                var commShort = comm.Id.ToString("N").Substring(0, 6).ToUpperInvariant();
                if (cleanContent.Contains(commShort) && request.Amount >= comm.CommissionAmount)
                {
                    comm.MarkPaid(request.ReferenceCode);
                    settledTotal += comm.CommissionAmount;
                    settledCount++;
                    break;
                }
            }
        }

        if (settledCount > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
            return new ProcessBankWebhookResult(
                Success: true,
                SettledCommissionsCount: settledCount,
                SettledAmount: settledTotal,
                Message: $"Gạch nợ thành công {settledCount} khoản hoa hồng, tổng tiền {settledTotal:N0} đ qua giao dịch {request.ReferenceCode}."
            );
        }

        return new ProcessBankWebhookResult(
            Success: false,
            SettledCommissionsCount: 0,
            SettledAmount: 0,
            Message: "Không tìm thấy khoản hoa hồng khớp với nội dung chuyển khoản hoặc số tiền không đủ."
        );
    }
}
