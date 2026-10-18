using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using Wedding.Application.Features.Notifications;

namespace Wedding.WebApi.Controllers;
[ApiController, Authorize, Route("api/notifications")]
public class NotificationsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] bool unreadOnly = false)
        => Ok(await mediator.Send(new GetNotificationsQuery(page, pageSize, unreadOnly), ct));
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct)
    { await mediator.Send(new ReadNotificationCommand(id), ct); return NoContent(); }
    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken ct)
    {
        await mediator.Send(new ReadNotificationCommand(null), ct); return NoContent();
    }
}
