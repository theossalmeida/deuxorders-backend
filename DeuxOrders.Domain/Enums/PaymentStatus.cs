namespace DeuxOrders.Domain.Enums
{
    public enum PaymentStatus
    {
        Pending   = 1,
        Paid      = 2,
        Failed    = 3,
        Refunded  = 4,
        Disputed  = 5,
        Expired   = 6,
        Cancelled = 7
    }
}
