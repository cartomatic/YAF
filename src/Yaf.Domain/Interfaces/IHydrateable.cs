namespace Yaf.Domain.Interfaces;

/// <summary>
/// Defines in-place hydration for mutable domain objects (entities).
/// Allows infrastructure to reload state into an existing tracked instance
/// (e.g., an EF Core change-tracked entity) without creating a new object.
/// </summary>
/// <remarks>
/// Value objects are immutable and should not implement this interface.
/// Use <see cref="IMemento{TSelf,TMemento}.Restore"/> instead.
/// Infrastructure can check <c>is IHydrateable&lt;TMemento&gt;</c> to decide
/// between hydrate-in-place and restore-as-new.
/// </remarks>
/// <typeparam name="TMemento">The memento type to hydrate from.</typeparam>
public interface IHydrateable<TMemento>
    where TMemento : class
{
    /// <summary>
    /// Reloads the state of this domain object from the provided memento.
    /// </summary>
    /// <param name="memento">The memento instance to hydrate from, provided by infrastructure.</param>
    void Hydrate(TMemento memento);
}
