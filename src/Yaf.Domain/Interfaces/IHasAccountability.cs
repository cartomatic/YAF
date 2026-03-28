namespace Yaf.Domain.Interfaces;

/// <summary>
/// Enforces accountability properties on memento types, enabling base entity classes
/// to handle accountability snapshot, restore, and hydrate automatically.
/// </summary>
/// <remarks>
/// Actor identities are stored as <see cref="Guid"/> values. The typed ID wrapper
/// (e.g., <c>UserId</c>) is reconstructed during hydration.
/// </remarks>
public interface IHasAccountability
{
    /// <summary>
    /// The creator's identity value. <see langword="null"/> before first persistence.
    /// </summary>
    Guid? CreatedBy { get; set; }

    /// <summary>
    /// The last modifier's identity value. <see langword="null"/> until first modification.
    /// </summary>
    Guid? ModifiedBy { get; set; }
}
