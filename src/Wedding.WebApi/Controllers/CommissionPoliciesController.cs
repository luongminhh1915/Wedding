using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedding.Application.Features.CommissionPolicies;
using Wedding.Domain.Enums;

namespace Wedding.WebApi.Controllers;

[ApiController]
[Route("api/commission-policies")]
public class CommissionPoliciesController(IMediator mediator) : ControllerBase
{
    [HttpGet("rules")]
    [Authorize(Roles = "SuperAdmin,Finance")]
    public async Task<IActionResult> Rules([FromQuery] CommissionPolicyStatus? status, CancellationToken ct) =>
        Ok(await mediator.Send(new GetCommissionPolicyRulesQuery(status), ct));

    [HttpPost("rules")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> CreateRule([FromBody] CreateCommissionPolicyRuleCommand command, CancellationToken ct) =>
        Ok(await mediator.Send(command, ct));

    [HttpPost("rules/{id:guid}/submit")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> SubmitRule(Guid id, [FromBody] ExpectedPolicyVersionBody body, CancellationToken ct) =>
        Ok(await mediator.Send(new SubmitCommissionPolicyRuleCommand(id, body.ExpectedVersion), ct));

    [HttpPost("rules/{id:guid}/review")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> ReviewRule(Guid id, [FromBody] ReviewPolicyRuleBody body, CancellationToken ct) =>
        Ok(await mediator.Send(new ReviewCommissionPolicyRuleCommand(id, body.ExpectedVersion, body.Approve, body.Reason), ct));

    [HttpGet("component-bases")]
    [Authorize(Roles = "SuperAdmin,Finance")]
    public async Task<IActionResult> ComponentBases([FromQuery] Guid revisionId, CancellationToken ct) =>
        Ok(await mediator.Send(new GetCommissionComponentBasisQuery(revisionId), ct));

    [HttpPost("component-bases")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> CreateComponentBasis([FromBody] CreateCommissionComponentBasisCommand command, CancellationToken ct) =>
        Ok(await mediator.Send(command, ct));

    [HttpPost("component-bases/{id:guid}/submit")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> SubmitComponentBasis(Guid id, [FromBody] ExpectedPolicyVersionBody body, CancellationToken ct) =>
        Ok(await mediator.Send(new SubmitCommissionComponentBasisCommand(id, body.ExpectedVersion), ct));

    [HttpPost("component-bases/{id:guid}/review")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> ReviewComponentBasis(Guid id, [FromBody] ReviewPolicyRuleBody body, CancellationToken ct) =>
        Ok(await mediator.Send(new ReviewCommissionComponentBasisCommand(id, body.ExpectedVersion, body.Approve, body.Reason), ct));

    [HttpPost("~/api/commission-adjustments")]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> RequestAdjustment([FromBody] CreateCommissionAdjustmentCommand command, CancellationToken ct) =>
        Ok(await mediator.Send(command, ct));

    [HttpGet("~/api/commission-adjustments")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Adjustments([FromQuery] CommissionAdjustmentStatus? status, CancellationToken ct) =>
        Ok(await mediator.Send(new GetCommissionAdjustmentsQuery(status), ct));

    [HttpPost("~/api/commission-adjustments/{id:guid}/decision")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> DecideAdjustment(Guid id, [FromBody] DecideAdjustmentBody body, CancellationToken ct) =>
        Ok(await mediator.Send(new DecideCommissionAdjustmentCommand(id, body.ExpectedVersion, body.Approve, body.Reason), ct));
}

public record ExpectedPolicyVersionBody(Guid ExpectedVersion);
public record ReviewPolicyRuleBody(Guid ExpectedVersion, bool Approve, string? Reason);
public record DecideAdjustmentBody(Guid ExpectedVersion, bool Approve, string Reason);
