using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Planner;
using Wedding.Application.Features.Listings;
using Wedding.Infrastructure.Services;

namespace Wedding.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UploadController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;

    public UploadController(IWebHostEnvironment env, IConfiguration configuration, IApplicationDbContext db, ICurrentUserService user)
    {
        _env = env;
        _configuration = configuration; _db = db; _user = user;
    }
    private InvitationImageStorage InvitationStorage => new(_env.ContentRootPath, _configuration["Uploads:InvitationRoot"]);
    private InvitationAudioStorage InvitationAudioStorage => new(_env.ContentRootPath, _configuration["Uploads:InvitationAudioRoot"]);
    private ListingImageStorage ListingStorage => new(_env.ContentRootPath, _configuration["Uploads:ListingRoot"], _env.WebRootPath);

    [HttpPost("invitation-images"), Authorize(Roles = "Customer")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(9 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 9 * 1024 * 1024)]
    public async Task<IActionResult> UploadInvitationImage(IFormFile file, CancellationToken ct)
    {
        if (file == null) return BadRequest(new { message = "Vui lòng chọn một ảnh JPG, PNG hoặc WebP." });
        var scope = await WeddingAccess.ResolveAsync(_db, _user, ct, write: true);
        await using var stream = file.OpenReadStream();
        return Ok(await InvitationStorage.SaveAsync(scope.OwnerId, file.FileName, file.ContentType, stream, file.Length, ct));
    }

    [HttpPost("invitation-audio"), Authorize(Roles = "Customer")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(13 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 13 * 1024 * 1024)]
    public async Task<IActionResult> UploadInvitationAudio(IFormFile file, CancellationToken ct)
    {
        if (file == null) return BadRequest(new { message = "Vui lòng chọn một file MP3." });
        var scope = await WeddingAccess.ResolveAsync(_db, _user, ct, write: true);
        await using var stream = file.OpenReadStream();
        return Ok(await InvitationAudioStorage.SaveAsync(scope.OwnerId, file.FileName, file.ContentType, stream, file.Length, ct));
    }

    [HttpGet("/uploads/invitation-audio/{ownerId:guid}/{fileName}"), AllowAnonymous]
    public IActionResult GetInvitationAudio(Guid ownerId, string fileName)
    {
        var path = InvitationAudioStorage.Resolve(ownerId, fileName);
        if (path == null || !System.IO.File.Exists(path)) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
        Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        return PhysicalFile(path, "audio/mpeg", enableRangeProcessing: true);
    }

    [HttpGet("/uploads/invitations/{ownerId:guid}/{fileName}"), AllowAnonymous]
    public IActionResult GetInvitationImage(Guid ownerId, string fileName)
    {
        var path = InvitationStorage.Resolve(ownerId, fileName);
        if (path == null || !System.IO.File.Exists(path)) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
        Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        var mime = Path.GetExtension(path) switch { ".png" => "image/png", ".webp" => "image/webp", _ => "image/jpeg" };
        return PhysicalFile(path, mime, enableRangeProcessing: true);
    }

    /// <summary>
    /// Tải lên một hoặc nhiều hình ảnh (Tối đa 10 ảnh, 8 MB mỗi ảnh, hỗ trợ jpg, jpeg, png, webp)
    /// </summary>
    [HttpPost("images"), Authorize(Roles = "VendorOwner")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(81 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 81 * 1024 * 1024)]
    public async Task<IActionResult> UploadImages(List<IFormFile> files, CancellationToken ct)
    {
        // Check DB ownership/status before reading or writing any uploaded data.
        await ListingAccess.RequireImageUploadAccessAsync(_db, _user, ct);
        if (files == null || files.Count == 0)
        {
            return BadRequest(new { message = "Vui lòng chọn ít nhất 1 hình ảnh." });
        }

        if (files.Count > 10)
        {
            return BadRequest(new { message = "Số lượng ảnh tối đa mỗi lần tải lên là 10 bức ảnh." });
        }

        var storage = ListingStorage;
        var urls = new List<string>();
        try
        {
            foreach (var file in files)
            {
                await using var stream = file.OpenReadStream();
                var image = await storage.SaveAsync(file.FileName, file.ContentType, stream, file.Length, ct);
                urls.Add(image.Url);
            }
        }
        catch
        {
            foreach (var url in urls) storage.RemoveNewUpload(url);
            throw;
        }

        return Ok(new { urls });
    }

    /// <summary>
    /// Trả về file ảnh tĩnh đã tải lên
    /// </summary>
    [HttpGet("/uploads/listings/{fileName}")]
    [AllowAnonymous]
    public IActionResult GetListingImage(string fileName)
    {
        var filePath = ListingStorage.Resolve(fileName);
        if (filePath == null || !System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        var ext = Path.GetExtension(filePath);
        var contentType = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/webp"
        };
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
        Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        return PhysicalFile(filePath, contentType, enableRangeProcessing: true);
    }
}

