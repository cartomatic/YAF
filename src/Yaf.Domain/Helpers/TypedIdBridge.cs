using System.Collections.Concurrent;
using System.Linq.Expressions;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain.Helpers;

/// <summary>
/// Compiled bridge for reading/writing a single typed ID property between an entity and a memento.
/// Handles boxing (entity → primitive via <see cref="ITypedId.BoxedValue"/>) and
/// unboxing (primitive → typed ID via cached constructor factory).
/// </summary>
internal sealed class TypedIdBridge<TSelf>
{
    private readonly Func<TSelf, object?> _reader;
    private readonly Action<TSelf, object?> _writer;
    private readonly Func<object, object> _factory;

    private TypedIdBridge(Func<TSelf, object?> reader, Action<TSelf, object?> writer, Func<object, object> factory)
    {
        _reader = reader;
        _writer = writer;
        _factory = factory;
    }

    /// <summary>
    /// Reads a typed ID property and extracts its primitive BoxedValue.
    /// Returns <see langword="null"/> if the property value is null.
    /// </summary>
    internal object? Read(TSelf entity)
    {
        var value = _reader(entity);
        return value is ITypedId typedId ? typedId.BoxedValue : null;
    }

    /// <summary>
    /// Reconstructs a typed ID from a primitive value and sets it on the entity.
    /// Sets <see langword="null"/> if the input is null.
    /// </summary>
    internal void Write(TSelf entity, object? boxedPrimitive) =>
        _writer(entity, boxedPrimitive is null ? null : _factory(boxedPrimitive));

    /// <summary>
    /// Builds a bridge for the given property if <typeparamref name="TSelf"/> implements
    /// the open generic domain interface and <c>TMemento</c> implements the memento interface.
    /// Returns <see langword="null"/> if the interfaces are not implemented.
    /// </summary>
    internal static TypedIdBridge<TSelf>? TryBuild<TMemento>(
        Type openGenericDomain, Type mementoInterface, string propertyName)
    {
        var entityType = typeof(TSelf);
        if (!mementoInterface.IsAssignableFrom(typeof(TMemento)))
            return null;

        var genericInterface = ReflectionHelper.FindGenericInterface(entityType, openGenericDomain);
        if (genericInterface is null)
            return null;

        var typedIdType = genericInterface.GetGenericArguments()[0];
        var factory = TypedIdFactoryCache.GetOrBuild(typedIdType);
        var reader = ReflectionHelper.BuildPropertyReader<TSelf>(propertyName);
        var writer = ReflectionHelper.BuildPropertyWriter<TSelf>(propertyName);

        return new TypedIdBridge<TSelf>(reader, writer, factory);
    }
}

/// <summary>
/// Cached compiled typed ID constructor delegates. Thread-safe.
/// </summary>
internal static class TypedIdFactoryCache
{
    private static readonly ConcurrentDictionary<Type, Func<object, object>> _cache = new();

    internal static Func<object, object> GetOrBuild(Type typedIdType) =>
        _cache.GetOrAdd(typedIdType, static type =>
        {
            var typedIdGeneric = ReflectionHelper.FindGenericInterface(type, typeof(ITypedId<>))
                ?? throw new InvalidOperationException(
                    $"{type.Name} implements ITypedId but does not implement ITypedId<T>.");

            var backingType = typedIdGeneric.GetGenericArguments()[0];
            var constructor = type.GetConstructor([backingType])
                ?? throw new InvalidOperationException(
                    $"{type.Name} must have a public constructor accepting a single " +
                    $"{backingType.Name} parameter. " +
                    $"Use positional record syntax: record {type.Name}({backingType.Name} Value)");

            var param = Expression.Parameter(typeof(object), "value");
            var body = Expression.New(constructor, Expression.Convert(param, backingType));
            return Expression.Lambda<Func<object, object>>(Expression.Convert(body, typeof(object)), param).Compile();
        });
}
