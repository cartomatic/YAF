namespace Yaf.Domain.Interfaces;

/// <summary>
/// Enforces creation and modification timestamp tracking on entities.
/// </summary>
/// <remarks>
/// <para>
/// Both properties are nullable. <see langword="null"/> indicates the entity has not yet
/// completed a persistence round-trip. Infrastructure auto-populates these during
/// <c>SaveChanges</c>: <see cref="CreatedAtUtc"/> on first save,
/// <see cref="ModifiedAtUtc"/> on every subsequent save. All times are UTC.
/// </para>
/// <para>
/// Deletion timestamps are a separate concern — see <see cref="ISoftDeletable"/>.
/// </para>
/// </remarks>
public interface ITimestamped
{
    /// <summary>
    /// When this entity was created (UTC). <see langword="null"/> before first persistence.
    /// </summary>
    DateTimeOffset? CreatedAtUtc { get; set; }

    /// <summary>
    /// When this entity was last modified (UTC). <see langword="null"/> until first modification.
    /// </summary>
    DateTimeOffset? ModifiedAtUtc { get; set; }
}
