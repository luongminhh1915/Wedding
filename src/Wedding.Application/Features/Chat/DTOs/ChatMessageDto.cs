namespace Wedding.Application.Features.Chat.DTOs;

public record ChatMessageDto(
    Guid Id,
    Guid LeadId,
    Guid SenderId,
    string SenderName,
    bool IsFromCustomer,
    string Content,
    bool IsQuote,
    decimal? QuoteAmount,
    string? QuoteDescription,
    bool IsRead,
    DateTime CreatedAt
);

public record SendMessageRequest(
    string Content,
    bool IsQuote = false,
    decimal? QuoteAmount = null,
    string? QuoteDescription = null
);
