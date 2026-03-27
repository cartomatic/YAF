using Yaf.Domain.Extensions;
using Yaf.Domain.Helpers;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain;

/// <summary>
/// Base class for aggregate roots that support memento-based persistence.
/// Concrete types must override <see cref="SnapshotCore"/> and <see cref="HydrateCore"/>
/// for their own state, and <see cref="GetValidationErrors"/> for invariant enforcement.
/// </summary>
/// <remarks>
/// <para>
/// Identity and timestamps are handled automatically when the memento implements
/// the corresponding interfaces. Other cross-cutting concerns (accountability,
/// soft-delete, tenant) are handled by consumers in <see cref="SnapshotCore"/>
/// and <see cref="HydrateCore"/>.
/// </para>
/// <para>
/// <see cref="Restore"/> creates a new instance and delegates to <see cref="Hydrate"/>.
/// Consumers only need to implement <see cref="HydrateCore"/> — there is no separate RestoreCore.
/// </para>
/// <para>
/// The domain events collection survives memento restoration via lazy initialization (<c>??=</c>).
/// </para>
/// </remarks>
/// <typeparam name="TId">The strongly-typed identifier type.</typeparam>
/// <typeparam name="TSelf">The concrete aggregate root type (CRTP pattern).</typeparam>
/// <typeparam name="TMemento">The memento contract.</typeparam>
public abstract class AggregateRoot<TId, TSelf, TMemento> : AggregateRoot<TId>, IMemento<TSelf, TMemento>, IHydratable<TMemento>, IValidatable
    where TId : ITypedId
    where TSelf : AggregateRoot<TId, TSelf, TMemento>
    where TMemento : class
{
    private static readonly Action<TSelf, object?>? _createdAtWriter = BuildTimestampWriter(nameof(ITimestamped.CreatedAtUtc));
    private static readonly Action<TSelf, object?>? _modifiedAtWriter = BuildTimestampWriter(nameof(ITimestamped.ModifiedAtUtc));

    /// <summary>
    /// Initializes a new instance of the aggregate root with the specified identifier.
    /// </summary>
    /// <param name="id">The aggregate root identifier.</param>
    protected AggregateRoot(TId id) : base(id)
    {
    }

    /// <summary>
    /// Parameterless constructor for memento restoration.
    /// </summary>
    protected AggregateRoot()
    {
    }

    /// <inheritdoc cref="IMemento{TSelf,TMemento}.Snapshot"/>
    public void Snapshot(TMemento memento)
    {
        ArgumentNullException.ThrowIfNull(memento);
        MementoHelper<TId, TSelf, TMemento>.WriteIdentity(memento, Id);
        SnapshotTimestamps(memento);
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
    /// <remarks>
    /// State is mutated before validation: Id, timestamps, then <see cref="HydrateCore"/>, then
    /// <see cref="IValidatable.GetValidationErrors"/>. If validation fails, the entity
    /// is left in a partially-mutated state. Callers should discard the entity instance
    /// on <see cref="ValidationException"/> rather than continuing to use it.
    /// </remarks>
    public void Hydrate(TMemento memento)
    {
        ArgumentNullException.ThrowIfNull(memento);

        var (id, success) = MementoHelper<TId, TSelf, TMemento>.ReadIdentity(memento);
        if (success)
        {
            Id = id!;
        }

        HydrateTimestamps(memento);
        HydrateCore(memento);
        this.ThrowIfInvalid();
    }

    /// <summary>
    /// Populates the provided memento with subclass-specific state.
    /// Identity and timestamps are handled automatically.
    /// </summary>
    /// <param name="memento">The memento instance to populate.</param>
    protected abstract void SnapshotCore(TMemento memento);

    /// <summary>
    /// Hydrates subclass-specific state from the provided memento.
    /// Called during both <see cref="Restore"/> and <see cref="Hydrate"/>.
    /// Identity and timestamps are handled automatically.
    /// </summary>
    /// <param name="memento">The memento instance to hydrate from.</param>
    protected abstract void HydrateCore(TMemento memento);

    /// <summary>
    /// Validates the state of this aggregate root after restoration or hydration from a memento.
    /// Return an empty collection if the state is valid.
    /// </summary>
    /// <returns>A collection of validation errors, empty if valid.</returns>
    public abstract IReadOnlyCollection<IError> GetValidationErrors();

    private void SnapshotTimestamps(TMemento memento)
    {
        if (this is ITimestamped timestamped && memento is IHasTimestamps hasTimestamps)
        {
            hasTimestamps.CreatedAtUtc = timestamped.CreatedAtUtc;
            hasTimestamps.ModifiedAtUtc = timestamped.ModifiedAtUtc;
        }
    }

    private void HydrateTimestamps(TMemento memento)
    {
        if (this is ITimestamped && memento is IHasTimestamps hasTimestamps)
        {
            _createdAtWriter?.Invoke((TSelf)this, hasTimestamps.CreatedAtUtc);
            _modifiedAtWriter?.Invoke((TSelf)this, hasTimestamps.ModifiedAtUtc);
        }
    }

    private static Action<TSelf, object?>? BuildTimestampWriter(string propertyName) =>
        typeof(ITimestamped).IsAssignableFrom(typeof(TSelf)) && typeof(IHasTimestamps).IsAssignableFrom(typeof(TMemento))
            ? ReflectionHelper.BuildPropertyWriter<TSelf>(propertyName)
            : null;
}
