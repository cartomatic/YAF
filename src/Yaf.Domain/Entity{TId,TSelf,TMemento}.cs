using Yaf.Domain.Extensions;
using Yaf.Domain.Helpers;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain;

/// <summary>
/// Base class for entities that support memento-based persistence.
/// Concrete types must override <see cref="SnapshotCore"/> and <see cref="HydrateCore"/>
/// for their own state, and <see cref="GetValidationErrors"/> for invariant enforcement.
/// </summary>
/// <remarks>
/// <para>
/// When <typeparamref name="TMemento"/> implements <see cref="IHasIdentity{T}"/> with the same
/// <c>T</c> as <typeparamref name="TId"/>'s <see cref="ITypedId{T}"/>, the base class handles
/// identity snapshot, restore, and hydrate automatically.
/// </para>
/// <para>
/// When <typeparamref name="TMemento"/> implements <see cref="IHasTimestamps"/> and the entity
/// implements <see cref="ITimestamped"/>, timestamps are mapped automatically in both directions.
/// </para>
/// <para>
/// For other cross-cutting concerns (accountability, soft-delete, tenant), consumers map
/// properties in <see cref="SnapshotCore"/> and <see cref="HydrateCore"/>.
/// </para>
/// <para>
/// <see cref="Restore"/> creates a new instance via
/// <see cref="System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject"/>
/// and delegates to <see cref="Hydrate"/>. Consumers only need to implement
/// <see cref="HydrateCore"/> — there is no separate RestoreCore.
/// </para>
/// <para>
/// Properties must use <c>{ get; private set; }</c> — positional parameters
/// and <c>init</c> accessors are not compatible with memento restoration via
/// <see cref="System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject"/>.
/// </para>
/// </remarks>
/// <typeparam name="TId">The strongly-typed identifier type.</typeparam>
/// <typeparam name="TSelf">The concrete entity type (CRTP pattern).</typeparam>
/// <typeparam name="TMemento">The memento contract. Should implement <see cref="IHasIdentity{T}"/> for automatic identity handling.</typeparam>
public abstract class Entity<TId, TSelf, TMemento> : Entity<TId>, IMemento<TSelf, TMemento>, IHydratable<TMemento>, IValidatable
    where TId : ITypedId
    where TSelf : Entity<TId, TSelf, TMemento>
    where TMemento : class
{
    private static readonly Action<TSelf, object?>? _createdAtWriter = BuildTimestampWriter(nameof(ITimestamped.CreatedAtUtc));
    private static readonly Action<TSelf, object?>? _modifiedAtWriter = BuildTimestampWriter(nameof(ITimestamped.ModifiedAtUtc));

    /// <summary>
    /// Initializes a new instance of the entity with the specified identifier.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    protected Entity(TId id) : base(id)
    {
    }

    /// <summary>
    /// Parameterless constructor for memento restoration.
    /// </summary>
    protected Entity()
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
    /// <remarks>
    /// Creates a new instance via <see cref="System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject"/>
    /// and delegates to <see cref="Hydrate"/>. There is no separate RestoreCore — implement
    /// <see cref="HydrateCore"/> for both initial materialization and in-place updates.
    /// </remarks>
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
    /// Identity and timestamps are handled automatically — this method is for entity-specific properties
    /// and other cross-cutting concerns (accountability, soft-delete, tenant).
    /// </summary>
    /// <param name="memento">The memento instance to populate.</param>
    protected abstract void SnapshotCore(TMemento memento);

    /// <summary>
    /// Hydrates subclass-specific state from the provided memento.
    /// Called during both <see cref="Restore"/> (initial materialization) and <see cref="Hydrate"/>
    /// (in-place update). Identity and timestamps are handled automatically — this method is for
    /// entity-specific properties and other cross-cutting concerns.
    /// </summary>
    /// <param name="memento">The memento instance to hydrate from.</param>
    protected abstract void HydrateCore(TMemento memento);

    /// <summary>
    /// Validates the state of this entity after restoration or hydration from a memento.
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
