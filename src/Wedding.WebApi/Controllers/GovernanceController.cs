using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedding.Application.Features.Governance;

namespace Wedding.WebApi.Controllers;

/// <summary>P6 – Xác minh NCC, tranh chấp, CMS bài viết, AuditLog, dashboard quản trị.</summary>
[ApiController]
[Route("api")]
public class GovernanceController : ControllerBase
{
    private readonly IMediator _mediator;
    public GovernanceController(IMediator mediator) => _mediator = mediator;

    public record GovernanceReviewBody(bool Approve, string? Note);

    [HttpPost("verifications")]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> SubmitVerification([FromBody] SubmitVerificationCommand command) => Ok(await _mediator.Send(command));

    [HttpGet("verifications/my")]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> MyVerifications() => Ok(await _mediator.Send(new GetVerificationsQuery(true)));

    [HttpGet("verifications")]
    [Authorize(Roles = "Moderator,SuperAdmin")]
    public async Task<IActionResult> Verifications() => Ok(await _mediator.Send(new GetVerificationsQuery(false)));

    [HttpPost("verifications/{id:guid}/review")]
    [Authorize(Roles = "Moderator,SuperAdmin")]
    public async Task<IActionResult> Review(Guid id, [FromBody] GovernanceReviewBody body) => Ok(await _mediator.Send(new ReviewVerificationCommand(id, body.Approve, body.Note)));

    [HttpPost("disputes")]
    [Authorize(Roles = "Customer,VendorOwner")]
    public async Task<IActionResult> OpenDispute([FromBody] OpenDisputeCommand command) => Ok(await _mediator.Send(command));

    [HttpGet("disputes/my")]
    [Authorize]
    public async Task<IActionResult> MyDisputes() => Ok(await _mediator.Send(new GetDisputesQuery(true)));

    [HttpGet("disputes")]
    [Authorize(Roles = "CustomerCare,Finance,SuperAdmin")]
    public async Task<IActionResult> Disputes() => Ok(await _mediator.Send(new GetDisputesQuery(false)));

    [HttpPost("disputes/{id:guid}/resolve")]
    [Authorize(Roles = "CustomerCare,Finance,SuperAdmin")]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveDisputeCommand body) => Ok(await _mediator.Send(body with { Id = id }));

    [HttpGet("articles")]
    public async Task<IActionResult> Articles() => Ok(await _mediator.Send(new GetArticlesQuery(false, null)));

    [HttpGet("articles/{slug}")]
    public async Task<IActionResult> Article(string slug) => Ok((await _mediator.Send(new GetArticlesQuery(false, slug))).FirstOrDefault());

    [HttpGet("admin/articles")]
    [Authorize(Roles = "Moderator,SuperAdmin")]
    public async Task<IActionResult> AllArticles([FromQuery] string? slug) => Ok(await _mediator.Send(new GetArticlesQuery(true, slug)));

    [HttpPost("admin/articles")]
    [Authorize(Roles = "Moderator,SuperAdmin")]
    public async Task<IActionResult> SaveArticle([FromBody] SaveArticleCommand command) => Ok(await _mediator.Send(command));

    [HttpPut("admin/articles/{id:guid}"), Authorize(Roles = "Moderator,SuperAdmin")]
    public async Task<IActionResult> UpdateArticle(Guid id, [FromBody] UpdateArticleCommand command) => Ok(await _mediator.Send(command with { Id = id }));
    [HttpDelete("admin/articles/{id:guid}"), Authorize(Roles = "Moderator,SuperAdmin")]
    public async Task<IActionResult> DeleteArticle(Guid id) { await _mediator.Send(new DeleteArticleCommand(id)); return NoContent(); }
    [HttpGet("vendor/tax-profile"), Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> TaxProfile() => Ok(await _mediator.Send(new GetVendorTaxProfileQuery()));
    [HttpPut("vendor/tax-profile"), Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> UpdateTaxProfile([FromBody] UpdateVendorTaxProfileCommand command) => Ok(await _mediator.Send(command));
    [HttpPost("admin/vendors/{id:guid}/suspend"), Authorize(Roles = "Moderator,SuperAdmin")]
    public async Task<IActionResult> Suspend(Guid id, [FromBody] SuspendVendorCommand command) { await _mediator.Send(command with { VendorId = id }); return NoContent(); }
    [HttpPost("admin/emergency-matches"), Authorize(Roles = "CustomerCare,SuperAdmin")]
    public async Task<IActionResult> EmergencyMatch([FromBody] EmergencyMatchCommand command) => Ok(new { leadId = await _mediator.Send(command) });

    [HttpGet("admin/audit-logs")]
    [Authorize(Roles = "SuperAdmin,Finance")]
    public async Task<IActionResult> AuditLogs([FromQuery] int take = 100) => Ok(await _mediator.Send(new GetAuditLogsQuery(take)));

    [HttpGet("admin/dashboard")]
    [Authorize(Roles = "SuperAdmin,Finance,Moderator,CustomerCare")]
    public async Task<IActionResult> Dashboard() => Ok(await _mediator.Send(new GetAdminDashboardQuery()));
}
