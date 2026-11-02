using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Combos;

/// <summary>Every new combo belongs to exactly one vendor. Legacy orders are read, never rewritten.</summary>
public static class ComboVendorPolicy
{
    public const string SingleVendorMessage = "Mỗi combo chỉ được chứa dịch vụ của một nhà cung cấp. Vui lòng chọn combo riêng cho NCC khác.";
    public static Guid RequireOneVendor(IEnumerable<Guid> vendorIds)
    {
        var ids = vendorIds.Distinct().ToList();
        if (ids.Count != 1) throw new DomainException(SingleVendorMessage);
        return ids[0];
    }
    public static bool TryAllowedIds(ComboTemplateItem slot, out List<Guid> ids)
    {
        try { ids = JsonSerializer.Deserialize<List<Guid>>(slot.AllowedItemIdsJson) ?? new(); return true; }
        catch (JsonException) { ids = new(); return false; }
    }
    public static async Task<bool> IsOwnedTemplateAsync(IApplicationDbContext db, ComboTemplate template, CancellationToken ct)
    {
        if (!template.OwnerVendorId.HasValue || template.Items.Count == 0 || template.Items.Any(i => !i.ServiceItemId.HasValue)) return false;
        var ids = new HashSet<Guid>();
        foreach (var slot in template.Items)
        {
            if (!TryAllowedIds(slot, out var allowed)) return false;
            ids.Add(slot.ServiceItemId!.Value); ids.UnionWith(allowed);
        }
        var items = await db.ServiceItems.AsNoTracking().Where(i => ids.Contains(i.Id)).Select(i => new { i.Id, i.VendorId, i.CategoryId }).ToDictionaryAsync(i => i.Id, ct);
        return template.Items.All(slot => TryAllowedIds(slot, out var allowed) && allowed.Append(slot.ServiceItemId!.Value).All(id =>
            items.TryGetValue(id, out var item) && item.VendorId == template.OwnerVendorId && item.CategoryId == slot.CategoryId));
    }
}
