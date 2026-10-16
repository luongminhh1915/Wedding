using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedding.Application.Features.Combos;

namespace Wedding.WebApi.Controllers;

/// <summary>P5 – Combo builder kiểu kiosk.</summary>
[ApiController]
[Route("api/combos")]
public class CombosController : ControllerBase
{
    private readonly IMediator _mediator;
    public CombosController(IMediator mediator) => _mediator = mediator;

    [HttpGet("builder")]
    public async Task<IActionResult> Builder([FromQuery] string? city) => Ok(await _mediator.Send(new GetComboBuilderQuery(city)));

    [HttpPost("orders")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Submit([FromBody] SubmitComboOrderCommand command) => Ok(await _mediator.Send(command));

    [HttpGet("orders/my")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> My() => Ok(await _mediator.Send(new GetMyComboOrdersQuery()));

    [HttpPost("orders/{id:guid}/cancel")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Cancel(Guid id) => Ok(new { success = await _mediator.Send(new CancelComboOrderCommand(id)) });

    [HttpGet("templates")]
    [Authorize(Roles = "Moderator,SuperAdmin")]
    public async Task<IActionResult> Templates() => Ok(await _mediator.Send(new GetAllComboTemplatesQuery()));

    [HttpPost("templates")]
    [Authorize(Roles = "Moderator,SuperAdmin")]
    public async Task<IActionResult> SaveTemplate([FromBody] SaveComboTemplateCommand command) => Ok(await _mediator.Send(command));
}
