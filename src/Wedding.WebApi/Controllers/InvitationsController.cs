using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedding.Application.Features.Invitations.Commands.SaveInvitation;
using Wedding.Application.Features.Invitations.Commands.SubmitRsvp;
using Wedding.Application.Features.Invitations.Queries.GetInvitationRsvps;
using Wedding.Application.Features.Invitations.Queries.GetMyInvitation;
using Wedding.Application.Features.Invitations.Queries.GetPublicInvitation;

namespace Wedding.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InvitationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public InvitationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// [Customer] Lấy thông tin thiệp cưới của chính mình (bao gồm thống kê RSVP).
    /// </summary>
    [HttpGet("my-invitation")]
    [Authorize]
    public async Task<IActionResult> GetMyInvitation()
    {
        var result = await _mediator.Send(new GetMyInvitationQuery());
        return Ok(result);
    }

    /// <summary>
    /// [Customer] Tạo mới hoặc cập nhật thiệp cưới điện tử (E-Invitation Builder).
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> SaveInvitation([FromBody] SaveInvitationCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// [Customer] Xem danh sách khách mời đã phản hồi RSVP.
    /// </summary>
    [HttpGet("my-rsvps")]
    [Authorize]
    public async Task<IActionResult> GetMyRsvps([FromQuery] string? status)
    {
        var result = await _mediator.Send(new GetInvitationRsvpsQuery(status));
        return Ok(result);
    }

    /// <summary>
    /// [Public API] Xem thiệp cưới điện tử công khai theo đường dẫn slug (không yêu cầu đăng nhập).
    /// </summary>
    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicInvitation(string slug)
    {
        var result = await _mediator.Send(new GetPublicInvitationQuery(slug));
        return Ok(result);
    }

    /// <summary>
    /// [Public API] Khách mời điểm danh phản hồi RSVP 1-chạm & gửi lời chúc (không yêu cầu đăng nhập).
    /// </summary>
    [HttpPost("{slug}/rsvp")]
    [AllowAnonymous]
    public async Task<IActionResult> SubmitRsvp(string slug, [FromBody] SubmitRsvpRequest request)
    {
        var command = new SubmitRsvpCommand(
            Slug: slug,
            GuestName: request.GuestName,
            PhoneNumber: request.PhoneNumber,
            Status: request.Status,
            CompanionCount: request.CompanionCount,
            Wishes: request.Wishes,
            DietaryPreference: request.DietaryPreference
        );

        var result = await _mediator.Send(command);
        return Ok(result);
    }
}

public record SubmitRsvpRequest(
    string GuestName,
    string? PhoneNumber,
    string Status,
    int CompanionCount = 0,
    string? Wishes = null,
    string? DietaryPreference = null
);
