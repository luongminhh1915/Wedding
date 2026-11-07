using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Wedding.Application.Features.CommissionNegotiations;
using Wedding.Domain.Enums;

namespace Wedding.WebApi.Controllers;

[ApiController]
[Route("api/commission-negotiations")]
public class CommissionNegotiationsController(IMediator mediator, IConfiguration configuration) : ControllerBase, IAsyncActionFilter
{
    public const string EnabledConfigurationKey = "Features:CommissionNegotiationsEnabled";

    [NonAction]
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // This unfinished foundation has no deployed schema or signed-agreement activation
        // workflow. Default off, before any handler can query its tables.
        if (!configuration.GetValue<bool>(EnabledConfigurationKey))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Đàm phán hoa hồng chưa được bật trong môi trường này."
            }) { StatusCode = StatusCodes.Status503ServiceUnavailable };
            return;
        }
        await next();
    }
    [HttpPost]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> Create([FromBody] CreateCommissionNegotiationCommand command, CancellationToken ct) =>
        Ok(await mediator.Send(command, ct));

    [HttpGet("my")]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> GetMy([FromQuery] NegotiationStatus? status, CancellationToken ct) =>
        Ok(await mediator.Send(new GetMyNegotiationsQuery(status), ct));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "VendorOwner,Moderator,SuperAdmin,Finance")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetNegotiationDetailQuery(id), ct));

    [HttpPost("{id:guid}/messages")]
    [Authorize(Roles = "VendorOwner,Moderator,SuperAdmin,Finance")]
    public async Task<IActionResult> SendMessage(Guid id, [FromBody] SendNegotiationMessageBody body, CancellationToken ct) =>
        Ok(await mediator.Send(new SendNegotiationMessageCommand(id, body.Message, body.AttachmentUrl), ct));

    [HttpPost("{id:guid}/counter-offer")]
    [Authorize(Roles = "Moderator,SuperAdmin")]
    public async Task<IActionResult> CounterOffer(Guid id, [FromBody] CounterOfferBody body, CancellationToken ct) =>
        Ok(await mediator.Send(new CounterOfferNegotiationCommand(id, body.ExpectedVersion, body.CounterRate, body.Message), ct));

    [HttpPost("{id:guid}/respond-counter")]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> RespondCounter(Guid id, [FromBody] RespondCounterBody body, CancellationToken ct) =>
        Ok(await mediator.Send(new RespondToCounterOfferCommand(id, body.ExpectedVersion, body.Accept, body.Message, body.NewProposedRate), ct));

    [HttpPost("{id:guid}/decide")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Decide(Guid id, [FromBody] DecideNegotiationBody body, CancellationToken ct) =>
        Ok(await mediator.Send(new DecideNegotiationCommand(id, body.ExpectedVersion, body.Approve, body.ApprovedRate, body.Reason, body.CreatePolicyRule), ct));

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelNegotiationBody body, CancellationToken ct) =>
        Ok(await mediator.Send(new CancelNegotiationCommand(id, body.Reason), ct));

    [HttpGet("admin")]
    [Authorize(Roles = "Moderator,SuperAdmin,Finance")]
    public async Task<IActionResult> GetAdminQueue(
        [FromQuery] NegotiationStatus? status,
        [FromQuery] Guid? vendorId,
        [FromQuery] CommissionRuleScope? scope,
        CancellationToken ct) =>
        Ok(await mediator.Send(new GetAdminNegotiationsQuery(status, vendorId, scope), ct));
}

public record SendNegotiationMessageBody(string Message, string? AttachmentUrl = null);
public record CounterOfferBody(Guid ExpectedVersion, decimal CounterRate, string Message);
public record RespondCounterBody(Guid ExpectedVersion, bool Accept, string Message, decimal? NewProposedRate = null);
public record DecideNegotiationBody(Guid ExpectedVersion, bool Approve, decimal? ApprovedRate, string Reason, bool CreatePolicyRule = false);
public record CancelNegotiationBody(string Reason);

