namespace Yaf.Domain.Interfaces;

/// <summary>
/// Enforces an identity property on memento types, enabling base entity classes
/// to handle identity snapshot, restore, and hydrate automatically.
/// </summary>
/// <remarks>
/// All identities are <see cref="Guid"/>-backed. The typed ID wrapper (e.g., <c>OrderId</c>)
/// is reconstructed during hydration.
/// </remarks>
public interface IHasIdentity
{
    /// <summary>
    /// The identity value. <see langword="null"/> when the identity has not been set.
    /// </summary>
    Guid? Id { get; set; }
}
