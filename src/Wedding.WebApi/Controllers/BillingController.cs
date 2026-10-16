using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Common;

namespace Wedding.WebApi.Controllers;

[ApiController]
[Route("api/billing")]
[Authorize]
public class BillingController : ControllerBase
{
    private readonly IBillingService _billing;
    private readonly ICurrentUserService _user;
    private readonly IApplicationDbContext _db;
    private readonly IConfiguration _config;
    public BillingController(IBillingService billing, ICurrentUserService user, IApplicationDbContext db, IConfiguration config) { _billing = billing; _user = user; _db = db; _config = config; }
    [HttpPost("premium/order")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Premium(CancellationToken ct) => Ok(await _billing.CreatePremiumOrderAsync(_user.UserId!.Value, ct));
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct) => Ok(await _billing.GetPremiumAsync(_user.UserId!.Value, ct));
    [HttpGet("unmatched")]
    [Authorize(Roles = "Finance,SuperAdmin")]
    public async Task<IActionResult> Unmatched(CancellationToken ct) => Ok(await _db.PaymentTransactions.AsNoTracking().Where(x => x.Status == "Unmatched").OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct));
    [HttpGet("premium/orders")]
    [Authorize(Roles = "Finance,SuperAdmin")]
    public async Task<IActionResult> PremiumOrders(CancellationToken ct) => Ok(await (from order in _db.PremiumOrders.AsNoTracking() join customer in _db.Users.AsNoTracking() on order.CustomerId equals customer.Id orderby order.CreatedAt descending select new { order.Id, order.PaymentReference, order.Amount, order.Status, order.ExpiresAt, order.PaidAt, CustomerName = customer.FullName }).Take(200).ToListAsync(ct));
    public record ManualPremiumBody(string TransactionId, decimal Amount, string Note);
    [HttpPost("premium/orders/{id:guid}/payment")]
    [Authorize(Roles = "Finance,SuperAdmin")]
    public async Task<IActionResult> ManualPremium(Guid id, ManualPremiumBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.TransactionId) || string.IsNullOrWhiteSpace(body.Note)) return BadRequest(new { message = "Cần mã giao dịch thật và ghi chú kiểm chứng sao kê." });
        var order = await _db.PremiumOrders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct); if (order == null) return NotFound();
        return Ok(await _billing.ReceiveAsync(new BankReceipt(body.TransactionId, "ManualFinance", "ManualVerified", body.Amount, order.PaymentReference), null, _user.UserId, body.Note, ct));
    }
    public record MatchBody(Guid? SettlementId, Guid? PremiumOrderId, string Note);
    [HttpPost("unmatched/{id:guid}/match")]
    [Authorize(Roles = "Finance,SuperAdmin")]
    public async Task<IActionResult> Match(Guid id, MatchBody body, CancellationToken ct) => Ok(await _billing.MatchAsync(id, body.SettlementId, body.PremiumOrderId, _user.UserId!.Value, body.Note, ct));

    /// <summary>HMAC SHA256 over exact UTF-8 body. Signature: X-Wedding-Signature hex; account must equal configured receiver.</summary>
    [HttpPost("webhook/bank-transfer")]
    [AllowAnonymous]
    [RequestSizeLimit(65536)]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        var secret = _config["BankWebhook:Secret"]; var account = _config["BankWebhook:AccountNumber"];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32 || string.IsNullOrWhiteSpace(account)) return StatusCode(503, new { message = "Webhook thanh toán chưa cấu hình an toàn." });
        using var reader = new StreamReader(Request.Body, Encoding.UTF8); var body = await reader.ReadToEndAsync(ct);
        if (!BankWebhookSignature.IsValid(secret, Request.Headers["X-Wedding-Signature"].ToString(), body)) return Unauthorized();
        BankReceipt? receipt;
        try { receipt = JsonSerializer.Deserialize<BankReceipt>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); } catch (JsonException) { return BadRequest(new { message = "JSON không hợp lệ." }); }
        if (receipt == null || receipt.AccountNumber != account || receipt.AccountNumber != _config["Payments:AccountNumber"] || string.IsNullOrWhiteSpace(receipt.TransactionId) || receipt.Content?.Length > 2000 || string.IsNullOrWhiteSpace(receipt.Gateway)) return BadRequest(new { message = "Giao dịch không thuộc tài khoản nhận hoặc thiếu dữ liệu." });
        return Ok(await _billing.ReceiveAsync(receipt, null, null, null, ct));
    }
}
