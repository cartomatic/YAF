using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain.Helpers;

/// <summary>
/// Compiled expression-tree helpers for reading and writing entity properties
/// at runtime. Used by <see cref="MementoHelper{TId,TSelf,TMemento}"/> to auto-handle
/// cross-cutting concerns during Snapshot/Restore/Hydrate.
/// </summary>
internal static class ReflectionHelper
{
    /// <summary>
    /// Finds a closed generic interface on a type (e.g., <c>IAccountable&lt;UserId&gt;</c>
    /// from <c>typeof(IAccountable&lt;&gt;)</c>).
    /// </summary>
    internal static Type? FindGenericInterface(Type type, Type openGenericInterface) =>
        Array.Find(
            type.GetInterfaces(),
            i => i.IsGenericType && i.GetGenericTypeDefinition() == openGenericInterface);

    /// <summary>
    /// Builds a compiled reader for a property on <typeparamref name="TEntity"/>.
    /// If the property type implements <see cref="ITypedId"/>, extracts <see cref="ITypedId.BoxedValue"/>
    /// (returning <see langword="null"/> when the property is null).
    /// Otherwise, returns the value boxed as <see cref="object"/>.
    /// </summary>
    internal static Func<TEntity, object?> BuildPropertyReader<TEntity>(string propertyName)
    {
        var entityType = typeof(TEntity);
        var prop = entityType.GetProperty(propertyName)
            ?? throw MissingPropertyError(entityType, propertyName);

        var entityParam = Expression.Parameter(entityType, "entity");

        Expression body = IsTypedIdProperty(prop)
            ? BuildBoxedValueExtractor(entityParam, prop)
            : Expression.Convert(Expression.Property(entityParam, prop), typeof(object));

        return Expression.Lambda<Func<TEntity, object?>>(body, entityParam).Compile();
    }

    /// <summary>
    /// Builds a compiled writer for a property on <typeparamref name="TEntity"/> from a boxed value.
    /// If the property type implements <see cref="ITypedId"/>, reconstructs the typed ID from
    /// the boxed primitive via a cached compiled factory. <see langword="null"/> input sets the
    /// property to null.
    /// Otherwise, sets the value directly (handling value-type coalescing to default).
    /// Throws if the property lacks a setter.
    /// </summary>
    internal static Action<TEntity, object?> BuildPropertyWriter<TEntity>(string propertyName)
    {
        var entityType = typeof(TEntity);
        var prop = entityType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw MissingPropertyError(entityType, propertyName);

        var setter = prop.GetSetMethod(nonPublic: true)
            ?? throw new InvalidOperationException(
                $"{entityType.Name}.{propertyName} must have a setter (e.g., {{ get; private set; }}) " +
                $"for automatic memento handling. Add a private setter or handle {propertyName} " +
                $"manually in SnapshotCore/RestoreCore/HydrateCore.");

        var rawSetter = CompilePropertySetter<TEntity>(entityType, prop, setter);

        var typedIdType = GetTypedIdType(prop);
        if (typedIdType is null)
        {
            return rawSetter;
        }

        var factory = TypedIdFactoryCache.GetOrBuild(typedIdType);
        return (entity, boxedValue) =>
            rawSetter(entity, boxedValue is null ? null : factory(boxedValue));
    }

    /// <summary>
    /// Determines whether a property's type implements <see cref="ITypedId"/>.
    /// </summary>
    private static bool IsTypedIdProperty(PropertyInfo prop) =>
        GetTypedIdType(prop) is not null;

    /// <summary>
    /// Returns the concrete <see cref="ITypedId"/> type for a property, or <see langword="null"/>
    /// if the property type does not implement it.
    /// </summary>
    private static Type? GetTypedIdType(PropertyInfo prop)
    {
        var type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
        return typeof(ITypedId).IsAssignableFrom(type) ? type : null;
    }

    /// <summary>
    /// Builds an expression that reads a typed ID property and extracts its
    /// <see cref="ITypedId.BoxedValue"/>, returning null if the property is null.
    /// </summary>
    private static Expression BuildBoxedValueExtractor(ParameterExpression entityParam, PropertyInfo property)
    {
        var propAccess = Expression.Property(entityParam, property);
        var boxedValueProp = typeof(ITypedId).GetProperty(nameof(ITypedId.BoxedValue))!;

        if (Nullable.GetUnderlyingType(property.PropertyType) is not null || !property.PropertyType.IsValueType)
        {
            var asTypedId = Expression.Convert(propAccess, typeof(ITypedId));
            var boxedValue = Expression.Property(asTypedId, boxedValueProp);
            return Expression.Condition(
                Expression.Equal(propAccess, Expression.Constant(null, property.PropertyType)),
                Expression.Constant(null, typeof(object)),
                Expression.Convert(boxedValue, typeof(object)));
        }

        var directCast = Expression.Convert(propAccess, typeof(ITypedId));
        return Expression.Convert(Expression.Property(directCast, boxedValueProp), typeof(object));
    }

    /// <summary>
    /// Compiles a raw property setter as <c>Action&lt;TEntity, object?&gt;</c>.
    /// </summary>
    private static Action<TEntity, object?> CompilePropertySetter<TEntity>(
        Type entityType, PropertyInfo prop, MethodInfo setter)
    {
        var entityParam = Expression.Parameter(entityType, "entity");
        var valueParam = Expression.Parameter(typeof(object), "value");

        var convertedValue = prop.PropertyType.IsValueType
            ? Expression.Convert(
                Expression.Coalesce(valueParam, Expression.Default(prop.PropertyType)),
                prop.PropertyType)
            : Expression.Convert(valueParam, prop.PropertyType);

        var call = Expression.Call(entityParam, setter, convertedValue);
        return Expression.Lambda<Action<TEntity, object?>>(call, entityParam, valueParam).Compile();
    }

    private static InvalidOperationException MissingPropertyError(Type entityType, string propertyName) =>
        new($"{entityType.Name} implements a cross-cutting interface but is missing " +
            $"the '{propertyName}' property. Use implicit interface implementation " +
            $"(e.g., public ... {propertyName} {{ get; private set; }}).");

    /// <summary>
    /// Shared cache of compiled typed ID factories, keyed by the concrete typed ID type.
    /// Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/>.
    /// </summary>
    private static class TypedIdFactoryCache
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
                var converted = Expression.Convert(body, typeof(object));
                return Expression.Lambda<Func<object, object>>(converted, param).Compile();
            });
    }
}
