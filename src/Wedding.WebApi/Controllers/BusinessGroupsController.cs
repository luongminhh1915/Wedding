using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedding.Application.Features.BusinessGroups;

namespace Wedding.WebApi.Controllers;

[ApiController]
[Route("api/business-groups")]
public class BusinessGroupsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await mediator.Send(new GetBusinessGroupsQuery(), ct));

    [HttpGet("my")]
    [Authorize(Roles = "VendorOwner")]
    public async Task<IActionResult> My(CancellationToken ct)
        => Ok(await mediator.Send(new GetMyBusinessGroupsQuery(), ct));

    [HttpGet("vendors/{vendorId:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<VendorBusinessGroupsDto>> VendorGroups(Guid vendorId, CancellationToken ct)
        => Ok(await mediator.Send(new GetVendorBusinessGroupsQuery(vendorId), ct));

    public record AssignGroupsBody(List<Guid>? BusinessGroupIds, string? Reason);

    [HttpPost("vendors/{vendorId:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<VendorBusinessGroupsDto>> AssignGroups(Guid vendorId, [FromBody] AssignGroupsBody body, CancellationToken ct)
        => Ok(await mediator.Send(new AssignVendorBusinessGroupsCommand(vendorId, body.BusinessGroupIds, body.Reason), ct));
}
