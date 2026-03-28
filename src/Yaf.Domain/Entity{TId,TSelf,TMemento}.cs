using Yaf.Domain.Extensions;
using Yaf.Domain.Helpers;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain;

/// <summary>
/// Base class for entities that support memento-based persistence.
/// Auto-maps identity, timestamps, accountability, and soft-delete when both entity
/// and memento implement matching interfaces. Consumers handle entity-specific properties
/// and tenant context in <see cref="SnapshotCore"/>/<see cref="HydrateCore"/>.
/// </summary>
/// <typeparam name="TId">The strongly-typed identifier type.</typeparam>
/// <typeparam name="TSelf">The concrete entity type (CRTP pattern).</typeparam>
/// <typeparam name="TMemento">The memento contract.</typeparam>
public abstract class Entity<TId, TSelf, TMemento> : Entity<TId>, IMemento<TSelf, TMemento>, IHydratable<TMemento>, IValidatable
    where TId : ITypedId
    where TSelf : Entity<TId, TSelf, TMemento>
    where TMemento : class
{
    private static readonly Action<TSelf, object?>? _createdAtWriter = MementoHelper<TId, TSelf, TMemento>.BuildWriter<ITimestamped, IHasTimestamps>(nameof(ITimestamped.CreatedAtUtc));
    private static readonly Action<TSelf, object?>? _modifiedAtWriter = MementoHelper<TId, TSelf, TMemento>.BuildWriter<ITimestamped, IHasTimestamps>(nameof(ITimestamped.ModifiedAtUtc));
    private static readonly Action<TSelf, object?>? _createdByWriter = MementoHelper<TId, TSelf, TMemento>.BuildWriter<IAccountable, IHasAccountability>(nameof(IAccountable.CreatedBy));
    private static readonly Action<TSelf, object?>? _modifiedByWriter = MementoHelper<TId, TSelf, TMemento>.BuildWriter<IAccountable, IHasAccountability>(nameof(IAccountable.ModifiedBy));
    private static readonly Action<TSelf, object?>? _deletedAtWriter = MementoHelper<TId, TSelf, TMemento>.BuildWriter<ISoftDeletable, IHasSoftDelete>(nameof(ISoftDeletable.DeletedAtUtc));
    private static readonly Action<TSelf, object?>? _deletedByWriter = MementoHelper<TId, TSelf, TMemento>.BuildWriter<ISoftDeletable, IHasSoftDelete>(nameof(ISoftDeletable.DeletedBy));

    /// <inheritdoc />
    protected Entity(TId id) : base(id) { }

    /// <summary>Parameterless constructor for memento restoration.</summary>
    protected Entity() { }

    /// <inheritdoc cref="IMemento{TSelf,TMemento}.Snapshot"/>
    public void Snapshot(TMemento memento)
    {
        ArgumentNullException.ThrowIfNull(memento);
        MementoHelper<TId, TSelf, TMemento>.WriteIdentity(memento, Id);
        SnapshotTimestamps(memento);
        SnapshotAccountability(memento);
        SnapshotSoftDelete(memento);
        SnapshotCore(memento);
    }

    /// <inheritdoc cref="IMemento{TSelf,TMemento}.Restore"/>
    public static TSelf Restore(TMemento memento)
    {
        ArgumentNullException.ThrowIfNull(memento);
        var instance = MementoHelper<TId, TSelf, TMemento>.CreateUninitializedInstance();
        instance.Hydrate(memento);
        return instance;
    }

    /// <inheritdoc cref="IHydratable{TMemento}.Hydrate"/>
    public void Hydrate(TMemento memento)
    {
        ArgumentNullException.ThrowIfNull(memento);

        var (id, success) = MementoHelper<TId, TSelf, TMemento>.ReadIdentity(memento);
        if (success)
        {
            Id = id!;
        }

        HydrateTimestamps(memento);
        HydrateAccountability(memento);
        HydrateSoftDelete(memento);
        HydrateCore(memento);
        this.ThrowIfInvalid();
    }

    /// <summary>
    /// Populates the provided memento with subclass-specific state.
    /// Identity, timestamps, accountability, and soft-delete are handled automatically.
    /// </summary>
    protected abstract void SnapshotCore(TMemento memento);

    /// <summary>
    /// Hydrates subclass-specific state from the provided memento.
    /// Called during both <see cref="Restore"/> and <see cref="Hydrate"/>.
    /// Identity, timestamps, accountability, and soft-delete are handled automatically.
    /// </summary>
    protected abstract void HydrateCore(TMemento memento);

    /// <inheritdoc />
    public abstract IReadOnlyCollection<IError> GetValidationErrors();

    // --- Auto-mapped concerns ---

    private void SnapshotTimestamps(TMemento memento)
    {
        if (this is ITimestamped ts && memento is IHasTimestamps hts)
        {
            hts.CreatedAtUtc = ts.CreatedAtUtc;
            hts.ModifiedAtUtc = ts.ModifiedAtUtc;
        }
    }

    private void HydrateTimestamps(TMemento memento)
    {
        if (this is ITimestamped && memento is IHasTimestamps hts)
        {
            _createdAtWriter?.Invoke((TSelf)this, hts.CreatedAtUtc);
            _modifiedAtWriter?.Invoke((TSelf)this, hts.ModifiedAtUtc);
        }
    }

    private void SnapshotAccountability(TMemento memento)
    {
        if (this is IAccountable acc && memento is IHasAccountability ha)
        {
            ha.CreatedBy = acc.CreatedBy?.Value;
            ha.ModifiedBy = acc.ModifiedBy?.Value;
        }
    }

    private void HydrateAccountability(TMemento memento)
    {
        if (_createdByWriter is not null && memento is IHasAccountability ha)
        {
            _createdByWriter.Invoke((TSelf)this, ha.CreatedBy.HasValue ? new ActorId(ha.CreatedBy.Value) : null);
            _modifiedByWriter!.Invoke((TSelf)this, ha.ModifiedBy.HasValue ? new ActorId(ha.ModifiedBy.Value) : null);
        }
    }

    private void SnapshotSoftDelete(TMemento memento)
    {
        if (this is ISoftDeletable sd && memento is IHasSoftDelete hsd)
        {
            hsd.DeletedAtUtc = sd.DeletedAtUtc;
            hsd.DeletedBy = sd.DeletedBy?.Value;
        }
    }

    private void HydrateSoftDelete(TMemento memento)
    {
        if (_deletedByWriter is not null && memento is IHasSoftDelete hsd)
        {
            _deletedAtWriter?.Invoke((TSelf)this, hsd.DeletedAtUtc);
            _deletedByWriter.Invoke((TSelf)this, hsd.DeletedBy.HasValue ? new ActorId(hsd.DeletedBy.Value) : null);
        }
    }

}
