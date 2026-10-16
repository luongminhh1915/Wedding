using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Quotes;

public record QuoteLineDto(string Name, int Quantity, decimal UnitPrice);
public record QuoteDto(Guid Id, string Code, Guid LeadId, Guid VendorId, string VendorName, Guid CustomerId, List<QuoteLineDto> Items,
    decimal TotalAmount, decimal DepositAmount, string? Terms, DateTime ValidUntil, string Status, int Version, DateTime CreatedAt, Guid? ContractId,
    decimal Subtotal = 0, decimal VatRate = 0, decimal VatAmount = 0);

internal static class QuoteMap
{
    public static QuoteDto ToDto(Quote q, Guid? contractId = null) => new(q.Id, q.Code, q.LeadId, q.VendorId, q.Vendor?.BrandName ?? "", q.CustomerId,
        JsonSerializer.Deserialize<List<QuoteLineDto>>(q.ItemsJson) ?? new(), q.TotalAmount, q.DepositAmount, q.Terms, q.ValidUntil,
        q.Status.ToString(), q.Version, q.CreatedAt, contractId, q.Subtotal > 0 ? q.Subtotal : q.TotalAmount, q.VatRate, q.VatAmount);
}

/// <summary>P4 – NCC gửi báo giá có cấu trúc cho Lead đã tiếp nhận. Báo giá mới thay thế báo giá cũ (Superseded).</summary>
public record SendQuoteCommand(Guid LeadId, List<QuoteLineDto> Items, decimal DepositAmount, string? Terms, int ValidDays, decimal VatRate = 0) : IRequest<QuoteDto>;

public class SendQuoteCommandHandler : IRequestHandler<SendQuoteCommand, QuoteDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    private readonly INotificationSender _notify;
    public SendQuoteCommandHandler(IApplicationDbContext context, ICurrentUserService user, INotificationSender notify) { _context = context; _user = user; _notify = notify; }

    public Task<QuoteDto> Handle(SendQuoteCommand r, CancellationToken ct) => _context.ExecuteSerializableAsync(token => SendAsync(r, token), ct);
    private async Task<QuoteDto> SendAsync(SendQuoteCommand r, CancellationToken ct)
    {
        var uid = _user.UserId ?? throw new DomainException("Bạn cần đăng nhập.");
        var lead = await _context.Leads.Include(l => l.Vendor).FirstOrDefaultAsync(l => l.Id == r.LeadId, ct) ?? throw new NotFoundException("Lead không tồn tại.");
        if (lead.Vendor.UserId != uid) throw new DomainException("Bạn không phải nhà cung cấp của yêu cầu này.");
        if (lead.Status is not (LeadStatus.Accepted or LeadStatus.Contacted)) throw new DomainException("Chỉ gửi báo giá cho yêu cầu đã tiếp nhận.");
        if (await _context.BookingContracts.AnyAsync(c => c.LeadId == lead.Id && c.Status != ContractStatus.Draft && c.Status != ContractStatus.Cancelled, ct)) throw new DomainException("Lead đã có hợp đồng đang xử lý. Chỉ sửa lại sau khi khách trả về bản nháp.");
        if (r.Items == null || r.Items.Count > 100 || r.Items.Any(i => string.IsNullOrWhiteSpace(i.Name) || i.Name.Length > 300 || i.Quantity < 1 || i.Quantity > 100000 || i.UnitPrice < 0 || i.UnitPrice > 100000000000m) || r.VatRate < 0 || r.VatRate > 1)
            throw new DomainException("Dòng báo giá hoặc thuế suất không hợp lệ.");
        var lines = r.Items.ToList();
        if (lines.Count == 0) throw new DomainException("Báo giá cần ít nhất 1 hạng mục.");
        var previous = await _context.Quotes.Where(q => q.LeadId == lead.Id).ToListAsync(ct);
        foreach (var p in previous) p.Supersede();
        var subtotal = lines.Sum(i => i.Quantity * i.UnitPrice);
        var quote = Quote.Create(lead.Id, lead.VendorId, lead.CustomerId, JsonSerializer.Serialize(lines), subtotal + Math.Round(subtotal * r.VatRate, 0, MidpointRounding.AwayFromZero),
            r.DepositAmount, r.Terms, r.ValidDays <= 0 ? 7 : r.ValidDays, previous.Count + 1);
        quote.SetVat(subtotal, r.VatRate);
        _context.Quotes.Add(quote);
        await _context.SaveChangesAsync(ct);
        await _notify.SendAsync("InApp", lead.CustomerId.ToString(), $"{lead.Vendor.BrandName} đã gửi báo giá {quote.Code}", ct);
        return QuoteMap.ToDto(quote) with { VendorName = lead.Vendor.BrandName };
    }
}

