using System.Runtime.CompilerServices;

namespace Yaf.Domain;

/// <summary>
/// Base record for value objects that support memento-based persistence.
/// Provides concrete <see cref="Snapshot"/> and <see cref="Restore"/> implementations
/// that delegate to <see cref="SnapshotInternal"/> and <see cref="RestoreInternal"/>.
/// </summary>
/// <remarks>
/// <para>
/// Concrete types must also declare <c>: IMemento&lt;TSelf, TMemento&gt;</c> on their type definition
/// due to a C# limitation where inherited static methods cannot satisfy <c>static abstract</c>
/// interface members for generic dispatch.
/// </para>
/// <para>
/// <see cref="Restore"/> uses <see cref="RuntimeHelpers.GetUninitializedObject"/> to create
/// an instance without calling a constructor, then populates it via <see cref="RestoreInternal"/>.
/// Properties set in <see cref="RestoreInternal"/> must use <c>private set</c> (not <c>init</c>).
/// </para>
/// </remarks>
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
    /// Override this method to map domain properties to memento properties.
    /// </summary>
    /// <param name="memento">The memento instance to populate.</param>
    protected abstract void SnapshotInternal(TMemento memento);

    /// <summary>
    /// Restores the state of this value object from the provided memento.
    /// Override this method to map memento properties back to domain properties.
    /// </summary>
    /// <param name="memento">The memento instance to restore from.</param>
    protected abstract void RestoreInternal(TMemento memento);
}
