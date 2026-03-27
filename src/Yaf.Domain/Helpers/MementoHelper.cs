using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain.Helpers;

/// <summary>
/// Shared memento orchestration logic used by both <see cref="Entity{TId,TSelf,TMemento}"/>
/// and <see cref="AggregateRoot{TId,TSelf,TMemento}"/> to avoid duplication.
/// </summary>
internal static class MementoHelper<TId, TSelf, TMemento>
    where TId : ITypedId
    where TSelf : Entity<TId>
    where TMemento : class
{
    private static readonly Func<object, TId>? _idFactory = BuildIdFactory();

    // Lazy-initialized cross-cutting concern handlers (one-time cost per generic instantiation).
    private static CrossCuttingHandlers? _handlers;

    /// <summary>
    /// Writes the entity's identity to the memento if types are compatible.
    /// </summary>
    internal static void WriteIdentity(TMemento memento, TId id)
    {
        if (memento is IHasIdentity hasIdentity && hasIdentity.IdentityType == TId.IdentityType)
        {
            hasIdentity.BoxedId = id.BoxedValue;
        }
    }

    /// <summary>
    /// Reads the identity from the memento and constructs a <typeparamref name="TId"/> if types are compatible.
    /// Returns <c>default</c> if the memento does not implement <see cref="IHasIdentity"/>
    /// or the identity types are incompatible.
    /// </summary>
    internal static (TId? id, bool success) ReadIdentity(TMemento memento)
    {
        if (memento is IHasIdentity hasIdentity && hasIdentity.IdentityType == TId.IdentityType)
        {
            if (_idFactory is null)
            {
                throw new InvalidOperationException(
                    $"{typeof(TId).Name} must have a public constructor accepting a single " +
                    $"{TId.IdentityType.Name} parameter for automatic identity restoration. " +
                    $"Use positional record syntax: record {typeof(TId).Name}({TId.IdentityType.Name} Value)");
            }

            return (_idFactory(hasIdentity.BoxedId), true);
        }

        return (default, false);
    }

    /// <summary>
    /// Writes accountability fields from entity to memento if both implement matching interfaces.
    /// </summary>
    internal static void WriteAccountability(TMemento memento, TSelf entity)
    {
        if (entity is not IAccountable || memento is not IHasAccountability hasAccountability)
            return;

        var handlers = EnsureHandlers();
        if (handlers.AccountabilityReader is null)
            return;

        var (createdBy, modifiedBy) = handlers.AccountabilityReader(entity);
        hasAccountability.BoxedCreatedBy = createdBy;
        hasAccountability.BoxedModifiedBy = modifiedBy;
    }

    /// <summary>
    /// Reads accountability fields from memento and sets them on the entity.
    /// </summary>
    internal static void ReadAccountability(TMemento memento, TSelf entity)
    {
        if (entity is not IAccountable || memento is not IHasAccountability hasAccountability)
            return;

        var handlers = EnsureHandlers();
        if (handlers.AccountabilityWriter is null)
            return;

        handlers.AccountabilityWriter(entity, hasAccountability.BoxedCreatedBy, hasAccountability.BoxedModifiedBy);
    }

    /// <summary>
    /// Writes timestamp fields from entity to memento.
    /// </summary>
    internal static void WriteTimestamps(TMemento memento, TSelf entity)
    {
        if (entity is not ITimestamped timestamped || memento is not IHasTimestamps hasTimestamps)
            return;

        hasTimestamps.CreatedAtUtc = timestamped.CreatedAtUtc;
        hasTimestamps.ModifiedAtUtc = timestamped.ModifiedAtUtc;
    }

    /// <summary>
    /// Reads timestamp fields from memento and sets them on the entity.
    /// </summary>
    internal static void ReadTimestamps(TMemento memento, TSelf entity)
    {
        if (entity is not ITimestamped || memento is not IHasTimestamps hasTimestamps)
            return;

        var handlers = EnsureHandlers();
        handlers.TimestampWriter?.Invoke(entity, hasTimestamps.CreatedAtUtc, hasTimestamps.ModifiedAtUtc);
    }

    /// <summary>
    /// Writes soft-delete fields from entity to memento.
    /// </summary>
    internal static void WriteSoftDelete(TMemento memento, TSelf entity)
    {
        if (entity is not ISoftDeletable || memento is not IHasSoftDelete hasSoftDelete)
            return;

        var handlers = EnsureHandlers();
        if (handlers.SoftDeleteReader is null)
            return;

        var (deletedAtUtc, deletedBy) = handlers.SoftDeleteReader(entity);
        hasSoftDelete.DeletedAtUtc = deletedAtUtc;
        hasSoftDelete.BoxedDeletedBy = deletedBy;
    }

    /// <summary>
    /// Reads soft-delete fields from memento and sets them on the entity.
    /// </summary>
    internal static void ReadSoftDelete(TMemento memento, TSelf entity)
    {
        if (entity is not ISoftDeletable || memento is not IHasSoftDelete hasSoftDelete)
            return;

        var handlers = EnsureHandlers();
        handlers.SoftDeleteWriter?.Invoke(entity, hasSoftDelete.DeletedAtUtc, hasSoftDelete.BoxedDeletedBy);
    }

    /// <summary>
    /// Writes tenant ID from entity to memento.
    /// </summary>
    internal static void WriteTenantId(TMemento memento, TSelf entity)
    {
        if (entity is not ITenantScoped || memento is not IHasTenantId hasTenantId)
            return;

        var handlers = EnsureHandlers();
        if (handlers.TenantReader is null)
            return;

        var tenantBoxed = handlers.TenantReader(entity);
        if (tenantBoxed is not null)
        {
            hasTenantId.BoxedTenantId = tenantBoxed;
        }
    }

    /// <summary>
    /// Reads tenant ID from memento and sets it on the entity.
    /// </summary>
    internal static void ReadTenantId(TMemento memento, TSelf entity)
    {
        if (entity is not ITenantScoped || memento is not IHasTenantId hasTenantId)
            return;

        var handlers = EnsureHandlers();
        handlers.TenantWriter?.Invoke(entity, hasTenantId.BoxedTenantId);
    }

    /// <summary>
    /// Creates a new <typeparamref name="TSelf"/> instance via <see cref="RuntimeHelpers.GetUninitializedObject"/>.
    /// </summary>
    internal static TSelf CreateUninitializedInstance() =>
        (TSelf)RuntimeHelpers.GetUninitializedObject(typeof(TSelf));

    private static CrossCuttingHandlers EnsureHandlers() =>
        _handlers ??= CrossCuttingHandlers.Build();

    private static Func<object, TId>? BuildIdFactory()
    {
        var backingType = TId.IdentityType;
        var constructor = typeof(TId).GetConstructor([backingType]);

        if (constructor is null)
        {
            return null;
        }

        var param = Expression.Parameter(typeof(object), "value");
        var body = Expression.New(constructor, Expression.Convert(param, backingType));
        return Expression.Lambda<Func<object, TId>>(body, param).Compile();
    }

    /// <summary>
    /// Cached compiled delegates for cross-cutting concern read/write operations.
    /// Built once per <c>MementoHelper&lt;TId, TSelf, TMemento&gt;</c> instantiation.
    /// </summary>
    private sealed class CrossCuttingHandlers
    {
        // Accountability: entity -> (boxedCreatedBy, boxedModifiedBy)
        internal Func<TSelf, (object?, object?)>? AccountabilityReader;

        // Accountability: (entity, boxedCreatedBy, boxedModifiedBy) -> void
        internal Action<TSelf, object?, object?>? AccountabilityWriter;

        // Timestamps: (entity, createdAtUtc, modifiedAtUtc) -> void
        internal Action<TSelf, DateTimeOffset?, DateTimeOffset?>? TimestampWriter;

        // SoftDelete: entity -> (deletedAtUtc, boxedDeletedBy)
        internal Func<TSelf, (DateTimeOffset?, object?)>? SoftDeleteReader;

        // SoftDelete: (entity, deletedAtUtc, boxedDeletedBy) -> void
        internal Action<TSelf, DateTimeOffset?, object?>? SoftDeleteWriter;

        // Tenant: entity -> boxedTenantId
        internal Func<TSelf, object?>? TenantReader;

        // Tenant: (entity, boxedTenantId) -> void
        internal Action<TSelf, object?>? TenantWriter;

        internal static CrossCuttingHandlers Build()
        {
            var handlers = new CrossCuttingHandlers();
            var entityType = typeof(TSelf);

            // Accountability
            if (typeof(IAccountable).IsAssignableFrom(entityType))
            {
                var genericInterface = FindGenericInterface(entityType, typeof(IAccountable<>));
                if (genericInterface is not null)
                {
                    var actorIdType = genericInterface.GetGenericArguments()[0];
                    handlers.AccountabilityReader = BuildAccountabilityReader(entityType, genericInterface, actorIdType);
                    handlers.AccountabilityWriter = BuildAccountabilityWriter(entityType, actorIdType);
                }
            }

            // Timestamps
            if (typeof(ITimestamped).IsAssignableFrom(entityType))
            {
                handlers.TimestampWriter = BuildTimestampWriter(entityType);
            }

            // SoftDelete
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType))
            {
                var genericInterface = FindGenericInterface(entityType, typeof(ISoftDeletable<>));
                if (genericInterface is not null)
                {
                    var actorIdType = genericInterface.GetGenericArguments()[0];
                    handlers.SoftDeleteReader = BuildSoftDeleteReader(entityType, genericInterface, actorIdType);
                    handlers.SoftDeleteWriter = BuildSoftDeleteWriter(entityType, actorIdType);
                }
            }

            // Tenant
            if (typeof(ITenantScoped).IsAssignableFrom(entityType))
            {
                var genericInterface = FindGenericInterface(entityType, typeof(ITenantScoped<>));
                if (genericInterface is not null)
                {
                    var tenantIdType = genericInterface.GetGenericArguments()[0];
                    handlers.TenantReader = BuildTenantReader(entityType, genericInterface, tenantIdType);
                    handlers.TenantWriter = BuildTenantWriter(entityType, tenantIdType);
                }
            }

            return handlers;
        }

        private static Type? FindGenericInterface(Type entityType, Type openGenericInterface)
        {
            return Array.Find(
                entityType.GetInterfaces(),
                i => i.IsGenericType && i.GetGenericTypeDefinition() == openGenericInterface);
        }

        // --- Accountability ---

        private static Func<TSelf, (object?, object?)> BuildAccountabilityReader(
            Type entityType, Type genericInterface, Type actorIdType)
        {
            // Read CreatedBy and ModifiedBy, extract BoxedValue from typed IDs
            var createdByProp = entityType.GetProperty("CreatedBy")
                ?? throw MissingPropertyError(entityType, "CreatedBy");
            var modifiedByProp = entityType.GetProperty("ModifiedBy")
                ?? throw MissingPropertyError(entityType, "ModifiedBy");

            var entityParam = Expression.Parameter(typeof(TSelf), "entity");

            var createdByExpr = BuildBoxedValueExtractor(entityParam, createdByProp, actorIdType);
            var modifiedByExpr = BuildBoxedValueExtractor(entityParam, modifiedByProp, actorIdType);

            var tupleConstructor = typeof(ValueTuple<object?, object?>).GetConstructor([typeof(object), typeof(object)])!;
            var body = Expression.New(tupleConstructor, createdByExpr, modifiedByExpr);

            return Expression.Lambda<Func<TSelf, (object?, object?)>>(body, entityParam).Compile();
        }

        private static Action<TSelf, object?, object?> BuildAccountabilityWriter(
            Type entityType, Type actorIdType)
        {
            var createdBySetter = FindPropertySetter(entityType, "CreatedBy");
            var modifiedBySetter = FindPropertySetter(entityType, "ModifiedBy");
            var factory = TypedIdFactoryCache.GetOrBuild(actorIdType);

            return (entity, boxedCreatedBy, boxedModifiedBy) =>
            {
                createdBySetter(entity, boxedCreatedBy is null ? null : factory(boxedCreatedBy));
                modifiedBySetter(entity, boxedModifiedBy is null ? null : factory(boxedModifiedBy));
            };
        }

        // --- Timestamps ---

        private static Action<TSelf, DateTimeOffset?, DateTimeOffset?> BuildTimestampWriter(Type entityType)
        {
            var createdAtSetter = FindPropertySetter(entityType, "CreatedAtUtc");
            var modifiedAtSetter = FindPropertySetter(entityType, "ModifiedAtUtc");

            return (entity, createdAtUtc, modifiedAtUtc) =>
            {
                createdAtSetter(entity, createdAtUtc);
                modifiedAtSetter(entity, modifiedAtUtc);
            };
        }

        // --- SoftDelete ---

        private static Func<TSelf, (DateTimeOffset?, object?)> BuildSoftDeleteReader(
            Type entityType, Type genericInterface, Type actorIdType)
        {
            var deletedAtProp = entityType.GetProperty("DeletedAtUtc")
                ?? throw MissingPropertyError(entityType, "DeletedAtUtc");
            var deletedByProp = entityType.GetProperty("DeletedBy")
                ?? throw MissingPropertyError(entityType, "DeletedBy");

            var entityParam = Expression.Parameter(typeof(TSelf), "entity");

            var deletedAtExpr = Expression.Convert(
                Expression.Property(entityParam, deletedAtProp),
                typeof(DateTimeOffset?));
            var deletedByExpr = BuildBoxedValueExtractor(entityParam, deletedByProp, actorIdType);

            var tupleConstructor = typeof(ValueTuple<DateTimeOffset?, object?>)
                .GetConstructor([typeof(DateTimeOffset?), typeof(object)])!;
            var body = Expression.New(tupleConstructor, deletedAtExpr, deletedByExpr);

            return Expression.Lambda<Func<TSelf, (DateTimeOffset?, object?)>>(body, entityParam).Compile();
        }

        private static Action<TSelf, DateTimeOffset?, object?> BuildSoftDeleteWriter(
            Type entityType, Type actorIdType)
        {
            var deletedAtSetter = FindPropertySetter(entityType, "DeletedAtUtc");
            var deletedBySetter = FindPropertySetter(entityType, "DeletedBy");
            var factory = TypedIdFactoryCache.GetOrBuild(actorIdType);

            return (entity, deletedAtUtc, boxedDeletedBy) =>
            {
                deletedAtSetter(entity, deletedAtUtc);
                deletedBySetter(entity, boxedDeletedBy is null ? null : factory(boxedDeletedBy));
            };
        }

        // --- Tenant ---

        private static Func<TSelf, object?> BuildTenantReader(
            Type entityType, Type genericInterface, Type tenantIdType)
        {
            var tenantIdProp = entityType.GetProperty("TenantId")
                ?? throw MissingPropertyError(entityType, "TenantId");

            var entityParam = Expression.Parameter(typeof(TSelf), "entity");
            var propAccess = Expression.Property(entityParam, tenantIdProp);

            // TenantId is non-nullable on the interface, but could be null before construction completes
            Expression body;
            if (!tenantIdProp.PropertyType.IsValueType)
            {
                // Reference type: check null, then call BoxedValue
                var boxedValueProp = typeof(ITypedId).GetProperty(nameof(ITypedId.BoxedValue))!;
                var asTypedId = Expression.Convert(propAccess, typeof(ITypedId));
                var boxedValue = Expression.Property(asTypedId, boxedValueProp);
                var nullCheck = Expression.Condition(
                    Expression.Equal(propAccess, Expression.Constant(null, tenantIdProp.PropertyType)),
                    Expression.Constant(null, typeof(object)),
                    Expression.Convert(boxedValue, typeof(object)));
                body = nullCheck;
            }
            else
            {
                body = Expression.Convert(propAccess, typeof(object));
            }

            return Expression.Lambda<Func<TSelf, object?>>(body, entityParam).Compile();
        }

        private static Action<TSelf, object?> BuildTenantWriter(Type entityType, Type tenantIdType)
        {
            var tenantIdSetter = FindPropertySetter(entityType, "TenantId");
            var factory = TypedIdFactoryCache.GetOrBuild(tenantIdType);

            return (entity, boxedTenantId) =>
            {
                tenantIdSetter(entity, boxedTenantId is null ? null : factory(boxedTenantId));
            };
        }

        // --- Shared Helpers ---

        /// <summary>
        /// Builds an expression that reads a typed ID property and extracts its BoxedValue,
        /// returning null if the property value is null.
        /// </summary>
        private static Expression BuildBoxedValueExtractor(
            ParameterExpression entityParam, PropertyInfo property, Type typedIdType)
        {
            var propAccess = Expression.Property(entityParam, property);
            var boxedValueProp = typeof(ITypedId).GetProperty(nameof(ITypedId.BoxedValue))!;

            if (Nullable.GetUnderlyingType(property.PropertyType) is not null || !property.PropertyType.IsValueType)
            {
                // Nullable reference or value type: check null, then BoxedValue
                var asTypedId = Expression.Convert(propAccess, typeof(ITypedId));
                var boxedValue = Expression.Property(asTypedId, boxedValueProp);
                return Expression.Condition(
                    Expression.Equal(propAccess, Expression.Constant(null, property.PropertyType)),
                    Expression.Constant(null, typeof(object)),
                    Expression.Convert(boxedValue, typeof(object)));
            }

            // Non-nullable: direct BoxedValue
            var directCast = Expression.Convert(propAccess, typeof(ITypedId));
            return Expression.Convert(Expression.Property(directCast, boxedValueProp), typeof(object));
        }

        /// <summary>
        /// Finds a property setter on the entity type, compiling it as <c>Action&lt;TSelf, object?&gt;</c>.
        /// Throws an actionable <see cref="InvalidOperationException"/> if the property has no setter.
        /// </summary>
        private static Action<TSelf, object?> FindPropertySetter(Type entityType, string propertyName)
        {
            var prop = entityType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
                ?? throw MissingPropertyError(entityType, propertyName);

            var setter = prop.GetSetMethod(nonPublic: true)
                ?? throw new InvalidOperationException(
                    $"{entityType.Name}.{propertyName} must have a setter (e.g., {{ get; private set; }}) " +
                    $"for automatic memento handling. Add a private setter or handle {propertyName} " +
                    $"manually in SnapshotCore/RestoreCore/HydrateCore.");

            var entityParam = Expression.Parameter(typeof(TSelf), "entity");
            var valueParam = Expression.Parameter(typeof(object), "value");

            var convertedValue = prop.PropertyType.IsValueType
                ? Expression.Convert(Expression.Coalesce(valueParam, Expression.Default(prop.PropertyType)), prop.PropertyType)
                : Expression.Convert(valueParam, prop.PropertyType);

            var call = Expression.Call(entityParam, setter, convertedValue);
            return Expression.Lambda<Action<TSelf, object?>>(call, entityParam, valueParam).Compile();
        }

        private static InvalidOperationException MissingPropertyError(Type entityType, string propertyName) =>
            new($"{entityType.Name} implements a cross-cutting interface but is missing " +
                $"the '{propertyName}' property. Use implicit interface implementation " +
                $"(e.g., public ... {propertyName} {{ get; private set; }}).");
    }

    /// <summary>
    /// Shared cache of compiled typed ID factories, keyed by the concrete typed ID type.
    /// Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/>.
    /// </summary>
    private static class TypedIdFactoryCache
    {
        private static readonly ConcurrentDictionary<Type, Func<object, object>> _cache = new();

        internal static Func<object, object> GetOrBuild(Type typedIdType)
        {
            return _cache.GetOrAdd(typedIdType, static type =>
            {
                // Find the backing type from the ITypedId<T> interface
                var typedIdGeneric = Array.Find(
                    type.GetInterfaces(),
                    i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITypedId<>));

                var backingType = typedIdGeneric?.GetGenericArguments()[0]
                    ?? throw new InvalidOperationException(
                        $"{type.Name} implements ITypedId but does not implement ITypedId<T>.");

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
}
