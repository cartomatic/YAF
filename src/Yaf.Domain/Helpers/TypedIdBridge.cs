using System.Collections.Concurrent;
using System.Linq.Expressions;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain.Helpers;

/// <summary>
/// Compiled bridge for reading/writing a single typed ID property between an entity and a memento.
/// Handles conversion between typed IDs (e.g., <c>UserId</c>) and their <see cref="Guid"/> values.
/// </summary>
internal sealed class TypedIdBridge<TSelf>
{
    private readonly Func<TSelf, object?> _reader;
    private readonly Action<TSelf, object?> _writer;
    private readonly Func<Guid, object> _factory;

    private TypedIdBridge(Func<TSelf, object?> reader, Action<TSelf, object?> writer, Func<Guid, object> factory)
    {
        _reader = reader;
        _writer = writer;
        _factory = factory;
    }

    /// <summary>
    /// Reads a typed ID property and extracts its <see cref="Guid"/> value.
    /// Returns <see langword="null"/> if the property value is null.
    /// </summary>
    internal Guid? Read(TSelf entity)
    {
        var value = _reader(entity);
        return value is ITypedId typedId ? typedId.Value : null;
    }

    /// <summary>
    /// Reconstructs a typed ID from a <see cref="Guid"/> value and sets it on the entity.
    /// Sets <see langword="null"/> if the input is null.
    /// </summary>
    internal void Write(TSelf entity, Guid? guid) =>
        _writer(entity, guid.HasValue ? _factory(guid.Value) : null);

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
/// All typed IDs use <see cref="Guid"/> as their backing value.
/// </summary>
internal static class TypedIdFactoryCache
{
    private static readonly ConcurrentDictionary<Type, Func<Guid, object>> _cache = new();

    internal static Func<Guid, object> GetOrBuild(Type typedIdType) =>
        _cache.GetOrAdd(typedIdType, static type =>
        {
            var constructor = type.GetConstructor([typeof(Guid)])
                ?? throw new InvalidOperationException(
                    $"{type.Name} must have a public constructor accepting a single " +
                    $"Guid parameter. " +
                    $"Use positional record syntax: record {type.Name}(Guid Value)");

            var param = Expression.Parameter(typeof(Guid), "value");
            var body = Expression.New(constructor, param);
            return Expression.Lambda<Func<Guid, object>>(Expression.Convert(body, typeof(object)), param).Compile();
        });
}
