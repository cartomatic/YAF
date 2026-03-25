namespace Yaf.Domain.Interfaces;

public interface IMemento<TSelf, TMemento>
    where TSelf : IMemento<TSelf, TMemento>
    where TMemento : class
{
    void Snapshot(TMemento memento);
    static abstract TSelf Restore(TMemento memento);
}
