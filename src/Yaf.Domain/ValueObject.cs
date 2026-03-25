namespace Yaf.Domain;

/// <summary>
/// Marker base record for value objects. Provides structural equality and immutability
/// via C# record semantics. Inherit from this type to declare value object semantics.
/// </summary>
/// <example>
/// <code>
/// public record Address(string Street, string City, string PostalCode) : ValueObject;
/// </code>
/// </example>
public abstract record ValueObject;
