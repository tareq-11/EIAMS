namespace SharedKernel;

public interface IDomainEventSource
{
    List<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
