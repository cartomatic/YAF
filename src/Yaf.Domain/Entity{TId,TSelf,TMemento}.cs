using System.Runtime.CompilerServices;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain;

/// <summary>
/// Base class for entities that support memento-based persistence.
/// Concrete types must override <see cref="SnapshotCore"/>, <see cref="RestoreCore"/>,
/// <see cref="HydrateCore"/>, and <see cref="Validate"/>.
/// </summary>
/// <remarks>
/// <para>
/// The base class handles the <see cref="Entity{TId}.Id"/> property in all memento operations.
/// The <c>*Core</c> template methods are for subclass-specific state only.
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
/// </remarks>
/// <typeparam name="TId">The strongly-typed identifier type.</typeparam>
/// <typeparam name="TSelf">The concrete entity type (CRTP pattern).</typeparam>
/// <typeparam name="TMemento">The memento contract — typically an interface at the domain level.</typeparam>
public abstract class Entity<TId, TSelf, TMemento> : Entity<TId>, IMemento<TSelf, TMemento>, IHydratable<TMemento>
    where TId : ITypedId
    where TSelf : Entity<TId, TSelf, TMemento>
    where TMemento : class
{
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
        SetIdOnMemento(memento, Id);
        SnapshotCore(memento);
    }

    /// <inheritdoc cref="IMemento{TSelf,TMemento}.Restore"/>
    public static TSelf Restore(TMemento memento)
    {
        ArgumentNullException.ThrowIfNull(memento);
        var instance = (TSelf)RuntimeHelpers.GetUninitializedObject(typeof(TSelf));
        instance.Id = instance.GetIdFromMemento(memento);
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
        Id = GetIdFromMemento(memento);
        HydrateCore(memento);

        var errors = Validate();
        if (errors.Count > 0)
        {
            throw new ValidationException(typeof(TSelf), errors);
        }
    }

    /// <summary>
    /// Extracts the entity identifier from the provided memento.
    /// </summary>
    /// <param name="memento">The memento to read the identifier from.</param>
    /// <returns>The entity identifier.</returns>
    protected abstract TId GetIdFromMemento(TMemento memento);

    /// <summary>
    /// Writes the entity identifier to the provided memento.
    /// </summary>
    /// <param name="memento">The memento to write the identifier to.</param>
    /// <param name="id">The entity identifier to write.</param>
    protected abstract void SetIdOnMemento(TMemento memento, TId id);

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
    /// Validates the state of this entity after restoration or hydration from a memento.
    /// Return an empty collection if the state is valid.
    /// </summary>
    /// <returns>A collection of validation errors, empty if valid.</returns>
    protected abstract IReadOnlyCollection<IError> Validate();
}
