using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Wedding.Application.Features.Chat.Commands.SendMessage;
using Wedding.Application.Features.Chat.DTOs;
using Wedding.Application.Features.Chat.Queries.GetLeadMessages;
using Wedding.WebApi.Hubs;

namespace Wedding.WebApi.Controllers;

[ApiController]
[Route("api/leads/{leadId:guid}/messages")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IHubContext<ChatHub> _hubContext;

    public ChatController(IMediator mediator, IHubContext<ChatHub> hubContext)
    {
        _mediator = mediator;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Lấy toàn bộ lịch sử tin nhắn trong phòng chat của một yêu cầu tư vấn (Lead).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMessages(Guid leadId)
    {
        var messages = await _mediator.Send(new GetLeadMessagesQuery(leadId));
        return Ok(messages);
    }

    /// <summary>
    /// Gửi tin nhắn hoặc báo giá qua REST API (tự động broadcast qua SignalR tới phòng chat).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SendMessage(Guid leadId, [FromBody] SendMessageRequest request)
    {
        var command = new SendMessageCommand(leadId, request.Content, request.IsQuote, request.QuoteAmount, request.QuoteDescription);
        var messageDto = await _mediator.Send(command);

        // Broadcast realtime qua SignalR tới các client đang mở phòng chat
        var groupName = $"lead-{leadId}";
        await _hubContext.Clients.Group(groupName).SendAsync("ReceiveMessage", messageDto);

        return Ok(messageDto);
    }
}
