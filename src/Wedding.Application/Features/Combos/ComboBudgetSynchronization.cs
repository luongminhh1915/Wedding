using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Planner;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;

namespace Wedding.Application.Features.Combos;

public record ComboBudgetProjection(WeddingScope Scope, WeddingProfile? Profile, decimal ReservedBeforeCart,
    decimal ReplacedSeedEstimate, List<string> Warnings);
public record ComboBudgetOrigin(bool CreatedNew, decimal PlannedCost, string? Notes, string? PlanTemplateKey,
    Guid? CategoryId, DateTime? ManagedUpdatedAt);
public record PersistedComboSelection(Guid ServiceItemId, List<Guid>? VariantIds, List<Guid>? AddOnIds, int Hours,
    ComboBudgetOrigin? BudgetOrigin = null);

/// <summary>Only explicit, untouched wizard provenance grants permission to replace a budget estimate.</summary>
public static class ComboBudgetSynchronization
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed record PricingUser(Guid Id) : ICurrentUserService
    { public Guid? UserId => Id; public string? Email => null; public string? Role => "Customer"; }
    private static decimal Reserve(BudgetItem b) => Math.Max(b.PlannedCost, b.ActualCost);
    public static string Serialize(ComboSelection selection, ComboBudgetOrigin? origin = null)
        => JsonSerializer.Serialize(new PersistedComboSelection(selection.ServiceItemId, selection.VariantIds, selection.AddOnIds, selection.Hours, origin), Json);
    private static ComboBudgetOrigin? Origin(ComboOrderItem item)
    {
        try { return JsonSerializer.Deserialize<PersistedComboSelection>(item.SelectionJson, Json)?.BudgetOrigin; }
        catch (JsonException) { return null; }
    }
    private static bool IsUnmodified(BudgetItem row, ComboBudgetOrigin origin)
        => row.ActualCost == 0 && !row.IsPaid && row.ContractId == null && row.UpdatedAt?.Ticks == origin.ManagedUpdatedAt?.Ticks;

    private static async Task<(List<BudgetItem> Rows, HashSet<Guid> Bound, Dictionary<Guid, string> Slugs)> InputsAsync(
        IApplicationDbContext db, WeddingScope scope, CancellationToken ct)
    {
        var rows = await db.BudgetItems.Where(b => b.CustomerId == scope.OwnerId &&
            (!scope.ProfileId.HasValue || b.WeddingProfileId == null || b.WeddingProfileId == scope.ProfileId)).ToListAsync(ct);
        var ids = rows.Select(b => b.Id).ToList();
        var bound = await db.ComboOrderItems.Where(i => i.BudgetItemId.HasValue && ids.Contains(i.BudgetItemId.Value) &&
            i.ComboOrder.Status != ComboOrderStatus.Cancelled && i.ComboOrder.Status != ComboOrderStatus.Expired)
            .Select(i => i.BudgetItemId!.Value).ToListAsync(ct);
        var slugs = await db.Categories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Slug, ct);
        return (rows, bound.ToHashSet(), slugs);
    }
    private static BudgetItem? Eligible(IEnumerable<BudgetItem> rows, HashSet<Guid> bound, Guid categoryId, string slug)
        => rows.Where(b => b.ActualCost == 0 && !b.IsPaid && b.ContractId == null && !bound.Contains(b.Id) &&
            PlannerBudgetSeeds.MatchesCategory(b, categoryId, slug)).OrderBy(b => b.CreatedAt).ThenBy(b => b.Id).FirstOrDefault();

    public static async Task<ComboBudgetProjection> ProjectAsync(IApplicationDbContext db, Guid customerId, IReadOnlyList<Guid> categories, CancellationToken ct, Guid? revisionOfId = null)
    {
        var scope = await WeddingAccess.ResolveAsync(db, new PricingUser(customerId), ct);
        var profile = scope.ProfileId.HasValue ? await db.WeddingProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == scope.ProfileId, ct) : null;
        var (rows, bound, slugs) = await InputsAsync(db, scope, ct);
        decimal replacement = 0; var warnings = new List<string>();
        var restored = new Dictionary<Guid, decimal>(); var removed = new HashSet<Guid>(); decimal releaseDelta = 0;
        if (revisionOfId.HasValue)
        {
            var source = await db.ComboOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == revisionOfId && o.CustomerId == customerId, ct)
                ?? throw new Wedding.Domain.Exceptions.NotFoundException("Không tìm thấy combo gốc của bạn.");
            await Wedding.Application.Features.CustomerOrders.CustomerOrderJourney.EnsureCanReplaceComboAsync(db, source, ct);
            foreach (var line in source.Items.Where(i => i.BudgetItemId.HasValue))
            {
                var row = rows.FirstOrDefault(b => b.Id == line.BudgetItemId); var origin = Origin(line);
                if (row == null || origin == null || !IsUnmodified(row, origin) || row.PlannedCost != line.Price - line.DiscountShare)
                {
                    warnings.Add("Khoản ngân sách combo gốc đã được chỉnh sửa/ghi thực chi hoặc thiếu dấu vết quản lý; được giữ nguyên, phiên bản mới có thể tạo khoản dự toán riêng. Hãy đối chiếu Planner.");
                    continue;
                }
                bound.Remove(row.Id);
                if (origin.CreatedNew) { removed.Add(row.Id); releaseDelta -= Reserve(row); }
                else { restored[row.Id] = origin.PlannedCost; releaseDelta += origin.PlannedCost - Reserve(row); }
            }
            warnings.Add("Dự phóng thay thế phần dự toán tự động chưa chỉnh sửa của combo gốc. Chỉ khi gửi phiên bản mới thành công yêu cầu cũ mới dừng; lưu nháp không thay đổi ngân sách.");
        }
        foreach (var categoryId in categories.Distinct())
        {
            var slug = slugs.GetValueOrDefault(categoryId, ""); var candidate = Eligible(rows.Where(b => !removed.Contains(b.Id)), bound, categoryId, slug);
            if (candidate != null) { replacement += restored.GetValueOrDefault(candidate.Id, Reserve(candidate)); continue; }
            var seedName = PlannerBudgetSeeds.All.FirstOrDefault(s => s.CategorySlug == slug)?.Name;
            if (rows.Any(b => b.ContractId == null && !bound.Contains(b.Id) && b.PlanTemplateKey == null &&
                (b.CategoryId == categoryId || (seedName != null && b.ItemName == seedName))))
                warnings.Add($"Khoản “{seedName ?? "dịch vụ đã chọn"}” hiện có chưa có dấu vết dự toán tự động hoặc đã được chỉnh sửa. Hệ thống giữ nguyên và thêm giá combo như khoản mới; có thể trùng dự toán. Hãy kiểm tra Planner trước khi gửi.");
        }
        var members = scope.ProfileId.HasValue ? await db.WeddingMembers.Where(m => m.WeddingProfileId == scope.ProfileId).Select(m => m.UserId).ToListAsync(ct) : new List<Guid>();
        members.Add(scope.OwnerId);
        var contracts = await db.BookingContracts.AsNoTracking().Where(c => members.Contains(c.CustomerId) &&
            (c.Status == ContractStatus.Confirmed || c.Status == ContractStatus.Completed || c.Status == ContractStatus.Disputed)).ToListAsync(ct);
        var contractLeadIds = contracts.Select(c => c.LeadId).ToList();
        var contractComboRows = await db.ComboOrderItems.AsNoTracking().Where(i => i.LeadId.HasValue && contractLeadIds.Contains(i.LeadId.Value) && i.BudgetItemId.HasValue)
            .Select(i => new { LeadId = i.LeadId!.Value, BudgetId = i.BudgetItemId!.Value }).ToListAsync(ct);
        decimal missingContractReserve = 0;
        foreach (var contract in contracts)
        {
            var associated = contractComboRows.Where(i => i.LeadId == contract.LeadId).Select(i => i.BudgetId).ToHashSet();
            var represented = rows.Where(b => b.ContractId == contract.Id || (b.ContractId == null && associated.Contains(b.Id))).Sum(Reserve);
            missingContractReserve += Math.Max(0, Math.Max(contract.ContractValue, contract.DepositAmount) - represented);
        }
        return new(scope, profile, rows.Sum(Reserve) + releaseDelta + missingContractReserve - replacement, Math.Max(0, replacement - releaseDelta), warnings);
    }

    public static async Task ReserveLineAsync(IApplicationDbContext db, WeddingScope scope, ComboOrder order, ComboOrderItem item,
        ComboPriceLineDto line, ComboSelection selection, CancellationToken ct)
    {
        var (rows, bound, slugs) = await InputsAsync(db, scope, ct);
        var budget = Eligible(rows, bound, line.CategoryId, slugs.GetValueOrDefault(line.CategoryId, ""));
        var created = budget == null;
        var originalCost = budget?.PlannedCost ?? 0; var originalNotes = budget?.Notes;
        var originalKey = budget?.PlanTemplateKey; var originalCategory = budget?.CategoryId;
        var notes = $"Dự kiến từ combo {order.Code}; chờ báo giá NCC xác nhận.";
        if (budget == null)
        {
            budget = BudgetItem.Create(scope.OwnerId, line.CategoryId, line.Title, line.Total, notes: notes); db.BudgetItems.Add(budget);
        }
        else
        {
            budget.AssignCategory(line.CategoryId); budget.UpdateCosts(line.Total, 0, notes, false, preservePlanTemplate: true);
        }
        if (scope.ProfileId.HasValue) budget.AttachToWedding(scope.ProfileId.Value, budget.Owner);
        item.AttachBudget(budget.Id);
        item.SetSelection(line.ServiceItemId, line.Quantity, line.Unit, Serialize(selection,
            new(created, originalCost, originalNotes, originalKey, originalCategory, budget.UpdatedAt)), line.CancellationPolicy);
    }

    public static async Task ReleaseAsync(IApplicationDbContext db, ComboOrderItem item, CancellationToken ct)
    {
        var origin = Origin(item); if (origin == null || !item.BudgetItemId.HasValue) return;
        var row = await db.BudgetItems.FirstOrDefaultAsync(b => b.Id == item.BudgetItemId, ct);
        if (row == null || !IsUnmodified(row, origin) || row.PlannedCost != item.Price - item.DiscountShare) return;
        if (origin.CreatedNew) db.BudgetItems.Remove(row);
        else
        {
            row.UpdateCosts(origin.PlannedCost, 0, origin.Notes, false); row.AssignCategory(origin.CategoryId);
            if (origin.PlanTemplateKey != null) row.MarkPlanTemplate(origin.PlanTemplateKey);
        }
    }

    public static async Task<BudgetItem?> FindContractEstimateAsync(IApplicationDbContext db, Guid leadId, WeddingScope scope, CancellationToken ct)
    {
        var item = await db.ComboOrderItems.FirstOrDefaultAsync(i => i.LeadId == leadId && i.BudgetItemId.HasValue, ct);
        var origin = item == null ? null : Origin(item); if (item == null || origin == null) return null;
        var row = await db.BudgetItems.FirstOrDefaultAsync(b => b.Id == item.BudgetItemId && b.CustomerId == scope.OwnerId, ct);
        return row != null && IsUnmodified(row, origin) && row.PlannedCost == item.Price - item.DiscountShare ? row : null;
    }
}
