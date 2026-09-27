using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Wedding.Application.Features.Chat.Commands.SendMessage;

namespace Wedding.WebApi.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IMediator _mediator;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IMediator mediator, ILogger<ChatHub> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Tham gia phòng chat của một Lead cụ thể.
    /// </summary>
    public async Task JoinLeadRoom(string leadId)
    {
        var groupName = $"lead-{leadId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Connection {ConnectionId} đã tham gia phòng {GroupName}", Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Rời khỏi phòng chat của một Lead.
    /// </summary>
    public async Task LeaveLeadRoom(string leadId)
    {
        var groupName = $"lead-{leadId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Connection {ConnectionId} đã rời phòng {GroupName}", Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Gửi tin nhắn trao đổi hoặc báo giá trực tiếp trong phòng chat.
    /// </summary>
    public async Task SendMessage(Guid leadId, string content, bool isQuote = false, decimal? quoteAmount = null, string? quoteDescription = null)
    {
        var command = new SendMessageCommand(leadId, content, isQuote, quoteAmount, quoteDescription);
        var messageDto = await _mediator.Send(command);

        var groupName = $"lead-{leadId}";
        // Bắn tin nhắn realtime tới tất cả client đang có mặt trong phòng chat này
        await Clients.Group(groupName).SendAsync("ReceiveMessage", messageDto);
    }
}
