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
    public async Task<IActionResult> Builder([FromQuery] string? city, [FromQuery] int guests = 200, [FromQuery] string? style = null) => Ok(await _mediator.Send(new GetComboBuilderQuery(city, guests, style)));

    [HttpGet("catalog")]
    public async Task<IActionResult> Catalog([FromQuery] GetComboCatalogPageQuery query) => Ok(await _mediator.Send(query));

    [HttpPost("price")]
    public async Task<IActionResult> Price([FromBody] ComboPriceCommand command) => Ok(await _mediator.Send(command));

    [HttpPost("orders")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Submit([FromBody] SubmitComboOrderCommand command) => Ok(await _mediator.Send(command));

    [HttpGet("orders/my")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> My() => Ok(await _mediator.Send(new GetMyComboOrdersQuery()));

    [HttpGet("orders/my/page")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> MyPage([FromQuery] GetMyComboOrdersPageQuery query) => Ok(await _mediator.Send(query));

    [HttpPost("orders/{id:guid}/cancel")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Cancel(Guid id) => Ok(new { success = await _mediator.Send(new CancelComboOrderCommand(id)) });

    [HttpGet("templates")]
    [Authorize(Roles = "Moderator,SuperAdmin")]
    public async Task<IActionResult> Templates() => Ok(await _mediator.Send(new GetAllComboTemplatesQuery()));

    [HttpPost("templates")]
    [Authorize(Roles = "Moderator,SuperAdmin,VendorOwner")]
    public async Task<IActionResult> SaveTemplate([FromBody] SaveComboTemplateCommand command) => Ok(await _mediator.Send(command));

    [HttpPost("templates/{id:guid}/archive")]
    [Authorize(Roles = "Moderator,SuperAdmin,VendorOwner")]
    public async Task<IActionResult> ArchiveTemplate(Guid id) => Ok(new { success = await _mediator.Send(new ArchiveComboTemplateCommand(id)) });

    public record ComboReviewBody(bool Approve, string? Note, Guid? RevisionId = null);
    [HttpPost("templates/{id:guid}/review")]
    [Authorize(Roles = "Moderator,SuperAdmin")]
    public async Task<IActionResult> ReviewTemplate(Guid id, [FromBody] ComboReviewBody body) => Ok(await _mediator.Send(new ReviewComboTemplateCommand(id, body.Approve, body.Note, body.RevisionId)));

    [HttpPost("templates/{id:guid}/submit")]
    [Authorize(Roles = "Moderator,SuperAdmin,VendorOwner")]
    public async Task<IActionResult> SubmitTemplate(Guid id) => Ok(await _mediator.Send(new SubmitComboTemplateCommand(id)));

    public record ComboActivityBody(bool IsActive);
    [HttpPost("templates/{id:guid}/activity")]
    [Authorize(Roles = "Moderator,SuperAdmin,VendorOwner")]
    public async Task<IActionResult> SetTemplateActivity(Guid id, [FromBody] ComboActivityBody body)
        => Ok(new { success = await _mediator.Send(new SetComboTemplateActivityCommand(id, body.IsActive)) });

    [HttpGet("templates/{id:guid}/preview")]
    [Authorize(Roles = "Moderator,SuperAdmin,VendorOwner")]
    public async Task<IActionResult> PreviewTemplate(Guid id, [FromQuery] Guid? revisionId = null)
        => Ok(await _mediator.Send(new GetComboTemplatePreviewQuery(id, revisionId)));

    [HttpGet("vendor/items")]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> MyItems() => Ok(await _mediator.Send(new GetServiceItemsQuery()));

    [HttpGet("vendor/templates")]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> MyTemplates() => Ok(await _mediator.Send(new GetAllComboTemplatesQuery(true)));

    [HttpPost("vendor/items")]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> SaveItem([FromBody] SaveServiceItemCommand command) => Ok(await _mediator.Send(command));

    [HttpGet("vendor/participation")]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> Participation() => Ok(await _mediator.Send(new GetComboParticipationQuery()));

    [HttpPost("vendor/participation")]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> SaveParticipation([FromBody] SaveComboParticipationCommand command) => Ok(await _mediator.Send(command));

    [HttpGet("items")]
    [Authorize(Roles = "Moderator,SuperAdmin")]
    public async Task<IActionResult> Items() => Ok(await _mediator.Send(new GetServiceItemsQuery(true)));

    [HttpPost("items/{id:guid}/review")]
    [Authorize(Roles = "Moderator,SuperAdmin")]
    public async Task<IActionResult> ReviewItem(Guid id, [FromBody] ServiceItemReviewBody body)
        => Ok(await _mediator.Send(new ReviewServiceItemCommand(id, body.Approve, body.Note, body.ReviewVersion)));

    public record ServiceItemReviewBody(bool Approve, string? Note, string? ReviewVersion);

    [HttpGet("bundle-rules")]
    public async Task<IActionResult> BundleRules() => Ok(await _mediator.Send(new GetBundleRulesQuery()));

    [HttpPost("bundle-rules")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> SaveRules([FromBody] SaveBundleRulesCommand command) => Ok(await _mediator.Send(command));
}
