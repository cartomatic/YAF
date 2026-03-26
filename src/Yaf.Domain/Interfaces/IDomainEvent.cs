namespace Yaf.Domain.Interfaces;

/// <summary>
/// Marker interface for domain events — in-process signals within a bounded context.
/// </summary>
/// <remarks>
/// Domain events are raised by aggregate roots via <c>AddDomainEvent</c> and dispatched
/// by infrastructure after persistence. The context envelope (TenantId, IdentityId,
/// CorrelationId, ActivityId, OccurredAtUtc) is attached by infrastructure at dispatch time,
/// not by the event itself.
/// </remarks>
public interface IDomainEvent;
