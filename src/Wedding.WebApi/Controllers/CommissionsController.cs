using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedding.Application.Features.Commissions.Commands.ProcessBankWebhook;
using Wedding.Application.Features.Commissions.Commands.RecordAdminPayment;
using Wedding.Application.Features.Commissions.Queries.GetAdminFinancialOverview;
using Wedding.Application.Features.Commissions.Queries.GetVendorSettlementStatement;

namespace Wedding.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CommissionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly Wedding.Application.Common.Interfaces.IAdvancePaymentService _advancePaymentService;

    public CommissionsController(
        IMediator mediator,
        Wedding.Application.Common.Interfaces.IAdvancePaymentService advancePaymentService)
    {
        _mediator = mediator;
        _advancePaymentService = advancePaymentService;
    }

    /// <summary>
    /// [SuperAdmin/Admin] Bảng kiểm kê tài chính toàn hệ thống: xem hoa hồng, cọc của từng Vendor.
    /// </summary>
    [HttpGet("admin/overview")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> GetAdminFinancialOverview([FromQuery] int? month, [FromQuery] int? year)
    {
        var result = await _mediator.Send(new GetAdminFinancialOverviewQuery(month, year));
        return Ok(result);
    }

    /// <summary>
    /// [SuperAdmin/Admin] Lấy danh sách tất cả thông báo chuyển tiền / yêu cầu thanh toán trước của Vendor.
    /// </summary>
    [HttpGet("admin/advance-requests")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> GetAdvanceRequests()
    {
        var list = await _advancePaymentService.GetAllAsync();
        return Ok(list);
    }

    /// <summary>
    /// [Vendor/Admin] Lấy danh sách thông báo chuyển tiền / yêu cầu thanh toán trước.
    /// </summary>
    [HttpGet("advance-requests")]
    [Authorize]
    public async Task<IActionResult> GetAllAdvanceRequests()
    {
        var list = await _advancePaymentService.GetAllAsync();
        return Ok(list);
    }

    /// <summary>
    /// [SuperAdmin/Admin] Xác nhận đã nhận được tiền từ thông báo chuyển tiền của Vendor.
    /// Tự động gạch nợ hoa hồng của hợp đồng và đổi trạng thái sang Approved.
    /// </summary>
    [HttpPost("admin/advance-requests/{id:guid}/confirm")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> ConfirmAdvancePayment(Guid id, [FromBody] ConfirmAdvanceNoticeRequest? request)
    {
        var notice = await _advancePaymentService.GetByIdAsync(id);
        if (notice == null)
        {
            return NotFound(new { success = false, message = "Không tìm thấy thông báo chuyển tiền." });
        }

        if (notice.Status != "PendingApproval")
        {
            return BadRequest(new { success = false, message = $"Thông báo này đã ở trạng thái {notice.Status}." });
        }

        var refCode = !string.IsNullOrWhiteSpace(request?.PaymentReference)
            ? request.PaymentReference.Trim()
            : $"UNC-XACNHAN-{DateTime.UtcNow:yyyyMMddHHmmss}";

        var paymentResult = await _mediator.Send(new RecordAdminPaymentCommand(
            ContractId: notice.ContractId,
            VendorId: notice.VendorId,
            Amount: notice.Amount,
            PaymentReference: refCode,
            Note: $"Xác nhận thông báo chuyển tiền {notice.Amount:N0} đ từ {notice.VendorBrandName}"
        ));

        if (!paymentResult.Success)
        {
            return BadRequest(new { success = false, message = paymentResult.Message });
        }

        var updated = await _advancePaymentService.ApproveAsync(id, request?.Note ?? "Admin đã xác nhận nhận đủ tiền chuyển khoản.");

        return Ok(new
        {
            success = true,
            message = $"Đã xác nhận nhận thành công {notice.Amount:N0} đ từ {notice.VendorBrandName} cho hợp đồng {notice.ContractCode}!",
            notice = updated,
            settledAmount = paymentResult.SettledAmount
        });
    }

    /// <summary>
    /// [SuperAdmin/Admin] Báo chưa nhận được tiền hoặc từ chối thông báo chuyển tiền của Vendor.
    /// </summary>
    [HttpPost("admin/advance-requests/{id:guid}/reject")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> RejectAdvancePayment(Guid id, [FromBody] RejectAdvanceNoticeRequest? request)
    {
        var notice = await _advancePaymentService.GetByIdAsync(id);
        if (notice == null)
        {
            return NotFound(new { success = false, message = "Không tìm thấy thông báo chuyển tiền." });
        }

        var reason = request?.Reason ?? "Chưa nhận được biến động số dư tài khoản ngân hàng.";
        var updated = await _advancePaymentService.RejectAsync(id, reason);

        return Ok(new
        {
            success = true,
            message = $"Đã báo chưa nhận được tiền / từ chối thông báo: {reason}",
            notice = updated
        });
    }

    /// <summary>
    /// [SuperAdmin/Admin] Ghi nhận số tiền hoa hồng mà Vendor đã trả thực tế.
    /// </summary>
    [HttpPost("admin/record-payment")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> RecordAdminPayment([FromBody] RecordAdminPaymentCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("statement")]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> GetVendorStatement([FromQuery] int? month, [FromQuery] int? year)
    {
        var result = await _mediator.Send(new GetVendorSettlementStatementQuery(month, year));
        return Ok(result);
    }

    /// <summary>
    /// Webhook tiếp nhận biến động số dư ngân hàng (từ Napas247, MBBank, Vietcombank...).
    /// Tự động phân tích nội dung chuyển khoản định danh 'HH <VendorCode> T<Month>'
    /// và gạch nợ hoa hồng sang trạng thái Paid (BR-007).
    /// </summary>
    [HttpPost("webhook/bank-transfer")]
    [AllowAnonymous]
    public async Task<IActionResult> ProcessBankWebhook([FromBody] ProcessBankWebhookCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
}

public record ConfirmAdvanceNoticeRequest(string? PaymentReference = null, string? Note = null);
public record RejectAdvanceNoticeRequest(string? Reason = null);
