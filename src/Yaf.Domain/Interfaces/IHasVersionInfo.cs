namespace Yaf.Domain.Interfaces;

/// <summary>
/// Enforces an optimistic concurrency token on memento types.
/// When present, infrastructure auto-configures <see cref="Version"/> as an EF Core
/// concurrency token: on save, a new <see cref="Guid"/> is generated; on conflict,
/// <c>DbUpdateConcurrencyException</c> is thrown.
/// </summary>
/// <remarks>
/// This is a memento-only interface — domain objects do not access the version property.
/// Opt-in: only mementos implementing this interface get optimistic concurrency.
/// </remarks>
public interface IHasVersionInfo
{
    /// <summary>
    /// The concurrency token. Application-managed <see cref="Guid"/>, portable across databases.
    /// </summary>
    Guid Version { get; set; }
}
