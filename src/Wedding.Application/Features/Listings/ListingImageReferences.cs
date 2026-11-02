using System.Text.RegularExpressions;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Listings;

/// <summary>Validate references only; never fetch an external image while saving a listing.</summary>
public static class ListingImageReferences
{
    public static bool IsValid(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return false;
        var value = reference.Trim();
        if (value.Length > 500) return false;
        var decoded = value;
        for (var depth = 0; depth < 4; depth++)
        {
            if (decoded.Any(char.IsControl) || decoded.Contains('\\') ||
                Regex.IsMatch(decoded, @"(^|/)\.\.(/|[?#]|$)", RegexOptions.CultureInvariant)) return false;
            var next = Uri.UnescapeDataString(decoded);
            if (next == decoded) break;
            if (depth == 3) return false;
            decoded = next;
        }
        if (value.StartsWith("/uploads/listings/", StringComparison.Ordinal))
            return Regex.IsMatch(value, @"^/uploads/listings/[0-9a-fA-F]{32}\.(jpg|jpeg|png|webp)$", RegexOptions.CultureInvariant);
        return (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) &&
            Uri.TryCreate(value, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host) && string.IsNullOrEmpty(uri.UserInfo);
    }

    public static List<string> NormalizeRequired(IReadOnlyCollection<string>? references)
    {
        if (references is not { Count: >= 1 and <= 10 } || references.Any(reference => !IsValid(reference)))
            throw new DomainException("Cần từ 1 đến 10 ảnh HTTP(S) hoặc ảnh dịch vụ đã tải lên hợp lệ; mỗi đường dẫn tối đa 500 ký tự.");
        var result = references.Select(reference => reference.Trim()).ToList();
        if (result.Distinct(StringComparer.Ordinal).Count() != result.Count)
            throw new DomainException("Danh sách ảnh không được trùng nhau.");
        return result;
    }
}
