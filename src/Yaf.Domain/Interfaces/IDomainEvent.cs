namespace Yaf.Domain.Interfaces;

/// <summary>
/// Contract for domain events — in-process signals within a bounded context that carry
/// their own identity, timestamp, and cross-cutting context.
/// </summary>
/// <remarks>
/// <para>
/// Domain events are raised by aggregate roots via <c>AddDomainEvent</c> and dispatched
/// by infrastructure after persistence. Each event is self-describing: it carries
/// correlation, tenant, user, and activity context so that consumers never need to
/// look up context separately.
/// </para>
/// <para>
/// Infrastructure populates context fields (from context providers) when the event is
/// created or dispatched. The event <em>carries</em> the context; infrastructure
/// <em>populates</em> it.
/// </para>
/// <para>
/// For events that carry a typed data payload, use <see cref="IDomainEvent{T}"/>.
/// </para>
/// </remarks>
public interface IDomainEvent : ICorrelated, ITenantScoped<TenantId>, IUserScoped, IActivityScoped
{
    /// <summary>
    /// The unique identifier of this event instance.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// When the event occurred (UTC).
    /// </summary>
    DateTimeOffset OccurredAtUtc { get; }
}

/// <summary>
/// A domain event that carries a typed data payload.
/// </summary>
/// <remarks>
/// <para>
/// The covariant <c>out T</c> enables polymorphic event handling — for example,
/// an <c>IDomainEvent&lt;object&gt;</c> reference can hold any <c>IDomainEvent&lt;OrderData&gt;</c>.
/// </para>
/// <para>
/// <typeparamref name="T"/> is restricted to output (covariant) positions only.
/// This is natural for events, which are read-only by design.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of the data payload carried by this event.</typeparam>
public interface IDomainEvent<out T> : IDomainEvent
{
    /// <summary>
    /// The data payload carried by this event.
    /// </summary>
    T Data { get; }
}
