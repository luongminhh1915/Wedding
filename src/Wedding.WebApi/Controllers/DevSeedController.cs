using Microsoft.AspNetCore.Mvc;
using Wedding.Infrastructure.Persistence;

namespace Wedding.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevSeedController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;

    public DevSeedController(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    [HttpPost("seed")]
    public async Task<IActionResult> SeedDatabase()
    {
        await DbInitializer.SeedAsync(_serviceProvider);
        return Ok(new
        {
            success = true,
            message = "Đã khởi tạo và cập nhật dữ liệu mẫu thành công! Tất cả tài khoản có mật khẩu: 123456",
            testAccounts = new[]
            {
                new { role = "Customer (Dâu rể)", email = "customer@wedding.com", password = "123456", name = "Nguyễn Văn An & Trần Thị Bình" },
                new { role = "Vendor Owner (Studio/Bridal)", email = "vendor.studio@wedding.com", password = "123456", name = "Mai Wedding Studio" },
                new { role = "Vendor Owner (Tiệc cưới)", email = "vendor.palace@wedding.com", password = "123456", name = "White Palace Convention" },
                new { role = "Vendor Owner (Decor)", email = "vendor.decor@wedding.com", password = "123456", name = "Dream Wedding Decor" },
                new { role = "Moderator (Kiểm duyệt)", email = "moderator@wedding.com", password = "123456", name = "Ban Kiểm Duyệt (Moderator)" },
                new { role = "Finance (Kế toán)", email = "finance@wedding.com", password = "123456", name = "Bộ Phận Tài Chính (Finance)" },
                new { role = "SuperAdmin (Quản trị)", email = "admin@wedding.com", password = "123456", name = "Quản Trị Viên Hệ Thống" }
            }
        });
    }
}
