namespace Yaf.Domain.Interfaces;

/// <summary>
/// Memento contract for domain objects that support persistence via snapshot and restore.
/// </summary>
/// <typeparam name="TSelf">The concrete type implementing this interface (CRTP pattern).</typeparam>
/// <typeparam name="TMemento">The memento type — an interface at the domain level, a concrete DTO in infrastructure.</typeparam>
public interface IMemento<TSelf, TMemento>
    where TSelf : IMemento<TSelf, TMemento>
    where TMemento : class
{
    /// <summary>
    /// Populates the provided memento with the current state of the domain object.
    /// </summary>
    /// <param name="memento">The memento instance to populate.</param>
    void Snapshot(TMemento memento);

    /// <summary>
    /// Creates a new instance of the domain object from the provided memento.
    /// </summary>
    /// <param name="memento">The memento instance to restore from.</param>
    /// <returns>A new instance of <typeparamref name="TSelf"/> populated from the memento.</returns>
    static abstract TSelf Restore(TMemento memento);
}
