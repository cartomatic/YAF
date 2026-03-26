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
public abstract record TypedId<T>(T Value) : ITypedId<T>
    where T : IEquatable<T>;
