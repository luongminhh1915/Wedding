using Wedding.Domain.Common;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class ChatMessage : BaseEntity
{
    public Guid LeadId { get; private set; }
    public Guid SenderId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public bool IsQuote { get; private set; } = false;
    public decimal? QuoteAmount { get; private set; }
    public string? QuoteDescription { get; private set; }
    public bool IsRead { get; private set; } = false;

    // Navigation Properties
    public Lead Lead { get; private set; } = null!;
    public User Sender { get; private set; } = null!;

    private ChatMessage() { } // Dành cho EF Core

    public static ChatMessage Create(Guid leadId, Guid senderId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new DomainException("Nội dung tin nhắn không được để trống.");

        return new ChatMessage
        {
            Id = Guid.NewGuid(),
            LeadId = leadId,
            SenderId = senderId,
            Content = content.Trim(),
            IsQuote = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static ChatMessage CreateQuote(Guid leadId, Guid senderId, string content, decimal quoteAmount, string? quoteDescription = null)
    {
        if (quoteAmount <= 0)
            throw new DomainException("Số tiền báo giá phải lớn hơn 0.");

        return new ChatMessage
        {
            Id = Guid.NewGuid(),
            LeadId = leadId,
            SenderId = senderId,
            Content = string.IsNullOrWhiteSpace(content) ? $"Báo giá dịch vụ: {quoteAmount:N0} đ" : content.Trim(),
            IsQuote = true,
            QuoteAmount = quoteAmount,
            QuoteDescription = quoteDescription?.Trim(),
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkAsRead()
    {
        IsRead = true;
        SetUpdated();
    }
}
