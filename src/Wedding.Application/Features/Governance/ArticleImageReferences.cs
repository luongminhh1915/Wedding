using System.Text.RegularExpressions;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Governance;

public static class ArticleImageReferences
{
    public static string? Normalize(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return null;
        var value = reference.Trim();
        if (value.Length > 500 || value.Any(char.IsControl) || value.Contains('\\')) throw Invalid();
        if (Regex.IsMatch(value, @"^/uploads/articles/[0-9a-fA-F]{32}\.(jpg|png|webp)$", RegexOptions.CultureInvariant)) return value;
        if (!value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo)) throw Invalid();
        return value;
    }
    private static DomainException Invalid() => new("Chọn ảnh cẩm nang đã tải lên hoặc dùng đường dẫn ảnh HTTPS hợp lệ (tối đa 500 ký tự).");
}
