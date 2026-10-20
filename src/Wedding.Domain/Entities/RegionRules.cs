using System.Globalization;
using System.Text;

namespace Wedding.Domain.Entities;
public static class RegionRules
{
    public static bool Serves(string allowed, string requested) => !string.IsNullOrWhiteSpace(requested) && allowed
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Any(value => value == "*" || Canonical(value) == Canonical(requested));
    public static string Canonical(string value)
    {
        var builder = new StringBuilder();
        foreach (var letter in value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(letter) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(letter)) builder.Append(letter == 'đ' ? 'd' : letter);
        return builder.ToString() switch
        {
            "hcm" or "tphcm" or "tphochiminh" or "hochiminh" or "thanhphohochiminh" or "saigon" => "hochiminh",
            "hn" or "hanoi" or "tphanoi" or "thanhphohanoi" => "hanoi",
            "danang" or "tpdanang" or "thanhphodanang" => "danang",
            "cantho" or "tpcantho" or "thanhphocantho" => "cantho",
            "haiphong" or "tphaiphong" or "thanhphohaiphong" => "haiphong",
            var normalized => normalized
        };
    }
}
