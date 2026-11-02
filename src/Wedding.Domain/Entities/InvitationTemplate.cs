using Wedding.Domain.Common;

namespace Wedding.Domain.Entities;

public class InvitationTemplate : BaseEntity
{
    public string Key { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Category { get; private set; } = "Classic";
    public string PrimaryColor { get; private set; } = "#25334c";
    public string BackgroundColor { get; private set; } = "#faf8f0";
    public string Motif { get; private set; } = "✦";
    public bool IsPremium { get; private set; }
    private InvitationTemplate() { }
    public static InvitationTemplate Create(int index, string key, string name, string category, string primary, string background, string motif, bool premium = false)
        => new() { Id = Guid.Parse($"80000000-0000-0000-0000-{index:000000000000}"), CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), Key = key, Name = name, Category = category, PrimaryColor = primary, BackgroundColor = background, Motif = motif, IsPremium = premium };
    public static InvitationTemplate[] Catalog => [
        Create(1, "rustic", "Lá xanh ngày vui", "Nature", "#456451", "#f3f5ec", "❧"),
        Create(2, "minimalist", "Lời hẹn tinh khôi", "Minimal", "#25334c", "#faf8f0", "♡"),
        Create(3, "luxury", "Ánh vàng thanh lịch", "Luxury", "#745e27", "#faf6e9", "✦"),
        Create(4, "navy-cream", "Wedding Match", "Classic", "#25334c", "#f2efe4", "∞"),
        Create(5, "rose-letter", "Thư tình hồng", "Romantic", "#955f6b", "#fbf0f1", "❀"),
        Create(6, "sage-garden", "Khu vườn bình yên", "Nature", "#52674f", "#f3f5ed", "❦"),
        Create(7, "terracotta", "Nắng ấm", "Boho", "#945a42", "#faf1e8", "☼"),
        Create(8, "midnight", "Dạ tiệc sao", "Luxury", "#e0c468", "#202d44", "✧"),
        Create(9, "lavender", "Hẹn ước lavender", "Romantic", "#6d5a8d", "#f6f0fa", "❁"),
        Create(10, "ocean", "Lời hẹn bên biển", "Nature", "#376477", "#edf5f4", "≈"),
        Create(11, "tradition", "Hỷ sự", "Traditional", "#9b3b36", "#fff4e8", "囍"),
        Create(12, "black-tie", "Black Tie", "Minimal", "#242424", "#faf9f6", "&"),
        Create(13, "olive", "Cành olive", "Nature", "#646c3f", "#f7f5e9", "❧"),
        Create(14, "blossom", "Mùa hoa nở", "Romantic", "#a85b68", "#fff7f3", "✿"),
        Create(15, "vintage", "Chuyện đôi mình", "Vintage", "#7a6144", "#f4ead7", "❦"),
        Create(16, "modern-arch", "Vòm hạnh phúc", "Modern", "#475471", "#f2f1eb", "⌒")
    ];
}
