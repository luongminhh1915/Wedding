using Wedding.Domain.Entities;

namespace Wedding.Application.Features.Planner;

public record PlannerBudgetSeed(string Key, string Name, decimal Ratio, string? CategorySlug = null);

/// <summary>Stable provenance for wizard suggestions. A matching name/amount alone never grants overwrite authority.</summary>
public static class PlannerBudgetSeeds
{
    public static IReadOnlyList<PlannerBudgetSeed> All { get; } = new PlannerBudgetSeed[]
    {
        new("venue", "Nhà hàng tiệc cưới", .40m, "tiem-cuoi-venues"),
        new("photo", "Chụp ảnh cưới", .08m, "quay-chup-photo-video"),
        new("attire", "Váy cưới & vest", .07m, "vay-cuoi-vest"),
        new("makeup", "Trang điểm cô dâu", .04m, "trang-diem-makeup"),
        new("decor", "Trang trí & hoa cưới", .08m, "trang-tri-decor"),
        new("rings", "Nhẫn cưới", .08m), new("invites", "Thiệp cưới", .02m, "thiep-cuoi-qua-tang"),
        new("car", "Xe hoa", .025m), new("ceremony", "Lễ ăn hỏi & gia tiên", .05m),
        new("music", "MC & âm nhạc", .035m), new("gifts", "Quà cảm ơn", .02m),
        new("travel", "Lưu trú & di chuyển", .02m), new("legal", "Phí pháp lý & đăng ký", .005m), new("reserve", "Dự phòng", .075m)
    };
    public static PlannerBudgetSeed? Find(string? key) => All.FirstOrDefault(seed => seed.Key == key);
    public static bool MatchesCategory(BudgetItem row, Guid categoryId, string categorySlug)
        => Find(row.PlanTemplateKey) is { } seed && (row.CategoryId == categoryId || seed.CategorySlug == categorySlug);
}
