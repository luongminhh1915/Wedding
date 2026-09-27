using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Chat.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Chat.Commands.SendMessage;

public record SendMessageCommand(
    Guid LeadId,
    string Content,
    bool IsQuote = false,
    decimal? QuoteAmount = null,
    string? QuoteDescription = null
) : IRequest<ChatMessageDto>;

public class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageCommandValidator()
    {
        RuleFor(x => x.LeadId).NotEmpty().WithMessage("LeadId không được để trống.");
        RuleFor(x => x.Content).NotEmpty().WithMessage("Nội dung tin nhắn không được để trống.").MaximumLength(2000);
        RuleFor(x => x.QuoteAmount).GreaterThan(0).When(x => x.IsQuote).WithMessage("Số tiền báo giá phải lớn hơn 0.");
        RuleFor(x => x.QuoteDescription).MaximumLength(500).When(x => !string.IsNullOrEmpty(x.QuoteDescription));
    }
}

public class SendMessageCommandHandler : IRequestHandler<SendMessageCommand, ChatMessageDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SendMessageCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ChatMessageDto> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để gửi tin nhắn.");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Người dùng không tồn tại.");

        var lead = await _context.Leads
            .Include(l => l.Vendor)
            .FirstOrDefaultAsync(l => l.Id == request.LeadId, cancellationToken)
            ?? throw new NotFoundException("Yêu cầu tư vấn không tồn tại.");

        // Kiểm tra quyền: Chỉ Customer của Lead hoặc Vendor của Lead mới có quyền gửi tin nhắn
        var isCustomer = lead.CustomerId == userId;
        var isVendor = lead.Vendor.UserId == userId;

        if (!isCustomer && !isVendor)
        {
            throw new DomainException("Bạn không phải thành viên của cuộc trò chuyện này.");
        }

        // Tạo tin nhắn (tin nhắn thường hoặc tin báo giá)
        ChatMessage message;
        if (request.IsQuote && request.QuoteAmount.HasValue && request.QuoteAmount > 0)
        {
            if (!isVendor)
                throw new DomainException("Chỉ nhà cung cấp mới có quyền gửi báo giá.");

            message = ChatMessage.CreateQuote(
                leadId: lead.Id,
                senderId: userId,
                content: request.Content,
                quoteAmount: request.QuoteAmount.Value,
                quoteDescription: request.QuoteDescription
            );
        }
        else
        {
            message = ChatMessage.Create(
                leadId: lead.Id,
                senderId: userId,
                content: request.Content
            );
        }

        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);

        return new ChatMessageDto(
            Id: message.Id,
            LeadId: message.LeadId,
            SenderId: message.SenderId,
            SenderName: user.FullName,
            IsFromCustomer: isCustomer,
            Content: message.Content,
            IsQuote: message.IsQuote,
            QuoteAmount: message.QuoteAmount,
            QuoteDescription: message.QuoteDescription,
            IsRead: message.IsRead,
            CreatedAt: message.CreatedAt
        );
    }
}
