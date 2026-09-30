using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Commissions.Commands.RecordAdminPayment;

public record RecordAdminPaymentCommand(
    Guid? ContractId = null,
    Guid? VendorId = null,
    decimal Amount = 0,
    string? PaymentReference = null,
    string? Note = null
) : IRequest<RecordAdminPaymentResult>;

public record RecordAdminPaymentResult(
    bool Success,
    int SettledCommissionsCount,
    decimal SettledAmount,
    string Message
);

public class RecordAdminPaymentCommandValidator : AbstractValidator<RecordAdminPaymentCommand>
{
    public RecordAdminPaymentCommandValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Số tiền thanh toán hoa hồng phải lớn hơn 0 VNĐ.");
        RuleFor(x => x).Must(x => x.ContractId.HasValue || x.VendorId.HasValue)
            .WithMessage("Phải cung cấp Mã hợp đồng (ContractId) hoặc Mã nhà cung cấp (VendorId).");
    }
}

public class RecordAdminPaymentCommandHandler : IRequestHandler<RecordAdminPaymentCommand, RecordAdminPaymentResult>
{
    private readonly IApplicationDbContext _context;

    public RecordAdminPaymentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RecordAdminPaymentResult> Handle(RecordAdminPaymentCommand request, CancellationToken cancellationToken)
    {
        string targetName;
        List<Commission> pendingCommissions;

        if (request.ContractId.HasValue && request.ContractId.Value != Guid.Empty)
        {
            var contract = await _context.BookingContracts
                .Include(ct => ct.Vendor)
                .Include(ct => ct.Customer)
                .FirstOrDefaultAsync(ct => ct.Id == request.ContractId.Value, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy hợp đồng.");

            targetName = $"đơn hàng {contract.ContractCode} ({contract.Vendor.BrandName})";

            pendingCommissions = await _context.Commissions
                .Where(c => c.ContractId == request.ContractId.Value &&
                           (c.Status == CommissionStatus.Pending || c.Status == CommissionStatus.Overdue))
                .OrderBy(c => c.DueDate)
                .ThenBy(c => c.CreatedAt)
                .ToListAsync(cancellationToken);
        }
        else if (request.VendorId.HasValue && request.VendorId.Value != Guid.Empty)
        {
            var vendor = await _context.Vendors
                .FirstOrDefaultAsync(v => v.Id == request.VendorId.Value, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy nhà cung cấp.");

            targetName = $"nhà cung cấp {vendor.BrandName}";

            pendingCommissions = await _context.Commissions
                .Where(c => c.VendorId == request.VendorId.Value &&
                           (c.Status == CommissionStatus.Pending || c.Status == CommissionStatus.Overdue))
                .OrderBy(c => c.DueDate)
                .ThenBy(c => c.CreatedAt)
                .ToListAsync(cancellationToken);
        }
        else
        {
            return new RecordAdminPaymentResult(
                Success: false,
                SettledCommissionsCount: 0,
                SettledAmount: 0,
                Message: "Vui lòng chọn đơn hợp đồng hoặc nhà cung cấp để ghi nhận thanh toán."
            );
        }

        if (!pendingCommissions.Any())
        {
            return new RecordAdminPaymentResult(
                Success: false,
                SettledCommissionsCount: 0,
                SettledAmount: 0,
                Message: $"{targetName} hiện không có khoản hoa hồng nào cần thanh toán."
            );
        }

        var totalPending = pendingCommissions.Sum(c => c.CommissionAmount);
        if (request.Amount > totalPending)
        {
            return new RecordAdminPaymentResult(
                Success: false,
                SettledCommissionsCount: 0,
                SettledAmount: 0,
                Message: $"Số tiền thanh toán ({request.Amount:N0} đ) vượt quá tổng dư nợ hiện tại ({totalPending:N0} đ) của {targetName}."
            );
        }

        var remainingAmount = request.Amount;
        var settledCount = 0;
        decimal settledTotal = 0;
        var refCode = !string.IsNullOrWhiteSpace(request.PaymentReference)
            ? request.PaymentReference.Trim()
            : (!string.IsNullOrWhiteSpace(request.Note) ? request.Note.Trim() : $"ADMIN-PAY-{DateTime.UtcNow:yyyyMMddHHmmss}");

        if (refCode.Length > 100)
        {
            refCode = refCode.Substring(0, 100);
        }

        foreach (var comm in pendingCommissions)
        {
            if (remainingAmount <= 0) break;

            if (remainingAmount >= comm.CommissionAmount)
            {
                remainingAmount -= comm.CommissionAmount;
                settledTotal += comm.CommissionAmount;
                settledCount++;
                comm.MarkPaid(refCode);
            }
            else
            {
                var partialPaid = remainingAmount;
                var remainder = comm.CommissionAmount - partialPaid;

                comm.AdjustAmount(partialPaid);
                comm.MarkPaid(refCode);
                settledTotal += partialPaid;
                settledCount++;
                remainingAmount = 0;

                var remainderComm = Commission.Create(
                    contractId: comm.ContractId,
                    vendorId: comm.VendorId,
                    period: comm.Period,
                    commissionRate: comm.CommissionRate,
                    commissionAmount: remainder,
                    dueDate: comm.DueDate
                );
                _context.Commissions.Add(remainderComm);
                break;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new RecordAdminPaymentResult(
            Success: true,
            SettledCommissionsCount: settledCount,
            SettledAmount: settledTotal,
            Message: $"Ghi nhận thành công thanh toán {settledTotal:N0} đ hoa hồng cho {targetName}."
        );
    }
}
