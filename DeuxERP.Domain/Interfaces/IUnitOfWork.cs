namespace DeuxERP.Domain.Interfaces
{
    public interface IUnitOfWork
    {
        Task<bool> CommitAsync();
    }
}