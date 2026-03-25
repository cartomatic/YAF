namespace Yaf.Domain.Interfaces;

/// <summary>
/// In-place hydration for mutable domain objects (entities).
/// Value objects are immutable and should not implement this interface.
/// </summary>
/// <typeparam name="TMemento">The memento type to hydrate from.</typeparam>
public interface IHydrateable<TMemento>
    where TMemento : class
{
    /// <summary>
    /// Reloads the state of this domain object from the provided memento.
    /// </summary>
    /// <param name="memento">The memento instance to hydrate from.</param>
    void Hydrate(TMemento memento);
}
