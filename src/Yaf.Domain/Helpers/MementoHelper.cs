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

            if (typeof(IAccountable).IsAssignableFrom(entityType)
                && ReflectionHelper.FindGenericInterface(entityType, typeof(IAccountable<>)) is not null)
            {
                var readCreatedBy = ReflectionHelper.BuildPropertyReader<TSelf>(nameof(IHasAccountability<Guid>.CreatedBy));
                var readModifiedBy = ReflectionHelper.BuildPropertyReader<TSelf>(nameof(IHasAccountability<Guid>.ModifiedBy));
                handlers.AccountabilityReader = entity => (readCreatedBy(entity), readModifiedBy(entity));

                var writeCreatedBy = ReflectionHelper.BuildPropertyWriter<TSelf>(nameof(IHasAccountability<Guid>.CreatedBy));
                var writeModifiedBy = ReflectionHelper.BuildPropertyWriter<TSelf>(nameof(IHasAccountability<Guid>.ModifiedBy));
                handlers.AccountabilityWriter = (entity, boxedCreatedBy, boxedModifiedBy) =>
                {
                    writeCreatedBy(entity, boxedCreatedBy);
                    writeModifiedBy(entity, boxedModifiedBy);
                };
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
                && ReflectionHelper.FindGenericInterface(entityType, typeof(ISoftDeletable<>)) is not null)
            {
                var readDeletedAt = ReflectionHelper.BuildPropertyReader<TSelf>(nameof(IHasSoftDelete<Guid>.DeletedAtUtc));
                var readDeletedBy = ReflectionHelper.BuildPropertyReader<TSelf>(nameof(IHasSoftDelete<Guid>.DeletedBy));
                handlers.SoftDeleteReader = entity => ((DateTimeOffset?)readDeletedAt(entity), readDeletedBy(entity));

                var writeDeletedAt = ReflectionHelper.BuildPropertyWriter<TSelf>(nameof(IHasSoftDelete<Guid>.DeletedAtUtc));
                var writeDeletedBy = ReflectionHelper.BuildPropertyWriter<TSelf>(nameof(IHasSoftDelete<Guid>.DeletedBy));
                handlers.SoftDeleteWriter = (entity, deletedAtUtc, boxedDeletedBy) =>
                {
                    writeDeletedAt(entity, deletedAtUtc);
                    writeDeletedBy(entity, boxedDeletedBy);
                };
            }

            if (typeof(ITenantScoped).IsAssignableFrom(entityType)
                && ReflectionHelper.FindGenericInterface(entityType, typeof(ITenantScoped<>)) is not null)
            {
                handlers.TenantReader = ReflectionHelper.BuildPropertyReader<TSelf>(nameof(IHasTenantId<Guid>.TenantId));
                handlers.TenantWriter = ReflectionHelper.BuildPropertyWriter<TSelf>(nameof(IHasTenantId<Guid>.TenantId));
            }

            return handlers;
        }
    }
}
