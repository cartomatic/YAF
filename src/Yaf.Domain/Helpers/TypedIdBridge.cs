using System.Collections.Concurrent;
using System.Linq.Expressions;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain.Helpers;

/// <summary>
/// Compiled bridge for reading/writing typed ID properties between entities and mementos.
/// Handles boxing (entity → primitive via <see cref="ITypedId.BoxedValue"/>) and
/// unboxing (primitive → typed ID via cached constructor factory).
/// </summary>
internal sealed class TypedIdBridge<TSelf>
{
    private readonly Func<TSelf, object?> _reader1;
    private readonly Func<TSelf, object?>? _reader2;
    private readonly Action<TSelf, object?> _writer1;
    private readonly Action<TSelf, object?>? _writer2;
    private readonly Func<object, object> _factory;

    private TypedIdBridge(
        Func<TSelf, object?> reader1, Func<TSelf, object?>? reader2,
        Action<TSelf, object?> writer1, Action<TSelf, object?>? writer2,
        Func<object, object> factory)
    {
        _reader1 = reader1;
        _reader2 = reader2;
        _writer1 = writer1;
        _writer2 = writer2;
        _factory = factory;
    }

    /// <summary>
    /// Reads typed ID properties, extracts their primitive BoxedValue.
    /// </summary>
    internal (object? first, object? second) ReadPair(TSelf entity)
    {
        var v1 = _reader1(entity);
        var v2 = _reader2?.Invoke(entity);
        return (
            v1 is ITypedId t1 ? t1.BoxedValue : null,
            v2 is ITypedId t2 ? t2.BoxedValue : null);
    }

    /// <summary>
    /// Reconstructs typed IDs from primitive values and sets them on the entity.
    /// </summary>
    internal void WritePair(TSelf entity, object? first, object? second)
    {
        _writer1(entity, first is null ? null : _factory(first));
        _writer2?.Invoke(entity, second is null ? null : _factory(second));
    }

    /// <summary>
    /// Builds a bridge if TSelf implements the open generic domain interface and TMemento implements the memento interface.
    /// Returns <see langword="null"/> if the interfaces are not implemented.
    /// </summary>
    internal static TypedIdBridge<TSelf>? TryBuild<TMemento>(
        Type openGenericDomain, Type mementoInterface, string prop1Name, string? prop2Name = null)
    {
        var entityType = typeof(TSelf);
        if (!mementoInterface.IsAssignableFrom(typeof(TMemento)))
            return null;

        var genericInterface = ReflectionHelper.FindGenericInterface(entityType, openGenericDomain);
        if (genericInterface is null)
            return null;

        var typedIdType = genericInterface.GetGenericArguments()[0];
        var factory = TypedIdFactoryCache.GetOrBuild(typedIdType);

        var reader1 = ReflectionHelper.BuildPropertyReader<TSelf>(prop1Name);
        var writer1 = ReflectionHelper.BuildPropertyWriter<TSelf>(prop1Name);

        Func<TSelf, object?>? reader2 = null;
        Action<TSelf, object?>? writer2 = null;
        if (prop2Name is not null)
        {
            reader2 = ReflectionHelper.BuildPropertyReader<TSelf>(prop2Name);
            writer2 = ReflectionHelper.BuildPropertyWriter<TSelf>(prop2Name);
        }

        return new TypedIdBridge<TSelf>(reader1, reader2, writer1, writer2, factory);
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
