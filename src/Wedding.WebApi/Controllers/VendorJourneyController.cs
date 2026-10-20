using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedding.Application.Features.VendorJourney;

namespace Wedding.WebApi.Controllers;

[ApiController]
[Route("api/vendor-journey")]
[Authorize(Roles = "VendorOwner")]
public class VendorJourneyController(IMediator mediator) : ControllerBase
{
    /// <summary>Read-only milestones for the currently authenticated active shop.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(VendorJourneySummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetVendorJourneyQuery(), cancellationToken));
}
