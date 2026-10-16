using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;

namespace Wedding.WebApi.Controllers;
[ApiController, Authorize, Route("api/notifications")]
public class NotificationsController(IApplicationDbContext db, ICurrentUserService user) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await db.Notifications.AsNoTracking().Where(n => n.UserId == user.UserId)
        .OrderByDescending(n => n.CreatedAt).Take(50).Select(n => new { n.Id, n.Message, n.CreatedAt, n.ReadAt }).ToListAsync(ct));
    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken ct)
    {
        var notifications = await db.Notifications.Where(n => n.UserId == user.UserId && n.ReadAt == null).ToListAsync(ct);
        foreach (var notification in notifications) notification.Read();
        await db.SaveChangesAsync(ct); return NoContent();
    }
}
