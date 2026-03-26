namespace Yaf.Domain.Interfaces;

/// <summary>
/// Enforces an identity property on memento types, enabling base entity classes
/// to handle identity snapshot, restore, and hydrate without abstract methods.
/// </summary>
/// <typeparam name="T">The backing value type of the identity (e.g., <see cref="Guid"/>).</typeparam>
public interface IHasIdentity<T>
    where T : IEquatable<T>
{
    /// <summary>
    /// The identity value. Maps to the entity's <c>TypedId&lt;T&gt;.Value</c> in the domain layer.
    /// </summary>
    T Id { get; set; }
}
