namespace Wedding.Domain.Enums;

public enum CommissionRuleScope
{
    System = 1,
    Category = 2,
    Vendor = 3,
    VendorCategory = 4,
    Service = 5,
    Combo = 6
}

public enum CommissionBasisKind
{
    StandardService = 1,
    AzManagementFee = 2,
    ComboComponents = 3
}

public enum CommissionPolicyStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4
}

public enum CommissionAdjustmentStatus
{
    Submitted = 1,
    Approved = 2,
    Rejected = 3
}
