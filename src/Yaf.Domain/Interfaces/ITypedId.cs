namespace Yaf.Domain.Interfaces;

/// <summary>
/// Non-generic base interface for strongly-typed identifiers.
/// Provides runtime access to the backing value type and boxed value
/// for cross-type identity operations.
/// </summary>
public interface ITypedId
{
    /// <summary>
    /// The <see cref="Type"/> of the backing value (e.g., <c>typeof(Guid)</c>).
    /// </summary>
    Type ValueType { get; }

    /// <summary>
    /// The backing value boxed as <see cref="object"/>.
    /// </summary>
    object BoxedValue { get; }
}

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
