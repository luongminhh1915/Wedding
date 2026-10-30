using Wedding.Domain.Common;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class Category : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Icon { get; private set; }
    public string? Description { get; private set; }
    public decimal DefaultCommissionRate { get; private set; } // FM-001 (0.03 - 0.12)
    public Guid? BusinessGroupId { get; private set; }
    public bool RequiresCommissionBasisApproval { get; private set; }
    public int DisplayOrder { get; private set; } = 0;
    public bool IsActive { get; private set; } = true;

    // Navigation Properties
    public ICollection<Listing> Listings { get; private set; } = new List<Listing>();
    public BusinessGroup? BusinessGroup { get; private set; }

    private Category() { } // Dành cho EF Core

    public static Category Create(string name, string slug, string? icon, string? description, 
        decimal defaultCommissionRate, int displayOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Tên ngành hàng không được để trống.");
        if (defaultCommissionRate < 0.03m || defaultCommissionRate > 0.12m)
            throw new DomainException("Tỷ lệ hoa hồng mặc định không hợp lệ.");

        return new Category
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            Icon = icon,
            Description = description,
            DefaultCommissionRate = defaultCommissionRate,
            DisplayOrder = displayOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string? icon, string? description, decimal defaultCommissionRate, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name) || defaultCommissionRate < .03m || defaultCommissionRate > .12m) throw new DomainException("Ngành cần tên và tỷ lệ hoa hồng trong khung 3–12%.");
        Name = name.Trim();
        Icon = icon;
        Description = description;
        DefaultCommissionRate = defaultCommissionRate;
        DisplayOrder = displayOrder;
        SetUpdated();
    }
}
