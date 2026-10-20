using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Commissions.Commands.RecordAdminPayment;

public record RecordAdminPaymentCommand(Guid? ContractId = null, Guid? VendorId = null, decimal Amount = 0, string? PaymentReference = null, string? Note = null) : IRequest<RecordAdminPaymentResult>;
public record RecordAdminPaymentResult(bool Success, int SettledCommissionsCount, decimal SettledAmount, string Message);
public class RecordAdminPaymentCommandValidator : AbstractValidator<RecordAdminPaymentCommand>
{
    public RecordAdminPaymentCommandValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.PaymentReference).NotEmpty().MaximumLength(150).WithMessage("Cần mã giao dịch ngân hàng thật để chống ghi nhận trùng.");
        RuleFor(x => x.Note).NotEmpty().WithMessage("Cần ghi chú kiểm chứng giao dịch.");
        RuleFor(x => x).Must(x => x.ContractId.HasValue || x.VendorId.HasValue);
    }
}
public class RecordAdminPaymentCommandHandler : IRequestHandler<RecordAdminPaymentCommand, RecordAdminPaymentResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IBillingService _billing;
    private readonly ICurrentUserService _user;
    public RecordAdminPaymentCommandHandler(IApplicationDbContext db, IBillingService billing, ICurrentUserService user) { _db = db; _billing = billing; _user = user; }
    public async Task<RecordAdminPaymentResult> Handle(RecordAdminPaymentCommand r, CancellationToken ct)
    {
        if (_user.Role is not ("SuperAdmin" or "Finance")) throw new DomainException("Chỉ Finance được ghi nhận thu tiền.");
        var vendorId = r.VendorId;
        if (r.ContractId.HasValue) vendorId = await _db.BookingContracts.Where(c => c.Id == r.ContractId).Select(c => (Guid?)c.VendorId).FirstOrDefaultAsync(ct);
        if (!vendorId.HasValue) throw new NotFoundException("Không tìm thấy đối tượng thu tiền.");
        var statement = await _db.CommissionSettlements.Where(s => s.VendorId == vendorId && (s.Status == SettlementStatus.Closed || s.Status == SettlementStatus.Overdue)).OrderBy(s => s.DueDate).FirstOrDefaultAsync(ct);
        if (statement == null) return new(false, 0, 0, "Cần chốt bảng kê trước. Không thu K2 đang khóa hay tự gạch nợ ngoài bảng kê.");
        var receipt = await _billing.ReceiveAsync(new BankReceipt(r.PaymentReference!, "ManualFinance", "ManualVerified", r.Amount, $"SETTLEMENT:{statement.Id}"), statement.Id, _user.UserId, r.Note, ct);
        return new(receipt.Status == "Matched", 0, receipt.Status == "Matched" ? receipt.Amount : 0, receipt.UnmatchedReason ?? "Giao dịch đã được ghi nhận vào sổ đối soát.");
    }
}
