using System.Linq.Expressions;
using System.Reflection;

namespace Yaf.Domain.Helpers;

/// <summary>
/// Compiled expression-tree helper for writing properties at runtime.
/// </summary>
internal static class ReflectionHelper
{
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
