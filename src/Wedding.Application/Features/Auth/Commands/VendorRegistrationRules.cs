using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Wedding.Application.Features.Auth.Commands;

/// <summary>Shared brand slug rules for public signup and administrative provisioning.</summary>
internal static class VendorRegistrationRules
{
    internal static string GenerateSlug(string phrase)
    {
        var normalized = phrase.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var ascii = new string(normalized.Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray());
        var slug = Regex.Replace(ascii, @"[^a-z0-9]+", "-").Trim('-');
        if (slug.Length == 0) slug = "doi-tac";
        return slug[..Math.Min(slug.Length, 180)].TrimEnd('-');
    }
}
