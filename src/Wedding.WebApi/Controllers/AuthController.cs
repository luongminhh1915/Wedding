using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Wedding.Application.Features.Auth.Commands;
using Wedding.Application.Features.Auth.DTOs;
using Wedding.Application.Features.Auth.Queries;
using Wedding.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;

namespace Wedding.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Đăng ký tài khoản Cô dâu / Chú rể
    /// </summary>
    [HttpPost("register-customer")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterCustomer([FromBody] RegisterCustomerCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Đăng ký đối tác Nhà cung cấp (Vendor Owner - BR-001)
    /// </summary>
    [HttpPost("register-vendor")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterVendor([FromBody] RegisterVendorCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Đăng nhập hệ thống chung (Customer, Vendor, Admin)
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>Gửi email đặt lại mật khẩu mà không tiết lộ tài khoản có tồn tại hay không.</summary>
    [HttpPost("password-reset/request")]
    [ProducesResponseType(typeof(PasswordResetRequestResponse), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> RequestPasswordReset([FromBody] RequestPasswordResetCommand command, CancellationToken ct)
        => Accepted(await _mediator.Send(command, ct));

    /// <summary>Đặt mật khẩu mới bằng liên kết dùng một lần gửi tới email tài khoản.</summary>
    [HttpPost("password-reset/confirm")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return NoContent();
    }

    /// <summary>Đổi mật khẩu và thu hồi mọi phiên đăng nhập của tài khoản hiện tại.</summary>
    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return NoContent();
    }

    /// <summary>Cập nhật số liên hệ của khách hàng, không tự xác minh quyền sở hữu số điện thoại.</summary>
    [Authorize(Roles = "Customer")]
    [HttpPatch("contact")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateContact([FromBody] UpdateContactCommand command, CancellationToken ct)
        => Ok(await _mediator.Send(command, ct));

    [Authorize]
    [HttpPatch("profile")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateAccountProfileCommand command, CancellationToken ct)
        => Ok(await _mediator.Send(command, ct));

    /// <summary>
    /// Gửi mã đăng nhập qua email khi SMTP đã được cấu hình; không trả mã trong API.
    /// </summary>
    [HttpPost("otp/request")]
    public async Task<IActionResult> RequestOtp([FromBody] RequestOtpCommand command) => Ok(await _mediator.Send(command));

    /// <summary>
    /// P1 – Xác thực OTP; tự tạo tài khoản Customer nếu chưa có.
    /// </summary>
    [HttpPost("otp/verify")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpCommand command) => Ok(await _mediator.Send(command));

    /// <summary>
    /// Đăng nhập khách hàng bằng Google ID token đã xác thực chữ ký, ứng dụng và hạn dùng.
    /// </summary>
    [HttpPost("google")]
    public async Task<IActionResult> Google([FromBody] GoogleLoginCommand command) => Ok(await _mediator.Send(command));

    /// <summary>Đăng nhập Customer bằng Facebook access token đã được máy chủ xác minh.</summary>
    [HttpPost("facebook")]
    public async Task<IActionResult> Facebook([FromBody] FacebookLoginCommand command) => Ok(await _mediator.Send(command));

    [HttpGet("providers")]
    public IActionResult Providers([FromServices] IOtpDelivery otp, [FromServices] IPasswordResetDelivery passwordReset,
        [FromServices] IFacebookIdentityVerifier facebook, [FromServices] IConfiguration configuration)
        => Ok(new { emailOtp = otp.EmailEnabled, passwordRecovery = passwordReset.PasswordRecoveryEnabled,
            googleClientId = configuration["Google:ClientId"], facebookAppId = facebook.AppId,
            facebookGraphApiVersion = facebook.GraphApiVersion, smsOtp = false });

    [Authorize(Roles = "Customer"), HttpGet("google/link-status")]
    public async Task<IActionResult> GoogleLinkStatus([FromServices] IApplicationDbContext db, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized();
        var subject = await db.Users.Where(user => user.Id == id).Select(user => user.GoogleSubject).FirstOrDefaultAsync(ct);
        if (subject == null && !await db.Users.AnyAsync(user => user.Id == id, ct)) return Unauthorized();
        return Ok(new { linked = subject != null });
    }

    [Authorize(Roles = "Customer"), HttpPost("google/link")]
    public async Task<IActionResult> LinkGoogle([FromBody] GoogleLoginCommand command, [FromServices] IApplicationDbContext db,
        [FromServices] IGoogleIdentityVerifier verifier, CancellationToken ct)
    {
        var identity = await verifier.VerifyAsync(command.IdToken, ct);
        var id = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var current = await db.Users.FirstAsync(u => u.Id == id, ct);
        if (current.Email != identity.Email || await db.Users.AnyAsync(u => u.Id != id && u.GoogleSubject == identity.Subject, ct)) return BadRequest(new { message = "Google không khớp email tài khoản hoặc đã được liên kết." });
        current.LinkGoogle(identity.Subject); await db.SaveChangesAsync(ct); return NoContent();
    }

    [Authorize(Roles = "Customer"), HttpGet("facebook/link-status")]
    public async Task<IActionResult> FacebookLinkStatus([FromServices] IApplicationDbContext db, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized();
        var subject = await db.Users.Where(user => user.Id == id).Select(user => user.FacebookSubject).FirstOrDefaultAsync(ct);
        if (subject == null && !await db.Users.AnyAsync(user => user.Id == id, ct)) return Unauthorized();
        return Ok(new { linked = subject != null });
    }

    [Authorize(Roles = "Customer"), HttpPost("facebook/link")]
    public async Task<IActionResult> LinkFacebook([FromBody] FacebookLoginCommand command, [FromServices] IApplicationDbContext db,
        [FromServices] IFacebookIdentityVerifier verifier, CancellationToken ct)
    {
        var identity = await verifier.VerifyAsync(command.AccessToken, ct);
        var id = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var current = await db.Users.FirstAsync(user => user.Id == id, ct);
        if ((identity.Email != null && current.Email != identity.Email) ||
            await db.Users.AnyAsync(user => user.Id != id && user.FacebookSubject == identity.Subject, ct))
            return BadRequest(new { message = "Danh tính Facebook không hợp lệ hoặc đã thuộc tài khoản khác." });
        current.LinkFacebook(identity.Subject);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Lấy thông tin tài khoản đang đăng nhập
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        var result = await _mediator.Send(new GetCurrentUserQuery(userId));
        return Ok(result);
    }
}
