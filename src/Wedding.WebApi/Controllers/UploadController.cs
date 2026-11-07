using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Planner;
using Wedding.Application.Features.Listings;
using Wedding.Application.Features.Governance;
using Wedding.Infrastructure.Services;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

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
    private VerificationDocumentStorage VerificationStorage => new(_env.ContentRootPath, _configuration["Uploads:VerificationRoot"], _configuration["Uploads:ListingRoot"]);

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

    /// <summary>
    /// Tải lên tệp âm thanh MP3 làm nhạc nền (Chỉ Quản trị viên / Kiểm duyệt viên, tối đa 12 MB)
    /// </summary>
    [HttpPost("music"), Authorize(Roles = "Moderator,SuperAdmin")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(13 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 13 * 1024 * 1024)]
    public async Task<IActionResult> UploadBackgroundMusic(IFormFile file, CancellationToken ct)
    {
        await RequireMusicManagerAsync(ct);
        if (file == null) return BadRequest(new { message = "Vui lòng chọn một file MP3." });
        await using var stream = file.OpenReadStream();
        return Ok(await InvitationAudioStorage.SaveAsync(BackgroundMusicPlaylistStorage.SiteOwnerId, file.FileName, file.ContentType, stream, file.Length, ct));
    }

    /// <summary>
    /// Lấy danh sách phát nhạc nền hệ thống (công khai cho toàn bộ khách truy cập)
    /// </summary>
    [HttpGet("music/playlist"), AllowAnonymous]
    public async Task<IActionResult> GetBackgroundPlaylist(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return Ok(await new BackgroundMusicPlaylistStorage(InvitationAudioStorage).ReadAsync(ct));
    }

    /// <summary>
    /// Cập nhật danh sách phát nhạc nền hệ thống (Chỉ Quản trị viên / Kiểm duyệt viên)
    /// </summary>
    [HttpPut("music/playlist"), Authorize(Roles = "Moderator,SuperAdmin")]
    [RequestSizeLimit(BackgroundMusicPlaylistStorage.MaximumJsonBytes)]
    public async Task<IActionResult> UpdateBackgroundPlaylist([FromBody] System.Text.Json.JsonElement payload, CancellationToken ct)
    {
        await RequireMusicManagerAsync(ct);
        var tracks = await new BackgroundMusicPlaylistStorage(InvitationAudioStorage).SaveAsync(payload, ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(new { success = true, tracks });
    }

    /// <summary>
    /// Lấy cấu hình giao diện & hiệu ứng nền (công khai cho toàn bộ khách truy cập)
    /// </summary>
    [HttpGet("appearance/settings"), AllowAnonymous]
    public async Task<IActionResult> GetAppearanceSettings(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return Ok(await new AppearanceSettingsStorage(InvitationAudioStorage).ReadAsync(ct));
    }

    /// <summary>
    /// Cập nhật cấu hình giao diện & hiệu ứng nền (Chỉ Quản trị viên / Kiểm duyệt viên)
    /// </summary>
    [HttpPut("appearance/settings"), Authorize(Roles = "Moderator,SuperAdmin")]
    [RequestSizeLimit(AppearanceSettingsStorage.MaximumJsonBytes)]
    public async Task<IActionResult> UpdateAppearanceSettings([FromBody] System.Text.Json.JsonElement payload, CancellationToken ct)
    {
        await RequireMusicManagerAsync(ct);
        var settings = await new AppearanceSettingsStorage(InvitationAudioStorage).SaveAsync(payload, ct);
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return Ok(new { success = true, settings });
    }

    private async Task RequireMusicManagerAsync(CancellationToken ct)
    {
        if (_user.UserId is not Guid id || _user.Role is not (nameof(UserRole.Moderator) or nameof(UserRole.SuperAdmin)))
            throw new DomainException("Chỉ quản trị viên hoặc kiểm duyệt viên đang hoạt động được quản lý nhạc chung.");
        var expected = _user.Role == nameof(UserRole.SuperAdmin) ? UserRole.SuperAdmin : UserRole.Moderator;
        if (!await _db.Users.AnyAsync(user => user.Id == id && user.IsActive && user.Role == expected, ct))
            throw new DomainException("Tài khoản quản lý nhạc không còn hoạt động hoặc phiên đăng nhập đã thay đổi.");
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
    [HttpPost("images"), Authorize(Roles = "VendorOwner,Moderator,SuperAdmin")]
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

    /// <summary>
    /// Tải lên một hoặc nhiều tài liệu xác minh (Hỗ trợ PDF, Word .doc/.docx, và Ảnh .jpg/.png)
    /// </summary>
    [HttpPost("documents"), Authorize(Roles = "VendorOwner")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(35 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 35 * 1024 * 1024)]
    public async Task<IActionResult> UploadDocuments(List<IFormFile> files, CancellationToken ct)
    {
        var vendor = await VerificationDocumentAccess.RequireVendorAsync(_db, _user, ct);
        if (files == null || files.Count == 0)
        {
            return BadRequest(new { message = "Vui lòng chọn ít nhất 1 tài liệu (PDF, Word hoặc Ảnh)." });
        }

        if (files.Count > 10)
        {
            return BadRequest(new { message = "Số lượng tài liệu tối đa mỗi lần tải lên là 10 tệp." });
        }

        if (files.Any(file => file.Length is <= 0 or > VerificationDocumentStorage.MaxFileBytes) ||
            files.Sum(file => file.Length) > VerificationDocumentStorage.MaxBatchBytes)
            return BadRequest(new { message = "Tối đa 20 MB mỗi tài liệu và 32 MB cho toàn bộ lần tải lên." });
        var storage = VerificationStorage;
        var items = new List<StoredVerificationDocument>();
        try
        {
            foreach (var file in files)
            {
                await using var stream = file.OpenReadStream();
                items.Add(await storage.SaveAsync(vendor.Id, file.FileName, file.ContentType, stream, file.Length, ct));
            }
        }
        catch
        {
            foreach (var item in items) storage.RemoveNewUpload(vendor.Id, item.Url);
            throw;
        }
        Response.Headers.CacheControl = "no-store";
        return Ok(new { urls = items.Select(item => item.Url), items });
    }

    /// <summary>
    /// Đọc file tài liệu đã tải lên
    /// </summary>
    [HttpGet("/uploads/documents/{vendorId:guid}/{fileName}")]
    [Authorize(Roles = "VendorOwner,Moderator,SuperAdmin")]
    public async Task<IActionResult> GetDocument(Guid vendorId, string fileName, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private,no-store,max-age=0";
        Response.Headers.Pragma = "no-cache";
        if (!await VerificationDocumentAccess.CanReadAsync(_db, _user, vendorId, ct)) return NotFound();
        var path = VerificationStorage.Resolve(vendorId, fileName);
        if (path == null || !System.IO.File.Exists(path)) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
        return PhysicalFile(path, VerificationDocumentStorage.ContentType(Path.GetExtension(path)), fileName, enableRangeProcessing: false);
    }
}

