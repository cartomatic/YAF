namespace Yaf.Domain;

/// <summary>
/// Framework-provided actor identifier representing who performed an operation.
/// Covers human users, service accounts, and automation bots.
/// Consumers may use this directly or define their own actor ID type by extending <see cref="TypedId"/>.
/// </summary>
/// <param name="Value">The backing <see cref="Guid"/> value.</param>
public record ActorId(Guid Value) : TypedId(Value);
