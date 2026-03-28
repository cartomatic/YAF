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
    private static readonly Action<TSelf, object?>? _deletedAtWriter = MementoHelper<TId, TSelf, TMemento>.BuildWriter(typeof(ISoftDeletable<>), typeof(IHasSoftDelete), nameof(IHasSoftDelete.DeletedAtUtc));
    private static readonly Func<TSelf, object?>? _deletedAtReader = MementoHelper<TId, TSelf, TMemento>.BuildReader(typeof(ISoftDeletable<>), typeof(IHasSoftDelete), nameof(IHasSoftDelete.DeletedAtUtc));
    private static readonly TypedIdBridge<TSelf>? _createdByBridge = MementoHelper<TId, TSelf, TMemento>.BuildBridge(typeof(IAccountable<>), typeof(IHasAccountability), nameof(IHasAccountability<Guid>.CreatedBy));
    private static readonly TypedIdBridge<TSelf>? _modifiedByBridge = MementoHelper<TId, TSelf, TMemento>.BuildBridge(typeof(IAccountable<>), typeof(IHasAccountability), nameof(IHasAccountability<Guid>.ModifiedBy));
    private static readonly TypedIdBridge<TSelf>? _deletedByBridge = MementoHelper<TId, TSelf, TMemento>.BuildBridge(typeof(ISoftDeletable<>), typeof(IHasSoftDelete), nameof(IHasSoftDelete<Guid>.DeletedBy));

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
        if (_createdByBridge is not null && memento is IHasAccountability ha)
        {
            ha.BoxedCreatedBy = _createdByBridge.Read((TSelf)this);
            ha.BoxedModifiedBy = _modifiedByBridge!.Read((TSelf)this);
        }
    }

    private void HydrateAccountability(TMemento memento)
    {
        if (_createdByBridge is not null && memento is IHasAccountability ha)
        {
            _createdByBridge.Write((TSelf)this, ha.BoxedCreatedBy);
            _modifiedByBridge!.Write((TSelf)this, ha.BoxedModifiedBy);
        }
    }

    private void SnapshotSoftDelete(TMemento memento)
    {
        if (_deletedByBridge is not null && memento is IHasSoftDelete hsd)
        {
            hsd.DeletedAtUtc = (DateTimeOffset?)_deletedAtReader?.Invoke((TSelf)this);
            hsd.BoxedDeletedBy = _deletedByBridge.Read((TSelf)this);
        }
    }

    private void HydrateSoftDelete(TMemento memento)
    {
        if (_deletedByBridge is not null && memento is IHasSoftDelete hsd)
        {
            _deletedAtWriter?.Invoke((TSelf)this, hsd.DeletedAtUtc);
            _deletedByBridge.Write((TSelf)this, hsd.BoxedDeletedBy);
        }
    }

}
