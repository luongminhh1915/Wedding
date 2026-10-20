using Wedding.Domain.Common;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

/// <summary>Business scope selected by a shop, distinct from the category of each sellable service.</summary>
public class BusinessGroup : BaseEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal DefaultCommissionRate { get; private set; }
    public bool RequiresCommissionBasisApproval { get; private set; }
    public string PolicyVersion { get; private set; } = string.Empty;
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; } = true;
    public ICollection<Category> Categories { get; private set; } = new List<Category>();
    public ICollection<VendorBusinessGroup> Vendors { get; private set; } = new List<VendorBusinessGroup>();

    private BusinessGroup() { }
}

public static class BusinessGroupPolicy
{
    public const string InitialVersion = "2026-10-19-v1";
    public static readonly Guid VenueBanquetId = Guid.Parse("22222222-2222-2222-2222-222222222201");
    public static readonly Guid DecorInvitationsId = Guid.Parse("22222222-2222-2222-2222-222222222202");
    public static readonly Guid PhotoBridalMakeupId = Guid.Parse("22222222-2222-2222-2222-222222222203");
    public static readonly Guid FullServicePlanningId = Guid.Parse("22222222-2222-2222-2222-222222222204");
    public static readonly Guid OtherEventsId = Guid.Parse("22222222-2222-2222-2222-222222222205");
}

/// <summary>Membership only: default commission rules are not copied into each shop.</summary>
public class VendorBusinessGroup
{
    public Guid VendorId { get; private set; }
    public Guid BusinessGroupId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Vendor Vendor { get; private set; } = null!;
    public BusinessGroup BusinessGroup { get; private set; } = null!;

    private VendorBusinessGroup() { }

    public static VendorBusinessGroup Create(Guid vendorId, Guid businessGroupId)
    {
        if (vendorId == Guid.Empty || businessGroupId == Guid.Empty)
            throw new DomainException("Nhà cung cấp và nhóm kinh doanh là bắt buộc.");
        return new() { VendorId = vendorId, BusinessGroupId = businessGroupId, CreatedAt = DateTime.UtcNow };
    }
}
