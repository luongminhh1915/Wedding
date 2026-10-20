using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;

namespace Wedding.WebApi.Controllers;

[ApiController, Route("api/invitation-templates"), AllowAnonymous]
public class InvitationTemplatesController(IApplicationDbContext db) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> Get(CancellationToken ct) => Ok(await db.InvitationTemplates.AsNoTracking().OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
        .Select(x => new { x.Id, x.Key, x.Name, x.Category, x.PrimaryColor, x.BackgroundColor, x.Motif, x.IsPremium }).ToListAsync(ct));
}
