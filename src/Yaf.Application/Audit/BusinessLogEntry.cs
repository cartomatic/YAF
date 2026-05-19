namespace Yaf.Application.Audit;

/// <summary>
/// Immutable, value-typed record of a single business event written to the
/// application's append-only audit log.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="BusinessLogEntry"/> answers the question "what business-meaningful
/// thing happened, to which aggregate, by whom, in what tenant, and when?" It is the
/// canonical wire/storage shape produced by <see cref="IBusinessEventLog"/> and
/// consumed by audit-log infrastructure (writers, projectors, search indexes).
/// </para>
/// <para>
/// <b>Primitive-type rationale.</b> The entry deliberately uses primitives
/// (<see cref="Guid"/>, <see cref="string"/>, <see cref="DateTimeOffset"/>) rather
/// than domain typed-IDs such as <see cref="Yaf.Domain.ActorId"/> or
/// <see cref="Yaf.Domain.TenantId"/>. This is a DTO that crosses the persistence
/// boundary — keeping primitives avoids forcing readers (log queries, projections,
/// downstream services) to reconstruct domain types just to read a stored value, and
/// keeps the contract stable when domain ID shapes evolve. A typed-ID call site can
/// always project to the underlying <c>Value</c> when constructing the entry.
/// </para>
/// <para>
/// <b>Naming.</b> <see cref="ActorId"/> uses the YAF domain vocabulary
/// (<see cref="Yaf.Domain.ActorId"/>, <see cref="Yaf.Domain.Interfaces.IActorScoped"/>)
/// so callers do not translate names across layers.
/// </para>
/// <para>
/// <b><see cref="ActivityId"/> shape.</b> Stored as <see cref="string"/> rather than
/// <see cref="Guid"/>. .NET's distributed tracing model expresses activity identifiers
/// as W3C trace-context strings (for example
/// <c>"00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"</c>), not GUIDs;
/// storing the formatted string preserves the value end-to-end without lossy
/// conversion.
/// </para>
/// <para>
/// All non-optional properties use the <c>required</c> modifier so the compiler
/// enforces complete construction at the call site. <see cref="ActivityId"/> and
/// <see cref="Metadata"/> are intentionally optional — callers omit them when no
/// activity scope is present and when no extra context applies.
/// </para>
/// </remarks>
public sealed record BusinessLogEntry
{
    /// <summary>
    /// Short, human-readable description of the business event that occurred,
    /// for example <c>"order.placed"</c> or <c>"invoice.cancelled"</c>.
    /// </summary>
    public required string What { get; init; }

    /// <summary>
    /// The type name of the aggregate that the event pertains to, used for
    /// log filtering and routing (for example <c>"Order"</c>, <c>"Invoice"</c>).
    /// </summary>
    public required string AggregateType { get; init; }

    /// <summary>
    /// Identifier of the aggregate instance that produced the event. YAF typed
    /// IDs are <see cref="Guid"/>-backed (see <see cref="Yaf.Domain.TypedId"/>),
    /// so the audit log records the underlying value directly.
    /// </summary>
    public required Guid AggregateId { get; init; }

    /// <summary>
    /// Identifier of the tenant in whose scope the event occurred, or
    /// <see langword="null"/> for cross-tenant administration and platform-level
    /// system operations that have no tenant.
    /// </summary>
    public required Guid? TenantId { get; init; }

    /// <summary>
    /// Identifier of the actor responsible for the event. Always populated —
    /// background jobs and other unattended operations record the well-known
    /// system actor identifier rather than leaving the field unset.
    /// </summary>
    public required Guid ActorId { get; init; }

    /// <summary>
    /// Correlation identifier linking this entry to every other artifact produced
    /// by the same logical operation (commands, notifications, log records) across
    /// process and service boundaries.
    /// </summary>
    public required Guid CorrelationId { get; init; }

    /// <summary>
    /// W3C trace-context activity identifier (for example
    /// <c>"00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"</c>) of the
    /// originating <see cref="System.Diagnostics.Activity"/>, or <see langword="null"/>
    /// when the event was produced outside of an active tracing scope. Optional —
    /// callers do not need to supply this when no activity is active.
    /// </summary>
    public string? ActivityId { get; init; }

    /// <summary>
    /// UTC instant at which the business event occurred. Captured by
    /// <see cref="IBusinessEventLog"/> from a system clock at append time.
    /// </summary>
    public required DateTimeOffset OccurredAtUtc { get; init; }

    /// <summary>
    /// Optional contextual key/value bag carrying event-specific data that does not
    /// fit the fixed columns (for example a target identifier, a reason code, or a
    /// payload diff). Exposed as a read-only dictionary so consumers cannot mutate
    /// the stored entry.
    /// </summary>
    /// <remarks>
    /// Values should be JSON-serializable primitives (<see cref="string"/>, numbers,
    /// <see cref="bool"/>, <see cref="DateTimeOffset"/>) or simple structures
    /// composed of the same. Infrastructure adapters may reject or truncate values
    /// that violate this expectation — delegates, large object graphs, and types
    /// with cycles are not supported.
    /// </remarks>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}
