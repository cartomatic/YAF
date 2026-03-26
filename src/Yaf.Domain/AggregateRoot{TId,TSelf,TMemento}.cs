using System.Runtime.CompilerServices;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain;

/// <summary>
/// Base class for aggregate roots that support memento-based persistence.
/// Concrete types must override <see cref="CreateId"/>, <see cref="SnapshotCore"/>,
/// <see cref="RestoreCore"/>, <see cref="HydrateCore"/>, and <see cref="Validate"/>.
/// </summary>
/// <remarks>
/// <para>
/// The base class handles the <see cref="Entity{TId}.Id"/> property in all memento operations
/// via <see cref="IHasIdentity{T}"/> on the memento. Snapshot writes <c>memento.Id = Id.Value</c>.
/// Restore/Hydrate reads <c>memento.Id</c> and converts back to <typeparamref name="TId"/>
/// via the abstract <see cref="CreateId"/> method.
/// </para>
/// <para>
/// Properties must use <c>{ get; private set; }</c> — positional parameters
/// and <c>init</c> accessors are not compatible with memento restoration via
/// <see cref="RuntimeHelpers.GetUninitializedObject"/>.
/// </para>
/// <para>
/// Both <see cref="IMemento{TSelf,TMemento}.Restore"/> and <see cref="IHydratable{TMemento}.Hydrate"/>
/// call <see cref="Validate"/> and throw <see cref="ValidationException"/> on invalid state.
/// </para>
/// <para>
/// The domain events collection survives memento restoration via lazy initialization (<c>??=</c>).
/// </para>
/// </remarks>
/// <typeparam name="TId">The strongly-typed identifier type.</typeparam>
/// <typeparam name="T">The backing value type of the identifier (e.g., <see cref="Guid"/>).</typeparam>
/// <typeparam name="TSelf">The concrete aggregate root type (CRTP pattern).</typeparam>
/// <typeparam name="TMemento">The memento contract, must implement <see cref="IHasIdentity{T}"/>.</typeparam>
public abstract class AggregateRoot<TId, T, TSelf, TMemento> : AggregateRoot<TId>, IMemento<TSelf, TMemento>, IHydratable<TMemento>
    where TId : ITypedId<T>
    where T : IEquatable<T>
    where TSelf : AggregateRoot<TId, T, TSelf, TMemento>
    where TMemento : class, IHasIdentity<T>
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
        memento.Id = Id.Value;
        SnapshotCore(memento);
    }

    /// <inheritdoc cref="IMemento{TSelf,TMemento}.Restore"/>
    public static TSelf Restore(TMemento memento)
    {
        ArgumentNullException.ThrowIfNull(memento);
        var instance = (TSelf)RuntimeHelpers.GetUninitializedObject(typeof(TSelf));
        instance.Id = instance.CreateId(memento.Id);
        instance.RestoreCore(memento);

        var errors = instance.Validate();
        if (errors.Count > 0)
        {
            throw new ValidationException(typeof(TSelf), errors);
        }

        return instance;
    }

    /// <inheritdoc cref="IHydratable{TMemento}.Hydrate"/>
    public void Hydrate(TMemento memento)
    {
        ArgumentNullException.ThrowIfNull(memento);
        Id = CreateId(memento.Id);
        HydrateCore(memento);

        var errors = Validate();
        if (errors.Count > 0)
        {
            throw new ValidationException(typeof(TSelf), errors);
        }
    }

    /// <summary>
    /// Creates a typed identifier from the raw backing value.
    /// </summary>
    /// <param name="value">The raw identity value from the memento.</param>
    /// <returns>A strongly-typed identifier instance.</returns>
    protected abstract TId CreateId(T value);

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
    protected abstract IReadOnlyCollection<IError> Validate();
}
