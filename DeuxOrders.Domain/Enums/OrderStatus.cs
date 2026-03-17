namespace DeuxOrders.Domain.Enums
{
    public enum OrderStatus
    {
        Pending              = 1,
        Completed            = 2,
        Canceled             = 3,
        Received             = 4,
        Preparing            = 5,
        WaitingPickupOrDelivery = 6
    }
}
