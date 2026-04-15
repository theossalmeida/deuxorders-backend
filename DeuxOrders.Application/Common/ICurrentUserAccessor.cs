namespace DeuxOrders.Application.Common;

public interface ICurrentUserAccessor
{
    Guid UserId { get; }
    string UserName { get; }
}
