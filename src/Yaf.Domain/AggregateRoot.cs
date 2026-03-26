using Yaf.Domain.Interfaces;

namespace Yaf.Domain;

/// <summary>
/// Base class for aggregate roots — entities that serve as consistency boundaries.
/// Owns a domain event collection for in-process event accumulation.
/// </summary>
/// <remarks>
/// <para>
/// Domain events are raised by the aggregate via <see cref="AddDomainEvent"/> (protected)
/// and dispatched by infrastructure after persistence. Infrastructure reads
/// <see cref="DomainEvents"/> and then calls <see cref="ClearDomainEvents"/> after dispatch.
/// </para>
/// <para>
/// Does not carry a concurrency token — optimistic concurrency is opt-in via
/// <c>IHasVersionInfo</c> on the memento.
/// </para>
/// </remarks>
/// <typeparam name="TId">The strongly-typed identifier type, constrained to <see cref="ITypedId"/>.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : ITypedId
{
    private List<IDomainEvent>? _domainEvents;

    /// <summary>
    /// The domain events raised by this aggregate, in insertion order.
    /// Returns an empty collection when no events have been raised.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents =>
        _domainEvents is not null ? _domainEvents.AsReadOnly() : Array.Empty<IDomainEvent>();

    /// <summary>
    /// Initializes a new instance of the aggregate root with the specified identifier.
    /// </summary>
    /// <param name="id">The aggregate root identifier.</param>
    protected AggregateRoot(TId id) : base(id)
    {
    }

    /// <summary>
    /// Parameterless constructor for memento restoration.
    /// </summary>
    protected AggregateRoot()
    {
    }

    /// <summary>
    /// Raises a domain event on this aggregate. Events are accumulated until
    /// infrastructure dispatches them after persistence.
    /// </summary>
    /// <param name="domainEvent">The domain event to raise.</param>
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents ??= [];
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Clears all accumulated domain events. Called by infrastructure after dispatch.
    /// </summary>
    public void ClearDomainEvents() => _domainEvents?.Clear();
}
