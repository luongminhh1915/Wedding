using Wedding.Domain.Common;

namespace Wedding.Domain.Entities;

/// <summary>P1 – Hồ sơ đám cưới của cặp đôi (wizard lập kế hoạch).</summary>
public class WeddingProfile : BaseEntity
{
    public Guid CustomerId { get; private set; }
    public string GroomName { get; private set; } = string.Empty;
    public string BrideName { get; private set; } = string.Empty;
    public DateTime? WeddingDate { get; private set; }
    public string City { get; private set; } = string.Empty;
    public decimal TotalBudget { get; private set; }
    public int GuestCount { get; private set; }
    public string Style { get; private set; } = "Classic";
    public string? PartnerEmail { get; private set; }

    public User Customer { get; private set; } = null!;

    private WeddingProfile() { }

    public static WeddingProfile Create(Guid customerId) => new() { CustomerId = customerId };

    public void Update(string groomName, string brideName, DateTime? weddingDate, string city, decimal totalBudget,
        int guestCount, string style, string? partnerEmail)
    {
        GroomName = groomName.Trim(); BrideName = brideName.Trim(); WeddingDate = weddingDate; City = city.Trim();
        TotalBudget = Math.Max(0, totalBudget); GuestCount = Math.Max(0, guestCount); Style = style;
        PartnerEmail = partnerEmail?.Trim().ToLowerInvariant();
        SetUpdated();
    }
}
