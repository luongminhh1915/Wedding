using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.PrivateServiceAgreements;

/// <summary>Never copy a whole combo amendment into each component contract or change its money.
/// Require a fresh, explicitly reviewed quote/contract after an accepted content change.</summary>
public static class PrivateAgreementContractHandoff
{
    public static async Task EnsureReviewedAsync(IApplicationDbContext db, Lead lead, DateTime documentCreatedAt, CancellationToken ct)
    {
        if (!lead.ComboOrderId.HasValue) return;
        var proposals = await db.PrivateServiceAgreements.AsNoTracking().Where(x => x.Kind == "combo" && x.TargetId == lead.ComboOrderId &&
            x.CustomerId == lead.CustomerId && x.VendorId == lead.VendorId).ToListAsync(ct);
        var reason = ReviewBlockReason(lead, documentCreatedAt, proposals);
        if (reason != null) throw new DomainException(reason);
    }

    /// <summary>Shared command/read guard. Recheck scope even when callers batch-load proposals.</summary>
    public static string? ReviewBlockReason(Lead lead, DateTime documentCreatedAt, IEnumerable<PrivateServiceAgreement> proposals)
    {
        if (!lead.ComboOrderId.HasValue) return null;
        var scoped = proposals.Where(x => x.Kind == "combo" && x.TargetId == lead.ComboOrderId &&
            x.CustomerId == lead.CustomerId && x.VendorId == lead.VendorId).ToList();
        if (scoped.Any(x => x.Status == "PendingCustomer"))
            return "Combo còn đề xuất nội dung đang chờ. Khách cần phản hồi hoặc NCC rút đề xuất trước khi chốt báo giá/hợp đồng.";
        if (scoped.Any(x => x.Status == "Accepted" && x.RespondedAt > documentCreatedAt))
            return "Nội dung combo đã được điều chỉnh sau khi lập hồ sơ này. NCC cần gửi báo giá/hợp đồng mới để khách đối chiếu và xác nhận; không tự chuyển nội dung hoặc tính lại tiền của hồ sơ cũ.";
        return null;
    }
}
