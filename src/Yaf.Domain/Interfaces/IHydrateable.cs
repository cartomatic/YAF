namespace Yaf.Domain.Interfaces;

public interface IHydrateable<TMemento>
    where TMemento : class
{
    void Hydrate(TMemento memento);
}
