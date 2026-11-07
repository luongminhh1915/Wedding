namespace Wedding.Domain.Enums;

public enum NegotiationStatus
{
    Submitted = 1,
    InDiscussion = 2,
    CounterOffered = 3,
    Approved = 4,
    Rejected = 5,
    Cancelled = 6
}

public enum NegotiationReasonCategory
{
    NewPartner = 1,
    HighValueOrder = 2,
    VolumeGrowth = 3,
    MarginCost = 4,
    StrategicLongTerm = 5,
    Other = 6
}

