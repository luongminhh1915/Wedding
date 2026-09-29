using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedding.Application.Features.Users.Commands.UpdateUserStatus;
using Wedding.Application.Features.Users.DTOs;
using Wedding.Application.Features.Users.Queries.GetUsers;

namespace Wedding.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin,Moderator")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// [Admin] Lấy danh sách tất cả người dùng trong hệ thống.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<UserManagementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers()
    {
        var result = await _mediator.Send(new GetUsersQuery());
        return Ok(result);
    }

    /// <summary>
    /// [Admin] Cập nhật trạng thái người dùng (Khóa / Kích hoạt).
    /// </summary>
    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateUserStatus(Guid id, [FromBody] UpdateUserStatusRequest request)
    {
        await _mediator.Send(new UpdateUserStatusCommand(id, request.IsActive));
        return Ok(new { message = request.IsActive ? "Kích hoạt tài khoản thành công." : "Đã khóa tài khoản thành công." });
    }
}

public record UpdateUserStatusRequest(bool IsActive);
