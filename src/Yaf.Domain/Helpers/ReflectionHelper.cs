using System.Linq.Expressions;
using System.Reflection;

namespace Yaf.Domain.Helpers;

/// <summary>
/// Compiled expression-tree helpers for reading and writing properties at runtime.
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
    /// Returns the property value boxed as <see cref="object"/>?.
    /// </summary>
    internal static Func<TEntity, object?> BuildPropertyReader<TEntity>(string propertyName)
    {
        var entityType = typeof(TEntity);
        var prop = entityType.GetProperty(propertyName)
            ?? throw MissingPropertyError(entityType, propertyName);

        var entityParam = Expression.Parameter(entityType, "entity");
        var body = Expression.Convert(Expression.Property(entityParam, prop), typeof(object));

        return Expression.Lambda<Func<TEntity, object?>>(body, entityParam).Compile();
    }

    /// <summary>
    /// Builds a compiled writer for a property on <typeparamref name="TEntity"/> from a boxed value.
    /// Handles value types (coalescing null to default) and reference types.
    /// Throws if the property lacks a setter.
    /// </summary>
    internal static Action<TEntity, object?> BuildPropertyWriter<TEntity>(string propertyName)
    {
        var entityType = typeof(TEntity);
        var prop = entityType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw MissingPropertyError(entityType, propertyName);

        var setter = prop.GetSetMethod(nonPublic: true)
            ?? throw new InvalidOperationException(
                $"{entityType.Name}.{propertyName} must have a setter " +
                $"(e.g., {{ get; private set; }}).");

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
        new($"{entityType.Name} is missing the '{propertyName}' property. " +
            $"Use implicit interface implementation " +
            $"(e.g., public ... {propertyName} {{ get; private set; }}).");
}
