using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedding.Application.Features.Settlements;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Wedding.WebApi.Controllers;

/// <summary>P4 – Bảng kê đối soát hoa hồng tháng (BR-007).</summary>
[ApiController]
[Route("api/settlements")]
[Authorize]
public class SettlementsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IBillingService _billing;
    private readonly ICurrentUserService _user;
    private readonly IApplicationDbContext _db;
    private readonly IAuditLogger _audit;
    public SettlementsController(IMediator mediator, IBillingService billing, ICurrentUserService user, IApplicationDbContext db, IAuditLogger audit) { _mediator = mediator; _billing = billing; _user = user; _db = db; _audit = audit; }

    public record PayBody(decimal? Amount);

    [HttpGet]
    [Authorize(Roles = "Finance,SuperAdmin,VendorOwner")]
    public async Task<IActionResult> List([FromQuery] string? period) => Ok(await _mediator.Send(new GetSettlementsQuery(period)));

    [HttpPost("run")]
    [Authorize(Roles = "Finance,SuperAdmin")]
    public async Task<IActionResult> Run([FromQuery] string period) => Ok(await _mediator.Send(new RunSettlementCommand(period)));

    /// <summary>Mock webhook ngân hàng/SePay: ghi nhận đã nhận tiền + sinh số hóa đơn điện tử giả lập.</summary>
    [HttpPost("{id:guid}/mock-payment")]
    [Authorize(Roles = "Finance,SuperAdmin")]
    public IActionResult Pay(Guid id, [FromBody] PayBody? body) => StatusCode(410, new { message = "Đã bỏ thanh toán giả lập. Dùng /payment và mã giao dịch thực tế." });

    public record RecordBody(string TransactionId, decimal Amount, string Note);
    [HttpPost("{id:guid}/payment")]
    [Authorize(Roles = "Finance,SuperAdmin")]
    public async Task<IActionResult> Record(Guid id, RecordBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.TransactionId) || string.IsNullOrWhiteSpace(body.Note)) return BadRequest(new { message = "Cần mã giao dịch ngân hàng và ghi chú kiểm chứng." });
        return Ok(await _billing.ReceiveAsync(new BankReceipt(body.TransactionId, "ManualFinance", "ManualVerified", body.Amount, $"SETTLEMENT:{id}"), id, _user.UserId, body.Note, ct));
    }
    public record DisputeBody(string Reason);
    [HttpPost("{id:guid}/dispute")]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> Dispute(Guid id, DisputeBody body, CancellationToken ct)
    {
        var s = await _db.CommissionSettlements.Include(s => s.Vendor).FirstOrDefaultAsync(s => s.Id == id && s.Vendor.UserId == _user.UserId, ct);
        if (s == null) return NotFound(); s.Dispute(body.Reason); await _audit.LogAsync("Settlement.Disputed", nameof(CommissionSettlement), id.ToString(), new { body.Reason }); await _db.SaveChangesAsync(ct); return Ok();
    }
    [HttpPost("{id:guid}/resolve")]
    [Authorize(Roles = "Finance,SuperAdmin")]
    public async Task<IActionResult> Resolve(Guid id, CancellationToken ct)
    {
        var s = await _db.CommissionSettlements.FirstOrDefaultAsync(s => s.Id == id, ct); if (s == null) return NotFound(); s.ResolveDispute(); await _audit.LogAsync("Settlement.DisputeResolved", nameof(CommissionSettlement), id.ToString()); await _db.SaveChangesAsync(ct); return Ok();
    }
    public record InvoiceBody(string Provider, string InvoiceNumber, string LookupUrl, decimal VatRate);
    [HttpPost("{id:guid}/invoice")]
    [Authorize(Roles = "Finance,SuperAdmin")]
    public async Task<IActionResult> Invoice(Guid id, InvoiceBody body, CancellationToken ct)
    {
        var s = await _db.CommissionSettlements.FirstOrDefaultAsync(s => s.Id == id && s.Status == SettlementStatus.Paid, ct); if (s == null) return BadRequest(new { message = "Bảng kê phải trả đủ trước khi ghi nhận hóa đơn đã phát hành." });
        if (await _db.EInvoiceRecords.AnyAsync(x => x.SettlementId == id, ct)) return Conflict(new { message = "Bảng kê đã có hóa đơn." });
        var invoice = EInvoiceRecord.Record(id, body.Provider, body.InvoiceNumber, body.LookupUrl, body.VatRate, _user.UserId!.Value); _db.EInvoiceRecords.Add(invoice); s.AttachInvoice(body.InvoiceNumber); await _audit.LogAsync("Invoice.Recorded", nameof(EInvoiceRecord), invoice.Id.ToString(), new { id, body.Provider, body.InvoiceNumber, body.VatRate }); await _db.SaveChangesAsync(ct); return Ok(invoice);
    }
    [HttpGet("credits")]
    [Authorize(Roles = "Finance,SuperAdmin,VendorOwner")]
    public async Task<IActionResult> Credits(CancellationToken ct)
    {
        var query = _db.CommissionCreditNotes.AsNoTracking().AsQueryable();
        if (_user.Role == "VendorOwner") { var vendorId = await _db.Vendors.Where(v => v.UserId == _user.UserId).Select(v => v.Id).FirstOrDefaultAsync(ct); query = query.Where(x => x.VendorId == vendorId); }
        return Ok(await query.OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct));
    }
}
