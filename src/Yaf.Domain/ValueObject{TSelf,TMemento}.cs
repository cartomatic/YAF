using System.Runtime.CompilerServices;
using Yaf.Domain.Extensions;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain;

/// <summary>
/// Base record for value objects that support memento-based persistence.
/// Concrete types must override <see cref="SnapshotCore"/>, <see cref="RestoreCore"/>,
/// and <see cref="GetValidationErrors"/>.
/// </summary>
/// <remarks>
/// Properties must use <c>{ get; private set; }</c> — positional record parameters
/// and <c>init</c> accessors are not compatible with memento restoration.
/// </remarks>
/// <typeparam name="TSelf">The concrete value object type (CRTP pattern).</typeparam>
/// <typeparam name="TMemento">The memento contract — typically an interface at the domain level.</typeparam>
public abstract record ValueObject<TSelf, TMemento> : ValueObject, IMemento<TSelf, TMemento>, IValidatable
    where TSelf : ValueObject<TSelf, TMemento>
    where TMemento : class
{
    /// <inheritdoc cref="IMemento{TSelf,TMemento}.Snapshot"/>
    public void Snapshot(TMemento memento)
    {
        ArgumentNullException.ThrowIfNull(memento);
        SnapshotCore(memento);
    }

    /// <inheritdoc cref="IMemento{TSelf,TMemento}.Restore"/>
    public static TSelf Restore(TMemento memento)
    {
        ArgumentNullException.ThrowIfNull(memento);
        var instance = (TSelf)RuntimeHelpers.GetUninitializedObject(typeof(TSelf));
        instance.RestoreCore(memento);
        ((IValidatable)instance).ThrowIfInvalid();
        return instance;
    }

    /// <summary>
    /// Populates the provided memento with the current state of this value object.
    /// </summary>
    /// <param name="memento">The memento instance to populate.</param>
    protected abstract void SnapshotCore(TMemento memento);

    /// <summary>
    /// Restores the state of this value object from the provided memento.
    /// </summary>
    /// <param name="memento">The memento instance to restore from.</param>
    protected abstract void RestoreCore(TMemento memento);

    /// <summary>
    /// Validates the state of this value object after restoration from a memento.
    /// Return an empty collection if the state is valid.
    /// </summary>
    /// <returns>A collection of validation errors, empty if valid.</returns>
    public abstract IReadOnlyCollection<IError> GetValidationErrors();
}
