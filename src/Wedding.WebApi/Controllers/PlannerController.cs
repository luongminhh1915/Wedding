using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedding.Application.Features.Planner;

namespace Wedding.WebApi.Controllers;

/// <summary>P1/P2 – Hồ sơ cưới, tổng quan Planner, thiệp Premium.</summary>
[ApiController]
[Route("api/planner")]
[Authorize(Roles = "Customer")]
public class PlannerController : ControllerBase
{
    private readonly IMediator _mediator;
    public PlannerController(IMediator mediator) => _mediator = mediator;

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile() => Ok(await _mediator.Send(new GetWeddingProfileQuery()));

    [HttpPut("profile")]
    public async Task<IActionResult> SaveProfile([FromBody] SaveWeddingProfileCommand command) => Ok(await _mediator.Send(command));

    [HttpGet("overview")]
    public async Task<IActionResult> Overview() => Ok(await _mediator.Send(new GetPlannerOverviewQuery()));

    /// <summary>P2 – Lấy QR thanh toán Premium (confirmPaid=false) hoặc xác nhận đã thanh toán (mock, confirmPaid=true).</summary>
    [HttpPost("invitation/premium")]
    public async Task<IActionResult> Premium([FromQuery] bool confirmPaid = false) => Ok(await _mediator.Send(new ActivateInvitationPremiumCommand(confirmPaid)));
}