public record GetQuotesQuery(Guid? LeadId) : IRequest<List<QuoteDto>>;

public class GetQuotesQueryHandler : IRequestHandler<GetQuotesQuery, List<QuoteDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    public GetQuotesQueryHandler(IApplicationDbContext context, ICurrentUserService user) { _context = context; _user = user; }

    public async Task<List<QuoteDto>> Handle(GetQuotesQuery r, CancellationToken ct)
    {
        var uid = _user.UserId ?? throw new DomainException("Bạn cần đăng nhập.");
        var q = _context.Quotes.AsNoTracking().Include(x => x.Vendor).Where(x => x.CustomerId == uid || x.Vendor.UserId == uid);
        if (r.LeadId.HasValue) q = q.Where(x => x.LeadId == r.LeadId);
        var list = await q.ToListAsync(ct);
        var ids = list.Select(x => x.Id).ToList();
        var contracts = await _context.BookingContracts.Where(c => c.QuoteId != null && ids.Contains(c.QuoteId.Value)).ToDictionaryAsync(c => c.QuoteId!.Value, c => c.Id, ct);
        return list.OrderByDescending(x => x.CreatedAt).Select(x => QuoteMap.ToDto(x, contracts.TryGetValue(x.Id, out var cid) ? cid : null)).ToList();
    }
}

/// <summary>P4 – Khách chấp nhận báo giá → tự sinh hợp đồng PendingVerification (điền sẵn giá trị/cọc, áp voucher của Lead).</summary>
public record RespondQuoteCommand(Guid Id, bool Accept, string? Note) : IRequest<QuoteDto>;

public class RespondQuoteCommandHandler : IRequestHandler<RespondQuoteCommand, QuoteDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    public RespondQuoteCommandHandler(IApplicationDbContext context, ICurrentUserService user) { _context = context; _user = user; }

    public Task<QuoteDto> Handle(RespondQuoteCommand r, CancellationToken ct) => _context.ExecuteSerializableAsync(token => RespondAsync(r, token), ct);
    private async Task<QuoteDto> RespondAsync(RespondQuoteCommand r, CancellationToken ct)
    {
        var uid = _user.UserId ?? throw new DomainException("Bạn cần đăng nhập.");
        var quote = await _context.Quotes.Include(q => q.Vendor).Include(q => q.Lead).ThenInclude(l => l.Voucher)
            .Include(q => q.Lead).ThenInclude(l => l.Listing)
            .FirstOrDefaultAsync(q => q.Id == r.Id && q.CustomerId == uid, ct) ?? throw new NotFoundException("Báo giá không tồn tại.");
        Guid? contractId = null;
        if (!r.Accept) quote.Reject(r.Note);
        else
        {
            quote.Accept();
            if (await _context.BookingContracts.AnyAsync(c => c.LeadId == quote.LeadId &&
                    (c.Status == ContractStatus.PendingVerification || c.Status == ContractStatus.Confirmed), ct))
                throw new DomainException("Yêu cầu này đã có hợp đồng đang xử lý.");
            var voucher = quote.Lead.Voucher is { Status: VoucherStatus.Active } v && v.ExpiresAt > DateTime.UtcNow ? v : null;
            var contract = await _context.BookingContracts.FirstOrDefaultAsync(c => c.LeadId == quote.LeadId, ct);
            if (contract == null)
            {
                contract = BookingContract.Create(quote.LeadId, voucher?.Id, quote.VendorId, quote.CustomerId, quote.TotalAmount, quote.DepositAmount, null, quote.Lead.WeddingDate);
                _context.BookingContracts.Add(contract);
            }
            else if (contract.Status == ContractStatus.Draft) contract.Revise(quote.TotalAmount, quote.DepositAmount, null, quote.Lead.WeddingDate, quote.Lead.Listing?.CancellationPolicy);
            else throw new DomainException("Lead đã có hợp đồng hủy/hoàn tất. Tạo yêu cầu tư vấn mới để giữ lịch sử hợp đồng.");
            contract.LinkQuote(quote.Id);
            contract.SetCancellationPolicy(quote.Lead.Listing?.CancellationPolicy);
            var appointmentCredit = (await _context.Appointments.Where(a => a.LeadId == quote.LeadId && a.DepositPaid && a.RefundDue == 0 && a.RefundedAt == null).ToListAsync(ct)).Sum(a => a.DepositAmount);
            contract.ApplyAppointmentDepositCredit(appointmentCredit);
            contractId = contract.Id;
        }
        await _context.SaveChangesAsync(ct);
        return QuoteMap.ToDto(quote, contractId);
    }
}
