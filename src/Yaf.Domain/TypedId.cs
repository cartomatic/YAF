using Yaf.Domain.Interfaces;

namespace Yaf.Domain;

/// <summary>
/// Abstract base record for strongly-typed identifiers.
/// Consumer derives concrete ID types: <c>public record OrderId(Guid Value) : TypedId&lt;Guid&gt;(Value);</c>
/// </summary>
/// <remarks>
/// Record semantics provide value equality and <see cref="object.ToString"/> for free.
/// The backing type <typeparamref name="T"/> is constrained to <see cref="IEquatable{T}"/>
/// to ensure proper equality semantics.
/// </remarks>
/// <typeparam name="T">The backing value type (typically <see cref="Guid"/>).</typeparam>
public abstract record TypedId<T> : ITypedId<T>
    where T : IEquatable<T>
{
    /// <inheritdoc cref="ITypedId{T}.Value"/>
    public T Value { get; }

    /// <inheritdoc cref="ITypedId.IdentityType"/>
    static Type ITypedId.IdentityType => typeof(T);

    /// <inheritdoc />
    public Type ValueType => typeof(T);

    /// <inheritdoc />
    public object BoxedValue => Value!;

    /// <summary>
    /// Initializes a new instance of the typed identifier with the specified backing value.
    /// </summary>
    /// <param name="value">The backing value.</param>
    protected TypedId(T value)
    {
        Value = value;
    }
}
