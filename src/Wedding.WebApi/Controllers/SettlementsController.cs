using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedding.Application.Features.Settlements;

namespace Wedding.WebApi.Controllers;

/// <summary>P4 – Bảng kê đối soát hoa hồng tháng (BR-007).</summary>
[ApiController]
[Route("api/settlements")]
[Authorize]
public class SettlementsController : ControllerBase
{
    private readonly IMediator _mediator;
    public SettlementsController(IMediator mediator) => _mediator = mediator;

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
    public async Task<IActionResult> Pay(Guid id, [FromBody] PayBody? body) => Ok(await _mediator.Send(new PaySettlementCommand(id, body?.Amount)));
}
