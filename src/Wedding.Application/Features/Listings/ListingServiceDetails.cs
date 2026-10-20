using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Listings;

/// <summary>Descriptive proposals only. These fields never set commission, ledger amounts or cancellation rules.</summary>
public record FullServiceDetailsDto(string Scope = "", string Capacity = "", string Stages = "",
    string Outsourcing = "", string ManagementFeeDescription = "", string PackagePriceDescription = "",
    string ExtraCostTerms = "", string PaymentTerms = "");

public record ServiceDetailsDto(string ShortDescription = "", string PriceUnit = "", string IncludedItems = "",
    string ExcludedItems = "", string Duration = "", string Process = "", string ServiceOptions = "",
    string AdditionalFees = "", string Terms = "", string ReschedulePolicy = "", List<string>? PortfolioUrls = null,
    string? VideoUrl = null, string Highlights = "", string CustomerRequirements = "", string Notes = "",
    FullServiceDetailsDto? FullService = null);

public static class ListingServiceDetails
{
    public const int MaximumJsonLength = 60000;
    // Used only for persisted Unicode JSON, never injected into HTML or an inline script.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true
    };

    public static string? NormalizeAndSerialize(ServiceDetailsDto? details)
    {
        if (details == null) return null;
        var full = details.FullService;
        var normalized = details with
        {
            ShortDescription = Text(details.ShortDescription, "Mô tả ngắn", 500),
            PriceUnit = Text(details.PriceUnit, "Đơn vị tính giá", 80),
            IncludedItems = Text(details.IncludedItems, "Hạng mục bao gồm"),
            ExcludedItems = Text(details.ExcludedItems, "Hạng mục không bao gồm"),
            Duration = Text(details.Duration, "Thời gian thực hiện"),
            Process = Text(details.Process, "Quy trình cung cấp"),
            ServiceOptions = Text(details.ServiceOptions, "Tùy chọn dịch vụ"),
            AdditionalFees = Text(details.AdditionalFees, "Mô tả phụ phí"),
            Terms = Text(details.Terms, "Điều kiện sử dụng"),
            ReschedulePolicy = Text(details.ReschedulePolicy, "Điều kiện đổi lịch"),
            PortfolioUrls = Links(details.PortfolioUrls),
            VideoUrl = string.IsNullOrWhiteSpace(details.VideoUrl) ? null : Link(details.VideoUrl),
            Highlights = Text(details.Highlights, "Điểm nổi bật"),
            CustomerRequirements = Text(details.CustomerRequirements, "Yêu cầu dành cho khách hàng"),
            Notes = Text(details.Notes, "Ghi chú"),
            FullService = full == null ? null : full with
            {
                Scope = Text(full.Scope, "Phạm vi tổ chức"),
                Capacity = Text(full.Capacity, "Quy mô phục vụ"),
                Stages = Text(full.Stages, "Các giai đoạn thực hiện"),
                Outsourcing = Text(full.Outsourcing, "Dịch vụ thuê ngoài"),
                ManagementFeeDescription = Text(full.ManagementFeeDescription, "Mô tả phí quản lý/tổ chức"),
                PackagePriceDescription = Text(full.PackagePriceDescription, "Mô tả giá gói"),
                ExtraCostTerms = Text(full.ExtraCostTerms, "Điều kiện phát sinh chi phí"),
                PaymentTerms = Text(full.PaymentTerms, "Tiến độ và điều kiện thanh toán")
            }
        };
        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        if (json.Length > MaximumJsonLength)
            throw new DomainException("Tổng nội dung dịch vụ vượt quá 60.000 ký tự lưu trữ. Vui lòng rút gọn các mục.");
        return json;
    }

    public static ServiceDetailsDto? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumJsonLength) return null;
        try
        {
            var details = JsonSerializer.Deserialize<ServiceDetailsDto>(json, JsonOptions);
            var normalized = NormalizeAndSerialize(details);
            return normalized == null ? null : JsonSerializer.Deserialize<ServiceDetailsDto>(normalized, JsonOptions);
        }
        catch (JsonException) { return null; }
        catch (DomainException) { return null; }
    }

    private static string Text(string? text, string label, int maximum = 4000)
    {
        var value = (text ?? string.Empty).Trim();
        if (value.Length > maximum) throw new DomainException($"{label} tối đa {maximum} ký tự.");
        return value;
    }

    private static List<string> Links(List<string>? links)
    {
        if (links == null) return new();
        if (links.Count > 10) throw new DomainException("Portfolio tối đa 10 đường dẫn.");
        return links.Select(Link).ToList();
    }

    private static string Link(string? link)
    {
        var value = (link ?? string.Empty).Trim();
        if (value.Length is < 1 or > 1000 || (link?.Any(char.IsControl) ?? false) || value.Any(char.IsWhiteSpace) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") ||
            string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo))
            throw new DomainException("Portfolio/video cần đường dẫn HTTP(S) tuyệt đối hợp lệ, không chứa thông tin đăng nhập, tối đa 1.000 ký tự.");
        var decoded = value;
        for (var pass = 0; pass < 5; pass++)
        {
            if (decoded.Any(char.IsControl) || decoded.Contains('\\') ||
                Regex.IsMatch(decoded, @"(?:^|/)\.{1,2}(?:/|$|[?#])"))
                throw new DomainException("Đường dẫn portfolio/video không được chứa ký tự điều khiển, dấu gạch chéo ngược hoặc đoạn đường dẫn tương đối.");
            var next = Uri.UnescapeDataString(decoded);
            if (next == decoded) return value;
            decoded = next;
        }
        throw new DomainException("Đường dẫn portfolio/video được mã hóa quá nhiều lớp.");
    }
}
