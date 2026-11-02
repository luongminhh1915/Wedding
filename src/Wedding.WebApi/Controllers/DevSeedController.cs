using Microsoft.AspNetCore.Mvc;

namespace Wedding.WebApi.Controllers;

/// <summary>Retired legacy endpoint. Database initialization must not be remotely triggered.</summary>
[ApiController]
[Route("api/[controller]")]
public class DevSeedController : ControllerBase
{
    [HttpPost("seed")]
    public IActionResult SeedDatabase() => StatusCode(StatusCodes.Status410Gone, new
    {
        message = "API nạp dữ liệu mẫu đã ngừng hoạt động. Không có dữ liệu hoặc mật khẩu nào được thay đổi."
    });
}
