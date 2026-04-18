namespace DeuxERP.Domain.Common;

public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent ev) => _domainEvents.Add(ev);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
