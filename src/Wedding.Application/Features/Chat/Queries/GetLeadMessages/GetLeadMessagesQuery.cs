using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Chat.DTOs;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Chat.Queries.GetLeadMessages;

public record GetLeadMessagesQuery(Guid LeadId) : IRequest<List<ChatMessageDto>>;

public class GetLeadMessagesQueryHandler : IRequestHandler<GetLeadMessagesQuery, List<ChatMessageDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetLeadMessagesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<ChatMessageDto>> Handle(GetLeadMessagesQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để xem tin nhắn.");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Người dùng không tồn tại.");

        var lead = await _context.Leads
            .Include(l => l.Vendor)
            .FirstOrDefaultAsync(l => l.Id == request.LeadId, cancellationToken)
            ?? throw new NotFoundException("Yêu cầu tư vấn không tồn tại.");

        var isCustomer = lead.CustomerId == userId;
        var isVendor = lead.Vendor.UserId == userId;
        var isAdminOrMod = user.Role == UserRole.SuperAdmin || user.Role == UserRole.Moderator;

        if (!isCustomer && !isVendor && !isAdminOrMod)
        {
            throw new DomainException("Bạn không có quyền truy cập lịch sử tin nhắn của yêu cầu tư vấn này.");
        }

        var messages = await _context.ChatMessages
            .Include(m => m.Sender)
            .Where(m => m.LeadId == request.LeadId)
            .OrderBy(m => m.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return messages.Select(m => new ChatMessageDto(
            Id: m.Id,
            LeadId: m.LeadId,
            SenderId: m.SenderId,
            SenderName: m.Sender.FullName,
            IsFromCustomer: m.SenderId == lead.CustomerId,
            Content: m.Content,
            IsQuote: m.IsQuote,
            QuoteAmount: m.QuoteAmount,
            QuoteDescription: m.QuoteDescription,
            IsRead: m.IsRead,
            CreatedAt: m.CreatedAt
        )).ToList();
    }
}
