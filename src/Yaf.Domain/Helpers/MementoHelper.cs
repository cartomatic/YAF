using System.Runtime.CompilerServices;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain.Helpers;

/// <summary>
/// Shared memento building blocks used by both <see cref="Entity{TId,TSelf,TMemento}"/>
/// and <see cref="AggregateRoot{TId,TSelf,TMemento}"/> for identity bridging and
/// cross-cutting concern mapping.
/// </summary>
internal static class MementoHelper<TId, TSelf, TMemento>
    where TId : ITypedId
    where TSelf : Entity<TId>
    where TMemento : class
{
    /// <summary>
    /// Writes the entity's identity to the memento if the memento implements <see cref="IHasIdentity"/>.
    /// </summary>
    internal static void WriteIdentity(TMemento memento, TId id)
    {
        if (memento is IHasIdentity hasIdentity)
        {
            hasIdentity.Id = id.Value;
        }
    }

    /// <summary>
    /// Reads the identity from the memento and constructs a <typeparamref name="TId"/>.
    /// </summary>
    internal static (TId? id, bool success) ReadIdentity(TMemento memento)
    {
        if (memento is IHasIdentity hasIdentity)
        {
            if (hasIdentity.Id is null)
            {
                return (default, true);
            }

            return ((TId)Activator.CreateInstance(typeof(TId), hasIdentity.Id.Value)!, true);
        }

        return (default, false);
    }

    /// <summary>
    /// Creates a new <typeparamref name="TSelf"/> instance via <see cref="RuntimeHelpers.GetUninitializedObject"/>.
    /// </summary>
    internal static TSelf CreateUninitializedInstance() =>
        (TSelf)RuntimeHelpers.GetUninitializedObject(typeof(TSelf));

    /// <summary>
    /// Snapshots all cross-cutting concerns (timestamps, accountability, soft-delete)
    /// from the entity to the memento via direct interface reads.
    /// </summary>
    internal static void SnapshotCrossCutting(TSelf entity, TMemento memento)
    {
        if (entity is ITimestamped ts && memento is IHasTimestamps hts)
        {
            hts.CreatedAtUtc = ts.CreatedAtUtc;
            hts.ModifiedAtUtc = ts.ModifiedAtUtc;
        }

        if (entity is IAccountable acc && memento is IHasAccountability ha)
        {
            ha.CreatedBy = acc.CreatedBy?.Value;
            ha.ModifiedBy = acc.ModifiedBy?.Value;
        }

        if (entity is ISoftDeletable sd && memento is IHasSoftDelete hsd)
        {
            hsd.DeletedAtUtc = sd.DeletedAtUtc;
            hsd.DeletedBy = sd.DeletedBy?.Value;
        }
    }

    /// <summary>
    /// Hydrates all cross-cutting concerns (timestamps, accountability, soft-delete)
    /// from the memento to the entity via direct interface writes.
    /// </summary>
    internal static void HydrateCrossCutting(TSelf entity, TMemento memento)
    {
        if (entity is ITimestampedWriter ts && memento is IHasTimestamps hts)
        {
            ts.CreatedAtUtc = hts.CreatedAtUtc;
            ts.ModifiedAtUtc = hts.ModifiedAtUtc;
        }

        if (entity is IAccountableWriter acc && memento is IHasAccountability ha)
        {
            acc.CreatedBy = ha.CreatedBy.HasValue ? new ActorId(ha.CreatedBy.Value) : null;
            acc.ModifiedBy = ha.ModifiedBy.HasValue ? new ActorId(ha.ModifiedBy.Value) : null;
        }

        if (entity is ISoftDeletableWriter sd && memento is IHasSoftDelete hsd)
        {
            sd.DeletedAtUtc = hsd.DeletedAtUtc;
            sd.DeletedBy = hsd.DeletedBy.HasValue ? new ActorId(hsd.DeletedBy.Value) : null;
        }
    }
}
