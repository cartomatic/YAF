namespace Yaf.Domain.Interfaces;

/// <summary>
/// Non-generic marker interface for strongly-typed identifiers.
/// Used as a generic constraint on <c>Entity&lt;TId&gt;</c> to prevent raw primitive IDs.
/// </summary>
public interface ITypedId;

/// <summary>
/// Generic interface for strongly-typed identifiers exposing the backing value.
/// </summary>
/// <typeparam name="T">The backing value type (e.g., <see cref="Guid"/>, <see cref="int"/>).</typeparam>
public interface ITypedId<out T> : ITypedId
    where T : IEquatable<T>
{
    /// <summary>
    /// The backing value of this identifier.
    /// </summary>
    T Value { get; }
}
