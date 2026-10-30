using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedding.Application.Features.ModerationQueue;

namespace Wedding.WebApi.Controllers;

[ApiController]
[Route("api/moderation-queue")]
[Authorize(Roles = "Moderator,SuperAdmin")]
public class ModerationQueueController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ModerationQueuePage>> Page([FromQuery] GetModerationQueueQuery query, CancellationToken ct)
        => Ok(await mediator.Send(query, ct));

    [HttpGet("{type}/{id:guid}")]
    public async Task<ActionResult<ModerationQueueDetail>> Detail(string type, Guid id, [FromQuery] Guid? revisionId, CancellationToken ct)
        => Ok(await mediator.Send(new GetModerationQueueDetailQuery(type, id, revisionId), ct));
}
