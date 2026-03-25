namespace Yaf.Domain.Interfaces;

/// <summary>
/// Defines the memento contract for domain objects that support persistence via snapshot and restore.
/// Infrastructure provides concrete memento instances; the domain object populates or restores from them.
/// </summary>
/// <typeparam name="TSelf">The concrete type implementing this interface (CRTP pattern).</typeparam>
/// <typeparam name="TMemento">The memento type — an interface at the domain level, a concrete DTO in infrastructure.</typeparam>
public interface IMemento<TSelf, TMemento>
    where TSelf : IMemento<TSelf, TMemento>
    where TMemento : class
{
    /// <summary>
    /// Populates the provided memento with the current state of the domain object.
    /// Called by infrastructure during the save path.
    /// </summary>
    /// <param name="memento">The memento instance to populate, provided by infrastructure.</param>
    void Snapshot(TMemento memento);

    /// <summary>
    /// Creates a new instance of the domain object from the provided memento.
    /// Called by infrastructure during the load path.
    /// </summary>
    /// <param name="memento">The memento instance to restore from, provided by infrastructure.</param>
    /// <returns>A new instance of <typeparamref name="TSelf"/> populated from the memento.</returns>
    static abstract TSelf Restore(TMemento memento);
}
