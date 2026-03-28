namespace Yaf.Domain.Interfaces;

/// <summary>
/// Interface for strongly-typed identifiers backed by <see cref="Guid"/>.
/// Consumer derives concrete ID types: <c>public record OrderId(Guid Value) : TypedId(Value);</c>
/// </summary>
/// <remarks>
/// All typed identifiers use <see cref="Guid"/> as the backing value. External systems
/// with non-Guid identifiers (int, string, etc.) remap at the anti-corruption layer boundary.
/// </remarks>
public interface ITypedId
{
    /// <summary>
    /// The backing <see cref="Guid"/> value of this identifier.
    /// </summary>
    Guid Value { get; }
}
