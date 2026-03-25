namespace Yaf.Domain;

public abstract record ValueObject<TSelf, TMemento> : ValueObject
    where TSelf : ValueObject<TSelf, TMemento>
    where TMemento : class
{
    public abstract void Snapshot(TMemento memento);
}
