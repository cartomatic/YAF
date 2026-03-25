using System.Runtime.CompilerServices;

namespace Yaf.Domain;

/// <summary>
/// Base record for value objects that support memento-based persistence.
/// Concrete types must override <see cref="SnapshotInternal"/> and <see cref="RestoreInternal"/>.
/// </summary>
/// <typeparam name="TSelf">The concrete value object type (CRTP pattern).</typeparam>
/// <typeparam name="TMemento">The memento contract — typically an interface at the domain level.</typeparam>
public abstract record ValueObject<TSelf, TMemento> : ValueObject
    where TSelf : ValueObject<TSelf, TMemento>
    where TMemento : class
{
    /// <inheritdoc cref="Interfaces.IMemento{TSelf,TMemento}.Snapshot"/>
    public void Snapshot(TMemento memento) => SnapshotInternal(memento);

    /// <inheritdoc cref="Interfaces.IMemento{TSelf,TMemento}.Restore"/>
    public static TSelf Restore(TMemento memento)
    {
        var instance = (TSelf)RuntimeHelpers.GetUninitializedObject(typeof(TSelf));
        instance.RestoreInternal(memento);
        return instance;
    }

    /// <summary>
    /// Populates the provided memento with the current state of this value object.
    /// </summary>
    /// <param name="memento">The memento instance to populate.</param>
    protected abstract void SnapshotInternal(TMemento memento);

    /// <summary>
    /// Restores the state of this value object from the provided memento.
    /// </summary>
    /// <param name="memento">The memento instance to restore from.</param>
    protected abstract void RestoreInternal(TMemento memento);
}
