namespace Wedding.Domain.Entities;

/// <summary>Current 34 provincial units, official directory https://chinhphu.vn/p checked 18 October 2026.
/// Plain names avoid baking changing province/municipality prefixes into stored values.</summary>
public static class VietnamProvinces
{
    public static IReadOnlyList<string> Names { get; } = Array.AsReadOnly(new[]
    {
        "An Giang", "Bắc Ninh", "Cà Mau", "Cao Bằng", "Cần Thơ", "Đà Nẵng",
        "Đắk Lắk", "Điện Biên", "Đồng Nai", "Đồng Tháp", "Gia Lai", "Hà Nội",
        "Hà Tĩnh", "Hải Phòng", "Hồ Chí Minh", "Huế", "Hưng Yên", "Khánh Hòa",
        "Lai Châu", "Lạng Sơn", "Lào Cai", "Lâm Đồng", "Nghệ An", "Ninh Bình",
        "Phú Thọ", "Quảng Ngãi", "Quảng Ninh", "Quảng Trị", "Sơn La", "Tây Ninh",
        "Thái Nguyên", "Thanh Hóa", "Tuyên Quang", "Vĩnh Long"
    });

    /// <summary>Only normalizes new input; no existing account or service-region record is rewritten.</summary>
    public static bool TryNormalize(string? value, out string province)
    {
        var key = RegionRules.Canonical(value ?? "");
        foreach (var prefix in new[] { "thanhpho", "tinh", "tp" })
            if (key.StartsWith(prefix, StringComparison.Ordinal)) { key = key[prefix.Length..]; break; }
        key = key switch { "hcm" or "saigon" => "hochiminh", "hn" => "hanoi", _ => key };
        province = Names.FirstOrDefault(name => RegionRules.Canonical(name) == key) ?? "";
        return province.Length != 0;
    }
}
