namespace Yaf.Application.Context;

/// <summary>
/// Exposes the ambient correlation identifier that links every artifact produced
/// by a single logical operation across process and service boundaries.
/// </summary>
/// <remarks>
/// <para>
/// Infrastructure derives the correlation ID from request headers (for example,
/// inbound HTTP propagation) or from message metadata. When no upstream value is
/// supplied, infrastructure generates a fresh <see cref="Guid"/> at the start of
/// the operation so that tracing and log aggregation always have a value to group
/// by. Consumers stamp the correlation ID onto commands, notifications, and
/// persisted artifacts that implement <see cref="Yaf.Domain.Interfaces.ICorrelated"/>.
/// </para>
/// <para>
/// <see cref="CorrelationId"/> is non-nullable because the provider guarantees a
/// value: either propagated or freshly generated. Pipeline behaviors and downstream
/// services therefore never need to defend against a missing correlation ID.
/// </para>
/// </remarks>
public interface ICorrelationIdProvider
{
    /// <summary>
    /// The correlation identifier for the current ambient operation. Infrastructure
    /// generates a new <see cref="Guid"/> when no upstream identifier is supplied.
    /// </summary>
    Guid CorrelationId { get; }
}
