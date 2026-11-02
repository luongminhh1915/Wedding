using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedding.Application.Features.Quotes;

namespace Wedding.WebApi.Controllers;

/// <summary>P4 – Báo giá có cấu trúc → hợp đồng.</summary>
[ApiController]
[Route("api/quotes")]
[Authorize]
public class QuotesController : ControllerBase
{
    private readonly IMediator _mediator;
    public QuotesController(IMediator mediator) => _mediator = mediator;

    public record RespondBody(string? Note);

    [HttpPost]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> Send([FromBody] SendQuoteCommand command) => Ok(await _mediator.Send(command));

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? leadId) => Ok(await _mediator.Send(new GetQuotesQuery(leadId)));

    [HttpPost("{id:guid}/accept")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Accept(Guid id) => Ok(await _mediator.Send(new RespondQuoteCommand(id, true, null)));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RespondBody? body) => Ok(await _mediator.Send(new RespondQuoteCommand(id, false, body?.Note)));
}
