using System.Text.RegularExpressions;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Governance;

internal static class ArticleCmsInput
{
    public static string Slug(string? title, string? slug, string? excerpt, string? content, string? category)
    {
        var normalizedSlug = slug?.Trim().ToLowerInvariant() ?? "";
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 300 || normalizedSlug.Length > 300 ||
            !Regex.IsMatch(normalizedSlug, "^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant) ||
            (excerpt?.Length ?? 0) > 1000 || string.IsNullOrWhiteSpace(content) ||
            string.IsNullOrWhiteSpace(category) || category.Trim().Length > 100)
            throw new DomainException("Kiểm tra tiêu đề (300 ký tự), slug không dấu (300 ký tự), tóm tắt (1.000 ký tự), nội dung và danh mục (100 ký tự).");
        return normalizedSlug;
    }
}
