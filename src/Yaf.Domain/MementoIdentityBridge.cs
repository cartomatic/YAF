using System.Reflection;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain;

/// <summary>
/// Cached reflection bridge that automatically handles identity transfer between
/// entities and mementos when <typeparamref name="TId"/> implements <see cref="ITypedId{T}"/>
/// and <typeparamref name="TMemento"/> implements <see cref="IHasIdentity{T}"/> with matching <c>T</c>.
/// </summary>
/// <typeparam name="TId">The strongly-typed identifier type.</typeparam>
/// <typeparam name="TMemento">The memento type.</typeparam>
internal static class MementoIdentityBridge<TId, TMemento>
    where TId : ITypedId
    where TMemento : class
{
    /// <summary>
    /// Writes the entity's identity value to the memento, or <c>null</c> if types don't match.
    /// </summary>
    internal static readonly Action<TMemento, TId>? WriteId;

    /// <summary>
    /// Creates a <typeparamref name="TId"/> from the memento's identity value, or <c>null</c> if types don't match.
    /// </summary>
    internal static readonly Func<TMemento, TId>? ReadId;

    /// <summary>
    /// Whether identity bridging is available for this type combination.
    /// </summary>
    internal static bool IsSupported => WriteId is not null && ReadId is not null;

    static MementoIdentityBridge()
    {
        // Find T from TId : ITypedId<T>
        var typedIdInterface = Array.Find(
            typeof(TId).GetInterfaces(),
            i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITypedId<>));

        if (typedIdInterface is null)
        {
            return;
        }

        var valueType = typedIdInterface.GetGenericArguments()[0];

        // Find IHasIdentity<T> on TMemento with matching T
        var hasIdentityInterface = Array.Find(
            typeof(TMemento).GetInterfaces(),
            i => i.IsGenericType
                 && i.GetGenericTypeDefinition() == typeof(IHasIdentity<>)
                 && i.GetGenericArguments()[0] == valueType);

        if (hasIdentityInterface is null)
        {
            return;
        }

        PropertyInfo valueProperty = typedIdInterface.GetProperty(nameof(ITypedId<int>.Value))!;
        PropertyInfo idProperty = hasIdentityInterface.GetProperty(nameof(IHasIdentity<int>.Id))!;
        ConstructorInfo? constructor = typeof(TId).GetConstructor([valueType]);

        if (constructor is null)
        {
            return;
        }

        WriteId = (memento, id) => idProperty.SetValue(memento, valueProperty.GetValue(id));

        ReadId = memento =>
        {
            object? rawValue = idProperty.GetValue(memento);
            return (TId)constructor.Invoke([rawValue]);
        };
    }
}
