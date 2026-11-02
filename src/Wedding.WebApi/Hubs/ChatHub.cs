using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Wedding.Application.Features.Chat.Commands.SendMessage;
using Wedding.Infrastructure.Services;

namespace Wedding.WebApi.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IMediator _mediator;
    private readonly ILogger<ChatHub> _logger;
    private readonly ChatRoomAccess _roomAccess;
    private const string AccessDenied = "Không thể truy cập cuộc trò chuyện này. Vui lòng đăng nhập lại hoặc kiểm tra quyền truy cập.";

    public ChatHub(IMediator mediator, ILogger<ChatHub> logger, ChatRoomAccess roomAccess)
    {
        _mediator = mediator;
        _logger = logger;
        _roomAccess = roomAccess;
    }

    /// <summary>
    /// Tham gia phòng chat của một Lead cụ thể.
    /// </summary>
    public async Task JoinLeadRoom(string leadId)
    {
        var membership = await RequireMembership(leadId, forSending: false);
        await Groups.AddToGroupAsync(Context.ConnectionId, membership.GroupName, Context.ConnectionAborted);
        _logger.LogInformation("Connection {ConnectionId} đã tham gia phòng chat được cấp quyền", Context.ConnectionId);
    }

    /// <summary>
    /// Rời khỏi phòng chat của một Lead.
    /// </summary>
    public async Task LeaveLeadRoom(string leadId)
    {
        var membership = await RequireMembership(leadId, forSending: false);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, membership.GroupName, Context.ConnectionAborted);
        _logger.LogInformation("Connection {ConnectionId} đã rời phòng chat được cấp quyền", Context.ConnectionId);
    }

    /// <summary>
    /// Gửi tin nhắn trao đổi hoặc báo giá trực tiếp trong phòng chat.
    /// </summary>
    public async Task SendMessage(Guid leadId, string content, bool isQuote = false, decimal? quoteAmount = null, string? quoteDescription = null)
    {
        var membership = await RequireMembership(leadId.ToString(), forSending: true);
        if (isQuote && !membership.IsVendor) throw new HubException(AccessDenied);
        var command = new SendMessageCommand(leadId, content, isQuote, quoteAmount, quoteDescription);
        var messageDto = await _mediator.Send(command, Context.ConnectionAborted);

        var groups = await _roomAccess.GetCurrentRecipientGroupsAsync(leadId, Context.ConnectionAborted);
        await Clients.Groups(groups).SendAsync("ReceiveMessage", messageDto, Context.ConnectionAborted);
    }

    private async Task<ChatRoomMembership> RequireMembership(string leadId, bool forSending)
    {
        if (!Guid.TryParse(leadId, out var parsed)) throw new HubException(AccessDenied);
        return await _roomAccess.GetMembershipAsync(Context.User, parsed, forSending, Context.ConnectionAborted)
            ?? throw new HubException(AccessDenied);
    }
}
