using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Infrastructure.Persistence;
using Wedding.Infrastructure.Services;
using Wedding.Application.Features.Planner;

// Deliberately NOT registered in application startup or migrations. Only the explicitly approved QA
// database may be targeted. Nothing here can seed/update the user's production/local database.
const string approvedDatabase = "WeddingPlanUi_0161333";
if (args.Length < 1 || args[0] != approvedDatabase || !args[0].StartsWith("WeddingPlanUi_", StringComparison.Ordinal))
    throw new InvalidOperationException($"QA fixture refuses every database except {approvedDatabase}. Pass that exact name and --apply to append fictional QA records.");
var apply = args.Contains("--apply", StringComparer.Ordinal);
var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer($"Server=localhost;Database={approvedDatabase};Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5").Options;
await using var db = new ApplicationDbContext(options);
if (!await db.Database.CanConnectAsync()) throw new InvalidOperationException("QA database does not exist or is unavailable. This utility will never create/migrate a database.");
var existing = await db.ServiceItems.CountAsync(i => i.Vendor.Slug.StartsWith("qa-ui-"));
Console.WriteLine($"QA target={approvedDatabase}; existing fictional catalog items={existing}; apply={apply}.");
if (args.Contains("--stamp-known-qa-plan", StringComparer.Ordinal))
{
    // This one account's 14 pristine rows were generated during this QA run before the provenance
    // column existed. Human provenance is explicitly known here; no heuristic/backfill is permitted
    // for any other account/database. Costs, actuals, notes and names are never changed.
    const string qaEmail = "journey-cart-qa-20261016@example.invalid";
    var owner = await db.Users.SingleAsync(u => u.Email == qaEmail);
    var profile = await db.WeddingProfiles.SingleAsync(p => p.CustomerId == owner.Id);
    var rows = await db.BudgetItems.Where(b => b.CustomerId == owner.Id).ToListAsync();
    if (rows.Count != 14 || !profile.OnboardingCompletedAt.HasValue || rows.Any(b => b.ActualCost != 0 || b.IsPaid || b.ContractId.HasValue || b.Notes != null)
        || await db.ComboOrderItems.AnyAsync(i => i.BudgetItemId.HasValue && rows.Select(b => b.Id).Contains(i.BudgetItemId.Value)))
        throw new InvalidOperationException("Known QA wizard cohort is no longer pristine; refusing to infer or stamp provenance.");
    foreach (var seed in PlannerBudgetSeeds.All)
    {
        var row = rows.SingleOrDefault(b => b.ItemName == seed.Name);
        if (row == null || row.PlannedCost != Math.Round(profile.TotalBudget * seed.Ratio) ||
            (row.PlanTemplateKey != null && row.PlanTemplateKey != seed.Key) ||
            Math.Abs((row.CreatedAt - profile.OnboardingCompletedAt.Value).TotalMinutes) > 5)
            throw new InvalidOperationException("Known QA wizard values/origin do not match; no changes were saved.");
    }
    if (!apply) { Console.WriteLine("Dry run: 14 explicitly known QA wizard rows eligible for provenance stamping."); return; }
    await db.ExecuteSerializableAsync(async ct =>
    {
        foreach (var seed in PlannerBudgetSeeds.All)
        {
            var row = rows.Single(b => b.ItemName == seed.Name); if (row.PlanTemplateKey == seed.Key) continue;
            row.MarkPlanTemplate(seed.Key);
            if (seed.CategorySlug != null)
            {
                var category = await db.Categories.SingleOrDefaultAsync(c => c.Slug == seed.CategorySlug, ct);
                if (category != null) row.AssignCategory(category.Id);
            }
        }
        await db.SaveChangesAsync(ct); return true;
    });
    Console.WriteLine("Stamped provenance only for 14 explicitly known pristine QA wizard rows. No amounts/names/notes were changed."); return;
}
if (!apply || existing > 0) { Console.WriteLine(existing > 0 ? "QA fixture already present; left every existing record untouched (legacy mixed presets remain historical and are filtered by the API)." : "Dry run only. Expected append: 3 fictional vendors, 27 service items, 9 single-vendor presets."); return; }
var categories = await db.Categories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).ToListAsync();
if (categories.Count != 7) throw new InvalidOperationException("QA fixture expects the existing seven service categories; no category will be created/updated.");
var seeded = await db.ExecuteSerializableAsync(async ct =>
{
    // Recheck after taking the database lock to make repeated/concurrent invocations idempotent.
    if (await db.ServiceItems.AnyAsync(i => i.Vendor.Slug.StartsWith("qa-ui-"), ct)) return 0;
    var hasher = new PasswordHasher();
    async Task<Vendor> CreateVendor(string slug, string brand, string email, string phone)
    {
        if (await db.Vendors.AnyAsync(v => v.Slug == slug, ct) || await db.Users.AnyAsync(u => u.Email == email, ct))
            throw new InvalidOperationException("A QA fixture identity already exists without the catalog; refusing to overwrite it.");
        var user = User.Create($"[DEMO] {brand}", email, phone, hasher.Hash("DemoWeddingOnly!2026"), UserRole.VendorOwner); db.Users.Add(user);
        var vendor = Vendor.Create(user.Id, $"[DEMO] {brand}", slug, "Nhân vật QA hư cấu", phone, "TP. Hồ Chí Minh", .08m);
        vendor.UpdateProfile(vendor.BrandName, vendor.ContactPerson, phone, "Địa chỉ hư cấu — dữ liệu thử nghiệm, không phải NCC thật", "TP. Hồ Chí Minh", "NCC HƯ CẤU, chỉ dùng kiểm thử giao diện/giỏ combo trên database QA riêng. Không đặt dịch vụ hoặc chuyển tiền.", null, null);
        vendor.SetVerified(true); vendor.ConfigureComboParticipation(true, .08m); db.Vendors.Add(vendor); return vendor;
    }
    var full = await CreateVendor("qa-ui-fullservice", "Mây Wedding — Trọn gói", "qa-full@weddingmatch.example", "0900000901");
    var photo = await CreateVendor("qa-ui-photo", "Mây Photo Lab", "qa-photo@weddingmatch.example", "0900000902");
    var decor = await CreateVendor("qa-ui-decor", "Mây Decor House", "qa-decor@weddingmatch.example", "0900000903");
    var amounts = new decimal[][] { new[] { 2500000m, 3500000m, 5000000m }, new[] { 15000000m, 25000000m, 45000000m }, new[] { 12000000m, 20000000m, 35000000m }, new[] { 7000000m, 15000000m, 30000000m }, new[] { 3000000m, 5000000m, 9000000m }, new[] { 20000m, 35000m, 65000m }, new[] { 15000000m, 25000000m, 40000000m } };
    var tierNames = new[] { "Tiết kiệm", "Phổ biến", "Sang trọng" }; var tierCodes = new[] { "Saving", "Popular", "Luxury" };
    const string policy = "[DEMO] Trước 90 ngày hoàn 100% cọc; 30–89 ngày hoàn 50%; dưới 30 ngày không hoàn; NCC hủy hoàn 100%. Không chuyển tiền vào dữ liệu QA.";
    var byVendorCategoryTier = new Dictionary<(Guid Vendor, Guid Category, int Tier), ServiceItem>();
    for (var categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++)
    {
        var category = categories[categoryIndex]; var vendors = new List<Vendor> { full };
        if (categoryIndex == 1) vendors.Add(decor); if (categoryIndex == 2) vendors.Add(photo);
        foreach (var vendor in vendors)
        {
            var title = $"[DEMO] {category.Name.Split('(')[0].Trim()} – {vendor.BrandName.Replace("[DEMO] ", "")}";
            var listing = Listing.Create(vendor.Id, category.Id, title, $"{vendor.Slug}-{category.Slug}", amounts[categoryIndex][0], amounts[categoryIndex][2], "Dữ liệu QA hư cấu, không phải dịch vụ/NCC thật. Dùng kiểm thử chọn món, đổi NCC, biến thể và giỏ hàng.", "TP. Hồ Chí Minh");
            listing.Approve(); db.Listings.Add(listing);
            var image = await db.ListingMedias.AsNoTracking().Where(m => m.Listing.CategoryId == category.Id).Select(m => m.ThumbnailUrl ?? m.MediaUrl).FirstOrDefaultAsync(ct)
                ?? "https://images.unsplash.com/photo-1519741497674-611481863552?auto=format&fit=crop&w=700&q=75";
            db.ListingMedias.Add(ListingMedia.Create(listing.Id, image, image, isFeatured: true));
            for (var tier = 0; tier < 3; tier++)
            {
                var unit = categoryIndex == 0 ? "Table" : categoryIndex == 5 ? "Guest" : "Package";
                var name = $"[DEMO] {category.Name.Split('(')[0].Trim()} · {tierNames[tier]}";
                var item = ServiceItem.Create(vendor.Id, listing.Id, category.Id, name, amounts[categoryIndex][tier], unit, 10, 1000, "TP. Hồ Chí Minh,HCM,Hồ Chí Minh", policy);
                item.SetDailyCapacity(10);
                var delta = categoryIndex == 0 ? 200000m : categoryIndex == 5 ? 10000m : 1500000m;
                item.Variants.Add(ItemVariant.Create(item.Id, categoryIndex == 0 ? "Nâng thực đơn / bàn" : "Nâng cấp gói", delta));
                if (categoryIndex == 2) item.Variants.Add(ItemVariant.Create(item.Id, "Thêm máy quay + flycam", 5000000));
                item.AddOns.Add(ItemAddOn.Create(item.Id, categoryIndex == 5 ? "Quà cảm ơn / khách" : "Photobooth / gói", categoryIndex == 5 ? 20000 : 3700000, categoryIndex == 5));
                item.Submit(); item.Review(true, "QA fixture hư cấu đã đánh dấu DEMO"); db.ServiceItems.Add(item);
                byVendorCategoryTier[(vendor.Id, category.Id, tier)] = item;
            }
        }
    }
    for (var tier = 0; tier < 3; tier++)
    {
        foreach (var presetOwner in new[] { full, photo, decor })
        {
            var template = ComboTemplate.Create($"[DEMO] {tierNames[tier]} · {presetOwner.BrandName.Replace("[DEMO] ", "")}", tierCodes[tier], "Hiện đại", "Combo QA hư cấu, chỉ thuộc một NCC. Chỉnh thêm/bớt/đổi trong danh mục của NCC này. Giá máy chủ tính theo số khách.", null, tier * 3 + (presetOwner == full ? 0 : presetOwner == photo ? 1 : 2));
            template.SetScope(presetOwner.Id, "TP. Hồ Chí Minh", new[] { .03m, .05m, .08m }[tier], approved: true);
            foreach (var pair in byVendorCategoryTier.Where(p => p.Key.Vendor == presetOwner.Id && p.Key.Tier == tier))
            {
                var item = pair.Value;
                var slot = ComboTemplateItem.Create(template.Id, item.CategoryId, item.ListingId, false);
                var replacements = byVendorCategoryTier.Where(p => p.Key.Category == item.CategoryId && p.Key.Vendor == presetOwner.Id).Select(p => p.Value.Id).ToList();
                slot.ConfigureItems(item.Id, JsonSerializer.Serialize(replacements)); template.Items.Add(slot);
            }
            db.ComboTemplates.Add(template);
        }
    }
    await db.SaveChangesAsync(ct); return byVendorCategoryTier.Count;
});
Console.WriteLine($"Appended {seeded} clearly marked fictional service items and nine single-vendor presets to {approvedDatabase}; no existing record was changed.");
