namespace Yaf.Domain.Interfaces;

/// <summary>
/// Enforces creation and modification timestamp properties on memento types,
/// enabling base entity classes to handle timestamp snapshot, restore, and hydrate automatically.
/// </summary>
/// <remarks>
/// Infrastructure auto-populates these during <c>SaveChanges</c>:
/// <see cref="CreatedAtUtc"/> on first save, <see cref="ModifiedAtUtc"/> on every subsequent save.
/// </remarks>
public interface IHasTimestamps
{
    /// <summary>
    /// When the entity was created (UTC). <see langword="null"/> before first persistence.
    /// </summary>
    DateTimeOffset? CreatedAtUtc { get; set; }

    /// <summary>
    /// When the entity was last modified (UTC). <see langword="null"/> until first modification.
    /// </summary>
    DateTimeOffset? ModifiedAtUtc { get; set; }
}
