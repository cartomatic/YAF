namespace Yaf.Domain.Interfaces;

/// <summary>
/// Enforces soft-deletion properties on memento types, enabling base entity classes
/// to handle soft-delete snapshot, restore, and hydrate automatically.
/// </summary>
/// <remarks>
/// When present on a memento, infrastructure auto-applies an EF Core global query filter:
/// <c>WHERE DeletedAtUtc IS NULL</c>.
/// The actor identity is stored as a <see cref="Guid"/> value. The typed ID wrapper
/// (e.g., <c>UserId</c>) is reconstructed during hydration.
/// </remarks>
public interface IHasSoftDelete
{
    /// <summary>
    /// When the entity was soft-deleted (UTC). <see langword="null"/> if not deleted.
    /// </summary>
    DateTimeOffset? DeletedAtUtc { get; set; }

    /// <summary>
    /// The deleter's identity value. <see langword="null"/> if not deleted.
    /// </summary>
    Guid? DeletedBy { get; set; }
}
