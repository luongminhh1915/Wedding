namespace Wedding.Domain.Enums;

public enum AppointmentStatus { AwaitingDeposit = 1, Pending = 2, Confirmed = 3, Rejected = 4, Cancelled = 5, Completed = 6, NoShow = 7, Expired = 8, RescheduleProposed = 9, CheckedIn = 10 }
public enum ComboOrderStatus { Submitted = 1, InProgress = 2, Completed = 3, Cancelled = 4, Draft = 5, Expired = 6, PartiallyQuoted = 7, Quoted = 8, PartiallyContracted = 9 }
public enum ComboItemStatus { Sent = 1, Accepted = 2, Quoted = 3, Contracted = 4, Cancelled = 5 }
public enum QuoteStatus { Sent = 1, Accepted = 2, Rejected = 3, Expired = 4, Superseded = 5 }
public enum SettlementStatus { Open = 1, Closed = 2, Paid = 3, Overdue = 4, InDispute = 5 }
public enum VerificationStatus { Draft = 0, Submitted = 1, Approved = 2, Rejected = 3, Suspended = 4 }
public enum DisputeStatus { Open = 1, InReview = 2, ResolvedCustomer = 3, ResolvedVendor = 4 }
public enum ReviewTargetType { Service = 1, Shop = 2, Combo = 3 }
public enum ReviewEditStatus { Pending = 1, Approved = 2, Rejected = 3 }
