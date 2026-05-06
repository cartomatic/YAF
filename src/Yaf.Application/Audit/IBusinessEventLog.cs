namespace Yaf.Application.Audit;

/// <summary>
/// Append-only sink for business-meaningful events. Application code calls
/// <see cref="AppendAsync"/> with a small, hand-curated payload describing what
/// happened; the implementation is responsible for materializing the full
/// <see cref="BusinessLogEntry"/> from ambient context and persisting it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Implementation responsibility.</b> Implementations construct the stored
/// <see cref="BusinessLogEntry"/> by combining the four caller-supplied inputs
/// (<c>what</c>, <c>aggregateType</c>, <c>aggregateId</c>, <c>metadata</c>) with
/// values resolved from infrastructure-provided context:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="Yaf.Application.Context.ITenantContextProvider.TenantId"/> for the tenant scope.</description></item>
///   <item><description><see cref="Yaf.Application.Context.IIdentityContextProvider.ActorId"/> for the responsible actor.</description></item>
///   <item><description><see cref="Yaf.Application.Context.ICorrelationIdProvider.CorrelationId"/> for cross-artifact correlation.</description></item>
///   <item><description><see cref="Yaf.Application.Context.IActivityIdProvider.ActivityId"/> for the W3C trace identifier.</description></item>
///   <item><description>A system clock (for example <see cref="TimeProvider"/>) for <see cref="BusinessLogEntry.OccurredAtUtc"/>.</description></item>
/// </list>
/// <para>
/// Centralizing this assembly inside the implementation means application code
/// never has to gather context manually at every call site — handlers state their
/// intent ("this aggregate did this thing") and the audit pipeline supplies the
/// rest.
/// </para>
/// <para>
/// <b>Why <see cref="Task"/> instead of <see cref="Yaf.Domain.Result"/>.</b> The
/// audit log is infrastructure that must succeed for the operation to be
/// considered durably recorded; a failure to append is a system-level fault, not a
/// recoverable business outcome. Implementations therefore signal failure by
/// throwing — the caller's pipeline handles the exception (typically by aborting
/// the unit of work). Returning <c>Task&lt;Result&gt;</c> would suggest that
/// callers inspect a domain-shaped error, which they do not.
/// </para>
/// <para>
/// <b>ADR-1146 drift — parameter-based signature instead of entry-based.</b>
/// ADR-1146's reference example exposed a method that accepted a fully constructed
/// <c>BusinessLogEntry</c>. YAF deliberately drifts to a parameter-based signature
/// because constructing the entry requires reading every ambient context provider
/// plus a clock — work that the implementation already does. Pushing that
/// construction onto every caller would duplicate the wiring across the codebase
/// and create opportunities for drift (different callers stamping
/// <see cref="BusinessLogEntry.OccurredAtUtc"/> from different clocks, for
/// example). The parameter-based shape keeps the call site honest about the only
/// information it actually owns.
/// </para>
/// </remarks>
public interface IBusinessEventLog
{
    /// <summary>
    /// Appends a business event to the audit log. The implementation enriches the
    /// caller-supplied inputs with ambient context (tenant, actor, correlation,
    /// activity, clock) and persists the resulting <see cref="BusinessLogEntry"/>.
    /// </summary>
    /// <param name="what">
    /// Short, human-readable description of the event (for example
    /// <c>"order.placed"</c>). Stored as <see cref="BusinessLogEntry.What"/>.
    /// Implementations must throw <see cref="ArgumentException"/> when
    /// <see langword="null"/>, empty, or whitespace — audit-log entries with
    /// missing identity information have no forensic value.
    /// </param>
    /// <param name="aggregateType">
    /// Type name of the aggregate the event pertains to (for example
    /// <c>"Order"</c>). Stored as <see cref="BusinessLogEntry.AggregateType"/>.
    /// Implementations must throw <see cref="ArgumentException"/> when
    /// <see langword="null"/>, empty, or whitespace.
    /// </param>
    /// <param name="aggregateId">
    /// Identifier of the aggregate instance. Stored as
    /// <see cref="BusinessLogEntry.AggregateId"/>. YAF typed IDs are
    /// <see cref="Guid"/>-backed, so callers pass the underlying value directly.
    /// Implementations must throw <see cref="ArgumentException"/> when this is
    /// <see cref="Guid.Empty"/>.
    /// </param>
    /// <param name="metadata">
    /// Optional contextual key/value bag attached to the event. May be
    /// <see langword="null"/> when no extra data applies. Stored as
    /// <see cref="BusinessLogEntry.Metadata"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// A token observed for cooperative cancellation while the entry is persisted.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> that completes when the entry has been durably
    /// appended. Failures surface as thrown exceptions — the audit log returns
    /// non-generic <see cref="Task"/> rather than <c>Task&lt;Yaf.Domain.Result&gt;</c>
    /// because an append failure is an infrastructure fault, not a recoverable
    /// business outcome.
    /// </returns>
    Task AppendAsync(
        string what,
        string aggregateType,
        Guid aggregateId,
        IReadOnlyDictionary<string, object>? metadata,
        CancellationToken cancellationToken);
}
