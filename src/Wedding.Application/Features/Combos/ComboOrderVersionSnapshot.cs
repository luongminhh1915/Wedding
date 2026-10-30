using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Combos;

internal static class ComboOrderVersionSnapshot
{
    public static async Task<(Guid? RevisionId, string Json)?> CaptureAsync(
        IApplicationDbContext db, ComboOrder order, CancellationToken ct)
    {
        if (!order.ComboTemplateId.HasValue) return null;
        var template = await db.ComboTemplates.Include(candidate => candidate.Items)
            .FirstOrDefaultAsync(candidate => candidate.Id == order.ComboTemplateId.Value, ct)
            ?? throw new DomainException("Combo mẫu không còn tồn tại để đóng băng phiên bản giao dịch.");
        if (template.ModerationStatus != "Approved")
            throw new DomainException("Chỉ có thể lưu giao dịch cho ComboTemplate đã được duyệt.");
        var revision = await db.ComboTemplateRevisions.Where(candidate =>
                candidate.ComboTemplateId == template.Id && candidate.Status == "Approved")
            .OrderByDescending(candidate => candidate.Sequence).FirstOrDefaultAsync(ct);
        if (revision is null)
            throw new DomainException("Combo mẫu chưa có phiên bản đã duyệt để gắn chính xác vào giao dịch mới.");
        var itemSnapshot = order.Items.OrderBy(item => item.Id).Select(item => new
        {
            item.ListingId,
            item.VendorId,
            item.CategoryId,
            item.Title,
            item.Price,
            item.DiscountShare,
            item.ServiceItemId,
            item.SelectionJson,
            item.ComponentSnapshotJson,
            item.CancellationPolicySnapshot,
            item.Quantity,
            item.Unit
        }).ToList();
        var json = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            templateId = template.Id,
            revisionId = revision.Id,
            revisionSequence = revision.Sequence,
            snapshotKind = "ApprovedRevision",
            capturedAtUtc = DateTime.UtcNow,
            revisionSnapshotJson = revision.SnapshotJson,
            order = new
            {
                order.Id,
                order.Code,
                order.Subtotal,
                order.DiscountPercent,
                order.DiscountAmount,
                order.Total,
                order.Region,
                Items = itemSnapshot
            }
        });
        return (revision?.Id, json);
    }
}
