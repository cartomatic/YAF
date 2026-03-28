using Yaf.Domain.Interfaces;

namespace Yaf.Domain;

/// <summary>
/// Abstract base record for strongly-typed identifiers backed by <see cref="Guid"/>.
/// Consumer derives concrete ID types: <c>public record OrderId(Guid Value) : TypedId(Value);</c>
/// </summary>
/// <remarks>
/// <para>
/// Record semantics provide value equality and <see cref="object.ToString"/> for free.
/// </para>
/// <para>
/// Derived types must use positional record syntax (e.g., <c>record OrderId(Guid Value)</c>)
/// which generates a public constructor accepting a single <see cref="Guid"/> parameter.
/// This constructor is required for memento identity reconstruction via
/// <see cref="System.Activator.CreateInstance(Type, object[])"/> in <c>MementoHelper</c>.
/// </para>
/// </remarks>
public abstract record TypedId : ITypedId
{
    /// <inheritdoc cref="ITypedId.Value"/>
    public Guid Value { get; }

    /// <summary>
    /// Initializes a new instance of the typed identifier with the specified backing value.
    /// </summary>
    /// <param name="value">The backing <see cref="Guid"/> value.</param>
    protected TypedId(Guid value)
    {
        Value = value;
    }
}
