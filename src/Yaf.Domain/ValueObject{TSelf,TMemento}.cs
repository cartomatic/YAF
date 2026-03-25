using System.Runtime.CompilerServices;

namespace Yaf.Domain;

public abstract record ValueObject<TSelf, TMemento> : ValueObject
    where TSelf : ValueObject<TSelf, TMemento>
    where TMemento : class
{
    public void Snapshot(TMemento memento) => SnapshotInternal(memento);

    public static TSelf Restore(TMemento memento)
    {
        var instance = (TSelf)RuntimeHelpers.GetUninitializedObject(typeof(TSelf));
        instance.RestoreInternal(memento);
        return instance;
    }

    protected abstract void SnapshotInternal(TMemento memento);

    protected abstract void RestoreInternal(TMemento memento);
}
