namespace Yaf.Domain.Interfaces;

/// <summary>
/// Carries a correlation identifier for tracing a logical operation across
/// multiple events, commands, and service boundaries.
/// </summary>
/// <remarks>
/// <para>
/// The correlation ID groups all side effects of a single user action or system operation.
/// Infrastructure populates this from <c>ICorrelationIdProvider</c> at dispatch time.
/// </para>
/// <para>
/// This interface is standalone and reusable — domain events, commands, queries,
/// and integration events can all implement it.
/// </para>
/// </remarks>
public interface ICorrelated
{
    /// <summary>
    /// The correlation identifier linking this object to the originating operation.
    /// </summary>
    Guid CorrelationId { get; }
}
