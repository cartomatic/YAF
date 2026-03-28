using Yaf.Domain.Extensions;
using Yaf.Domain.Helpers;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain;

/// <summary>
/// Base class for aggregate roots that support memento-based persistence.
/// Auto-maps identity, timestamps, accountability, and soft-delete when both entity
/// and memento implement matching interfaces. Consumers handle entity-specific properties
/// and tenant context in <see cref="SnapshotCore"/>/<see cref="HydrateCore"/>.
/// </summary>
/// <typeparam name="TId">The strongly-typed identifier type.</typeparam>
/// <typeparam name="TSelf">The concrete aggregate root type (CRTP pattern).</typeparam>
/// <typeparam name="TMemento">The memento contract.</typeparam>
public abstract class AggregateRoot<TId, TSelf, TMemento> : AggregateRoot<TId>, IMemento<TSelf, TMemento>, IHydratable<TMemento>, IValidatable
    where TId : ITypedId
    where TSelf : AggregateRoot<TId, TSelf, TMemento>
    where TMemento : class
{
    private static readonly Action<TSelf, object?>? _createdAtWriter = MementoHelper<TId, TSelf, TMemento>.BuildWriter<ITimestamped, IHasTimestamps>(nameof(ITimestamped.CreatedAtUtc));
    private static readonly Action<TSelf, object?>? _modifiedAtWriter = MementoHelper<TId, TSelf, TMemento>.BuildWriter<ITimestamped, IHasTimestamps>(nameof(ITimestamped.ModifiedAtUtc));
    private static readonly Action<TSelf, object?>? _deletedAtWriter = MementoHelper<TId, TSelf, TMemento>.BuildWriter(typeof(ISoftDeletable<>), typeof(IHasSoftDelete), nameof(IHasSoftDelete.DeletedAtUtc));
    private static readonly Func<TSelf, object?>? _deletedAtReader = MementoHelper<TId, TSelf, TMemento>.BuildReader(typeof(ISoftDeletable<>), typeof(IHasSoftDelete), nameof(IHasSoftDelete.DeletedAtUtc));
    private static readonly TypedIdBridge<TSelf>? _accountabilityBridge = MementoHelper<TId, TSelf, TMemento>.BuildBridge(typeof(IAccountable<>), typeof(IHasAccountability), nameof(IHasAccountability<Guid>.CreatedBy), nameof(IHasAccountability<Guid>.ModifiedBy));
    private static readonly TypedIdBridge<TSelf>? _softDeleteBridge = MementoHelper<TId, TSelf, TMemento>.BuildBridge(typeof(ISoftDeletable<>), typeof(IHasSoftDelete), nameof(IHasSoftDelete<Guid>.DeletedBy));

    /// <inheritdoc />
    protected AggregateRoot(TId id) : base(id) { }

    /// <summary>Parameterless constructor for memento restoration.</summary>
    protected AggregateRoot() { }

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

    // --- Auto-mapped concerns (identical to Entity — shared via TypedIdBridge and ReflectionHelper) ---

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
        if (_accountabilityBridge is not null && memento is IHasAccountability ha)
        {
            var (createdBy, modifiedBy) = _accountabilityBridge.ReadPair((TSelf)this);
            ha.BoxedCreatedBy = createdBy;
            ha.BoxedModifiedBy = modifiedBy;
        }
    }

    private void HydrateAccountability(TMemento memento)
    {
        if (_accountabilityBridge is not null && memento is IHasAccountability ha)
        {
            _accountabilityBridge.WritePair((TSelf)this, ha.BoxedCreatedBy, ha.BoxedModifiedBy);
        }
    }

    private void SnapshotSoftDelete(TMemento memento)
    {
        if (_softDeleteBridge is not null && memento is IHasSoftDelete hsd)
        {
            var (deletedBy, _) = _softDeleteBridge.ReadPair((TSelf)this);
            hsd.BoxedDeletedBy = deletedBy;
            hsd.DeletedAtUtc = (DateTimeOffset?)_deletedAtReader?.Invoke((TSelf)this);
        }
    }

    private void HydrateSoftDelete(TMemento memento)
    {
        if (_softDeleteBridge is not null && memento is IHasSoftDelete hsd)
        {
            _deletedAtWriter?.Invoke((TSelf)this, hsd.DeletedAtUtc);
            _softDeleteBridge.WritePair((TSelf)this, hsd.BoxedDeletedBy, null);
        }
    }

}
