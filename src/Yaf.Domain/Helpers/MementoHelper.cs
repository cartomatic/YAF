using System.Collections.Concurrent;
using System.Linq.Expressions;
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
    private static MementoBridge? _handlers;

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

            if (hasIdentity.BoxedId is null)
            {
                return (default, true);
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
        hasAccountability.BoxedCreatedBy = ExtractPrimitive(createdBy);
        hasAccountability.BoxedModifiedBy = ExtractPrimitive(modifiedBy);
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

        var createdBy = ReconstructTypedId(hasAccountability.BoxedCreatedBy, handlers.ActorIdFactory);
        var modifiedBy = ReconstructTypedId(hasAccountability.BoxedModifiedBy, handlers.ActorIdFactory);
        handlers.AccountabilityWriter(entity, createdBy, modifiedBy);
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
        hasSoftDelete.BoxedDeletedBy = ExtractPrimitive(deletedBy);
    }

    /// <summary>
    /// Reads soft-delete fields from memento and sets them on the entity.
    /// </summary>
    internal static void ReadSoftDelete(TMemento memento, TSelf entity)
    {
        if (entity is not ISoftDeletable || memento is not IHasSoftDelete hasSoftDelete)
            return;

        var handlers = EnsureHandlers();
        if (handlers.SoftDeleteWriter is null)
            return;

        var deletedBy = ReconstructTypedId(hasSoftDelete.BoxedDeletedBy, handlers.DeleteActorIdFactory);
        handlers.SoftDeleteWriter(entity, hasSoftDelete.DeletedAtUtc, deletedBy);
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

        var tenantId = handlers.TenantReader(entity);
        hasTenantId.BoxedTenantId = ExtractPrimitive(tenantId);
    }

    /// <summary>
    /// Reads tenant ID from memento and sets it on the entity.
    /// </summary>
    internal static void ReadTenantId(TMemento memento, TSelf entity)
    {
        if (entity is not ITenantScoped || memento is not IHasTenantId hasTenantId)
            return;

        var handlers = EnsureHandlers();
        if (handlers.TenantWriter is null)
            return;

        var tenantId = ReconstructTypedId(hasTenantId.BoxedTenantId, handlers.TenantIdFactory);
        handlers.TenantWriter(entity, tenantId);
    }

    /// <summary>
    /// Creates a new <typeparamref name="TSelf"/> instance via <see cref="RuntimeHelpers.GetUninitializedObject"/>.
    /// </summary>
    internal static TSelf CreateUninitializedInstance() =>
        (TSelf)RuntimeHelpers.GetUninitializedObject(typeof(TSelf));

    private static MementoBridge EnsureHandlers() =>
        _handlers ??= MementoBridge.Build();

    /// <summary>
    /// Extracts the primitive backing value from a typed ID, or returns the value as-is
    /// if it is not a typed ID.
    /// </summary>
    private static object? ExtractPrimitive(object? value) =>
        value is ITypedId typedId ? typedId.BoxedValue : value;

    /// <summary>
    /// Reconstructs a typed ID from a boxed primitive value using the given factory.
    /// Returns <see langword="null"/> if the input is null or the factory is null.
    /// </summary>
    private static object? ReconstructTypedId(object? boxedPrimitive, Func<object, object>? factory) =>
        boxedPrimitive is null || factory is null ? null : factory(boxedPrimitive);

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
    private sealed class MementoBridge
    {
        internal Func<TSelf, (object?, object?)>? AccountabilityReader;
        internal Action<TSelf, object?, object?>? AccountabilityWriter;
        internal Func<object, object>? ActorIdFactory;

        internal Action<TSelf, DateTimeOffset?, DateTimeOffset?>? TimestampWriter;

        internal Func<TSelf, (DateTimeOffset?, object?)>? SoftDeleteReader;
        internal Action<TSelf, DateTimeOffset?, object?>? SoftDeleteWriter;
        internal Func<object, object>? DeleteActorIdFactory;

        internal Func<TSelf, object?>? TenantReader;
        internal Action<TSelf, object?>? TenantWriter;
        internal Func<object, object>? TenantIdFactory;

        internal static MementoBridge Build()
        {
            var handlers = new MementoBridge();
            var entityType = typeof(TSelf);

            if (typeof(IAccountable).IsAssignableFrom(entityType)
                && ReflectionHelper.FindGenericInterface(entityType, typeof(IAccountable<>)) is { } accountableInterface)
            {
                var readCreatedBy = ReflectionHelper.BuildPropertyReader<TSelf>(nameof(IHasAccountability<Guid>.CreatedBy));
                var readModifiedBy = ReflectionHelper.BuildPropertyReader<TSelf>(nameof(IHasAccountability<Guid>.ModifiedBy));
                handlers.AccountabilityReader = entity => (readCreatedBy(entity), readModifiedBy(entity));

                var writeCreatedBy = ReflectionHelper.BuildPropertyWriter<TSelf>(nameof(IHasAccountability<Guid>.CreatedBy));
                var writeModifiedBy = ReflectionHelper.BuildPropertyWriter<TSelf>(nameof(IHasAccountability<Guid>.ModifiedBy));
                handlers.AccountabilityWriter = (entity, createdBy, modifiedBy) =>
                {
                    writeCreatedBy(entity, createdBy);
                    writeModifiedBy(entity, modifiedBy);
                };

                handlers.ActorIdFactory = TypedIdFactoryCache.GetOrBuild(accountableInterface.GetGenericArguments()[0]);
            }

            if (typeof(ITimestamped).IsAssignableFrom(entityType))
            {
                var writeCreatedAt = ReflectionHelper.BuildPropertyWriter<TSelf>(nameof(ITimestamped.CreatedAtUtc));
                var writeModifiedAt = ReflectionHelper.BuildPropertyWriter<TSelf>(nameof(ITimestamped.ModifiedAtUtc));
                handlers.TimestampWriter = (entity, createdAtUtc, modifiedAtUtc) =>
                {
                    writeCreatedAt(entity, createdAtUtc);
                    writeModifiedAt(entity, modifiedAtUtc);
                };
            }

            if (typeof(ISoftDeletable).IsAssignableFrom(entityType)
                && ReflectionHelper.FindGenericInterface(entityType, typeof(ISoftDeletable<>)) is { } softDeletableInterface)
            {
                var readDeletedAt = ReflectionHelper.BuildPropertyReader<TSelf>(nameof(IHasSoftDelete<Guid>.DeletedAtUtc));
                var readDeletedBy = ReflectionHelper.BuildPropertyReader<TSelf>(nameof(IHasSoftDelete<Guid>.DeletedBy));
                handlers.SoftDeleteReader = entity => ((DateTimeOffset?)readDeletedAt(entity), readDeletedBy(entity));

                var writeDeletedAt = ReflectionHelper.BuildPropertyWriter<TSelf>(nameof(IHasSoftDelete<Guid>.DeletedAtUtc));
                var writeDeletedBy = ReflectionHelper.BuildPropertyWriter<TSelf>(nameof(IHasSoftDelete<Guid>.DeletedBy));
                handlers.SoftDeleteWriter = (entity, deletedAtUtc, deletedBy) =>
                {
                    writeDeletedAt(entity, deletedAtUtc);
                    writeDeletedBy(entity, deletedBy);
                };

                handlers.DeleteActorIdFactory = TypedIdFactoryCache.GetOrBuild(softDeletableInterface.GetGenericArguments()[0]);
            }

            if (typeof(ITenantScoped).IsAssignableFrom(entityType)
                && ReflectionHelper.FindGenericInterface(entityType, typeof(ITenantScoped<>)) is { } tenantScopedInterface)
            {
                handlers.TenantReader = ReflectionHelper.BuildPropertyReader<TSelf>(nameof(IHasTenantId<Guid>.TenantId));
                handlers.TenantWriter = ReflectionHelper.BuildPropertyWriter<TSelf>(nameof(IHasTenantId<Guid>.TenantId));
                handlers.TenantIdFactory = TypedIdFactoryCache.GetOrBuild(tenantScopedInterface.GetGenericArguments()[0]);
            }

            return handlers;
        }
    }

    /// <summary>
    /// Shared cache of compiled typed ID constructor delegates.
    /// Keyed by the concrete typed ID type. Thread-safe.
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
