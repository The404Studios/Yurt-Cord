namespace VeaMarketplace.Shared.Enums;

public enum OrderStatus
{
    Pending = 0,
    PaymentProcessing = 1,
    Paid = 2,
    Processing = 3,
    Completed = 4,
    Cancelled = 5,
    Refunded = 6,
    Disputed = 7,
    DisputeResolved = 8
}
