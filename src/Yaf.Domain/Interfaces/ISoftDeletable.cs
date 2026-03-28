namespace Yaf.Domain.Interfaces;

/// <summary>
/// Enforces soft-deletion tracking on entities — when deleted and by whom.
/// When present, infrastructure uses soft-delete semantics instead of hard delete:
/// the row stays in the main table with <c>DeletedAtUtc</c> set, and an EF Core
/// global query filter (<c>WHERE DeletedAtUtc IS NULL</c>) excludes it from normal queries.
/// </summary>
/// <remarks>
/// <para>
/// <c>DeletedAtUtc != null</c> means the entity is soft-deleted. Infrastructure sets
/// both properties during the soft-delete operation. Soft-deleted entities can be
/// un-deleted by clearing both properties.
/// </para>
/// <para>
/// This interface is standalone — it does not inherit from <see cref="ITimestamped"/>
/// or <see cref="IAccountable"/>. Consumers compose interfaces as needed.
/// </para>
/// <para>
/// Soft-deletion coexists with the graveyard (<see cref="IHasVersionHistory"/>):
/// <list type="bullet">
///   <item><description><see cref="ISoftDeletable"/> alone: soft delete (row stays, timestamp set)</description></item>
///   <item><description><see cref="IHasVersionHistory"/> alone: graveyard (row archived)</description></item>
///   <item><description>Both: soft delete first, then optional permanent delete to graveyard</description></item>
///   <item><description>Neither: hard delete (row gone)</description></item>
/// </list>
/// </para>
/// <para>
/// Uses the framework-provided <see cref="Yaf.Domain.ActorId"/> type.
/// </para>
/// </remarks>
public interface ISoftDeletable
{
    /// <summary>
    /// When this entity was soft-deleted (UTC). <see langword="null"/> if not deleted.
    /// </summary>
    DateTimeOffset? DeletedAtUtc { get; }

    /// <summary>
    /// The actor who soft-deleted this entity. <see langword="null"/> if not deleted.
    /// </summary>
    ActorId? DeletedBy { get; }
}
