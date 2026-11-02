using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public sealed record OperatingAreaSelection(IReadOnlyList<string> Regions, bool Nationwide, string StorageValue);

/// <summary>Shop operating coverage is separate from its address and each service's own availability.</summary>
public static class OperatingAreaRules
{
    public const int MaximumStorageLength = 1000;

    public static OperatingAreaSelection Normalize(string? city, IReadOnlyList<string>? operatingRegions, bool nationwide)
    {
        if (!string.IsNullOrWhiteSpace(city) && !VietnamProvinces.TryNormalize(city, out _))
            throw new DomainException("Chọn tỉnh / thành phố thuộc danh sách 34 địa phương hiện hành.");
        if (operatingRegions is { Count: > 34 })
            throw new DomainException("Khu vực hoạt động tối đa 34 tỉnh / thành phố.");
        if (nationwide)
        {
            if (operatingRegions is { Count: > 0 })
                throw new DomainException("Chọn Toàn quốc hoặc các tỉnh / thành phố cụ thể, không chọn đồng thời.");
            return new([], true, "*");
        }

        var input = operatingRegions ?? (string.IsNullOrWhiteSpace(city) ? [] : [city]);
        var normalized = new List<string>();
        foreach (var value in input)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 100 || !VietnamProvinces.TryNormalize(value, out var province))
                throw new DomainException("Khu vực hoạt động phải thuộc danh sách 34 tỉnh / thành phố hiện hành.");
            if (!normalized.Contains(province, StringComparer.Ordinal)) normalized.Add(province);
        }
        var storage = string.Join(',', normalized);
        if (storage.Length > MaximumStorageLength)
            throw new DomainException("Danh sách khu vực hoạt động vượt quá giới hạn lưu trữ.");
        return new(normalized, false, storage);
    }

    public static bool IsValid(string? city, IReadOnlyList<string>? regions, bool nationwide, bool requireArea = false)
    {
        try
        {
            var result = Normalize(city, regions, nationwide);
            return !requireArea || result.Nationwide || result.Regions.Count > 0;
        }
        catch (DomainException) { return false; }
    }

    /// <summary>Old rows keep their City fallback; reading never rewrites retired/unknown address values.</summary>
    public static OperatingAreaSelection Read(string? stored, string? legacyCity)
    {
        var value = stored ?? legacyCity ?? "";
        if (value.Trim() == "*") return new([], true, "*");
        var regions = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => VietnamProvinces.TryNormalize(item, out var province) ? province : null)
            .Where(item => item != null).Cast<string>().Distinct(StringComparer.Ordinal).ToArray();
        return new(regions, false, string.Join(',', regions));
    }

    public static bool Serves(string? stored, string? legacyCity, string requested)
    {
        if (!VietnamProvinces.TryNormalize(requested, out var province)) return false;
        var coverage = Read(stored, legacyCity);
        return coverage.Nationwide || coverage.Regions.Contains(province, StringComparer.Ordinal);
    }
}
