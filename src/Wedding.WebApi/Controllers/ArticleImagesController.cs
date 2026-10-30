using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Governance;
using Wedding.Infrastructure.Services;

namespace Wedding.WebApi.Controllers;

[ApiController]
public sealed class ArticleImagesController(IWebHostEnvironment env, IConfiguration configuration,
    IApplicationDbContext db, ICurrentUserService user) : ControllerBase
{
    private ArticleImageStorage Storage => new(env.ContentRootPath, configuration["Uploads:ArticleRoot"], configuration["Uploads:ListingRoot"]);

    [HttpPost("api/admin/articles/images"), Authorize(Roles = "SuperAdmin,Moderator")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(9 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 9 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        await ArticleCmsAccess.RequireAsync(db, user, ct);
        if (file == null) return BadRequest(new { message = "Vui lòng chọn một ảnh JPG, PNG hoặc WebP." });
        await using var stream = file.OpenReadStream();
        var result = await Storage.SaveAsync(file.FileName, file.ContentType, stream, file.Length, ct);
        return Ok(new { url = result.Url });
    }

    [HttpGet("/uploads/articles/{fileName}"), AllowAnonymous]
    public IActionResult Get(string fileName)
    {
        var path = Storage.Resolve(fileName);
        if (path == null || !System.IO.File.Exists(path)) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
        Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        var mime = Path.GetExtension(path) switch { ".png" => "image/png", ".webp" => "image/webp", _ => "image/jpeg" };
        return PhysicalFile(path, mime, enableRangeProcessing: true);
    }
}
