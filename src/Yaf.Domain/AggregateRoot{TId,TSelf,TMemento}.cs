using Yaf.Domain.Extensions;
using Yaf.Domain.Helpers;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain;

/// <summary>
/// Base class for aggregate roots that support memento-based persistence.
/// Concrete types must override <see cref="SnapshotCore"/>, <see cref="RestoreCore"/>,
/// <see cref="HydrateCore"/>, and <see cref="GetValidationErrors"/>.
/// </summary>
/// <remarks>
/// <para>
/// When <typeparamref name="TMemento"/> implements <see cref="IHasIdentity{T}"/> with the same
/// <c>T</c> as <typeparamref name="TId"/>'s <see cref="ITypedId{T}"/>, the base class handles
/// identity snapshot, restore, and hydrate automatically.
/// The <c>*Core</c> template methods are for subclass-specific state only.
/// </para>
/// <para>
/// Properties must use <c>{ get; private set; }</c> — positional parameters
/// and <c>init</c> accessors are not compatible with memento restoration via
/// <see cref="System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject"/>.
/// </para>
/// <para>
/// Both <see cref="IMemento{TSelf,TMemento}.Restore"/> and <see cref="IHydratable{TMemento}.Hydrate"/>
/// validate state via <see cref="IValidatable"/> and throw <see cref="ValidationException"/> on invalid state.
/// </para>
/// <para>
/// The domain events collection survives memento restoration via lazy initialization (<c>??=</c>).
/// </para>
/// </remarks>
/// <typeparam name="TId">The strongly-typed identifier type.</typeparam>
/// <typeparam name="TSelf">The concrete aggregate root type (CRTP pattern).</typeparam>
/// <typeparam name="TMemento">The memento contract. Should implement <see cref="IHasIdentity{T}"/> for automatic identity handling.</typeparam>
public abstract class AggregateRoot<TId, TSelf, TMemento> : AggregateRoot<TId>, IMemento<TSelf, TMemento>, IHydratable<TMemento>, IValidatable
    where TId : ITypedId
    where TSelf : AggregateRoot<TId, TSelf, TMemento>
    where TMemento : class
{
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
        SnapshotCore(memento);
    }

    /// <inheritdoc cref="IMemento{TSelf,TMemento}.Restore"/>
    public static TSelf Restore(TMemento memento)
    {
        ArgumentNullException.ThrowIfNull(memento);
        var instance = MementoHelper<TId, TSelf, TMemento>.CreateUninitializedInstance();

        var (id, success) = MementoHelper<TId, TSelf, TMemento>.ReadIdentity(memento);
        if (success)
        {
            instance.Id = id!;
        }

        instance.RestoreCore(memento);
        instance.ThrowIfInvalid();
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

        HydrateCore(memento);
        this.ThrowIfInvalid();
    }

    /// <summary>
    /// Populates the provided memento with subclass-specific state (not the Id).
    /// </summary>
    /// <param name="memento">The memento instance to populate.</param>
    protected abstract void SnapshotCore(TMemento memento);

    /// <summary>
    /// Restores subclass-specific state from the provided memento (not the Id).
    /// Called during <see cref="Restore"/> for initial materialization.
    /// </summary>
    /// <param name="memento">The memento instance to restore from.</param>
    protected abstract void RestoreCore(TMemento memento);

    /// <summary>
    /// Hydrates subclass-specific state from the provided memento (not the Id).
    /// Called during <see cref="Hydrate"/> for in-place state update of tracked instances.
    /// </summary>
    /// <param name="memento">The memento instance to hydrate from.</param>
    protected abstract void HydrateCore(TMemento memento);

    /// <summary>
    /// Validates the state of this aggregate root after restoration or hydration from a memento.
    /// Return an empty collection if the state is valid.
    /// </summary>
    /// <returns>A collection of validation errors, empty if valid.</returns>
    public abstract IReadOnlyCollection<IError> GetValidationErrors();
}
