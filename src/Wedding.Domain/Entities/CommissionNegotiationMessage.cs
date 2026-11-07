using Wedding.Domain.Common;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class CommissionNegotiationMessage : BaseEntity
{
    public Guid NegotiationRequestId { get; private set; }
    public Guid SenderId { get; private set; }
    public string SenderRole { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public decimal? ProposedRateSnapshot { get; private set; }
    public string? AttachmentUrl { get; private set; }

    public CommissionNegotiationRequest NegotiationRequest { get; private set; } = null!;
    public User Sender { get; private set; } = null!;

    private CommissionNegotiationMessage() { }

    public static CommissionNegotiationMessage Create(
        Guid negotiationRequestId,
        Guid senderId,
        string senderRole,
        string message,
        decimal? proposedRateSnapshot = null,
        string? attachmentUrl = null)
    {
        if (negotiationRequestId == Guid.Empty) throw new DomainException("Mã yêu cầu đàm phán không hợp lệ.");
        if (senderId == Guid.Empty) throw new DomainException("Người gửi không hợp lệ.");
        if (string.IsNullOrWhiteSpace(senderRole)) throw new DomainException("Vai trò người gửi không hợp lệ.");
        if (string.IsNullOrWhiteSpace(message) || message.Trim().Length > 4000)
            throw new DomainException("Nội dung tin nhắn đàm phán phải từ 1 đến 4000 ký tự.");
        if (proposedRateSnapshot.HasValue && (proposedRateSnapshot.Value is < 0.03m or > 0.12m))
            throw new DomainException("Tỷ lệ hoa hồng phải nằm trong khung 3–12%.");
        if (!string.IsNullOrWhiteSpace(attachmentUrl) && attachmentUrl.Trim().Length > 500)
            throw new DomainException("Đường dẫn tệp đính kèm tối đa 500 ký tự.");

        return new CommissionNegotiationMessage
        {
            NegotiationRequestId = negotiationRequestId,
            SenderId = senderId,
            SenderRole = senderRole.Trim(),
            Message = message.Trim(),
            ProposedRateSnapshot = proposedRateSnapshot.HasValue ? decimal.Round(proposedRateSnapshot.Value, 4) : null,
            AttachmentUrl = string.IsNullOrWhiteSpace(attachmentUrl) ? null : attachmentUrl.Trim()
        };
    }
}

