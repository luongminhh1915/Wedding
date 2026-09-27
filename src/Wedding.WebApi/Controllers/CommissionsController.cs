using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedding.Application.Features.Commissions.Commands.ProcessBankWebhook;
using Wedding.Application.Features.Commissions.Queries.GetVendorSettlementStatement;

namespace Wedding.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CommissionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CommissionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// [VendorOwner] Xem bảng kê đối soát hoa hồng chu kỳ ngày 25 hàng tháng (BR-007).
    /// Kèm mã VietQR động chuẩn Napas247 để quét app ngân hàng thanh toán.
    /// </summary>
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
