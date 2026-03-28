namespace Yaf.Domain;

/// <summary>
/// Framework-provided tenant identifier. Consumers may use this or define their own
/// tenant ID type by extending <see cref="TypedId{T}"/>.
/// </summary>
/// <param name="Value">The backing <see cref="Guid"/> value.</param>
public record TenantId(Guid Value) : TypedId<Guid>(Value);
