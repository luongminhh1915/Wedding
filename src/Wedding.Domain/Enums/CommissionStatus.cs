namespace Wedding.Domain.Enums;

public enum CommissionStatus
{
    Pending = 1,
    Paid = 2,
    Overdue = 3,
    Cancelled = 4,
    Unsettled = 5,
    EscrowLocked = 6,
    InSettlement = 7,
    Settled = 8,
    Frozen = 9
}
