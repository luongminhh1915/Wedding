using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Wedding.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UploadController : ControllerBase
{
    private readonly IWebHostEnvironment _env;

    public UploadController(IWebHostEnvironment env)
    {
        _env = env;
    }

    /// <summary>
    /// Tải lên một hoặc nhiều hình ảnh (Tối đa 10 ảnh, hỗ trợ jpg, jpeg, png, webp)
    /// </summary>
    [HttpPost("images")]
    public async Task<IActionResult> UploadImages([FromForm] List<IFormFile> files)
    {
        if (files == null || files.Count == 0)
        {
            return BadRequest(new { message = "Vui lòng chọn ít nhất 1 hình ảnh." });
        }

        if (files.Count > 10)
        {
            return BadRequest(new { message = "Số lượng ảnh tối đa mỗi lần tải lên là 10 bức ảnh." });
        }

        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadsFolder = Path.Combine(webRoot, "uploads", "listings");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var urls = new List<string>();

        foreach (var file in files)
        {
            if (file.Length == 0) continue;

            if (file.Length > 10 * 1024 * 1024)
            {
                return BadRequest(new { message = $"Ảnh {file.FileName} vượt quá dung lượng tối đa cho phép (10MB)." });
            }

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                return BadRequest(new { message = $"Định dạng tệp {file.FileName} không hợp lệ. Chỉ chấp nhận các định dạng: .jpg, .jpeg, .png, .webp." });
            }

            var uniqueFileName = $"{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            urls.Add($"/uploads/listings/{uniqueFileName}");
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
        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var filePath = Path.Combine(webRoot, "uploads", "listings", fileName);
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var contentType = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };

        return PhysicalFile(filePath, contentType);
    }
}

