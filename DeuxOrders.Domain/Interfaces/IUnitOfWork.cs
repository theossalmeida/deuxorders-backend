namespace DeuxOrders.Domain.Interfaces
{
    public interface IUnitOfWork
    {
        Task<bool> CommitAsync();
    }
}