using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public static class CommissionRules
{
    public static decimal ResolveRate(decimal categoryRate, decimal vendorRate)
    {
        if (categoryRate is < 0.03m or > 0.12m) throw new DomainException("Tỷ lệ ngành phải trong khung 3–12%.");
        if (vendorRate == 0) return categoryRate;
        if (vendorRate is < 0.03m or > 0.12m) throw new DomainException("Tỷ lệ riêng NCC nằm ngoài khung 3–12%; cần Finance điều chỉnh trước khi xác nhận.");
        return vendorRate;
    }
    public static (decimal First, decimal Second) Split(decimal value, decimal rate)
    {
        if (value <= 0) throw new DomainException("Giá trị hợp đồng phải dương.");
        var total = Math.Round(value * rate, 0, MidpointRounding.AwayFromZero);
        var first = Math.Round(total / 2m, 0, MidpointRounding.AwayFromZero);
        return (first, total - first);
    }
}
