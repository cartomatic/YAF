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
    private static readonly Func<object, TId>? IdFactory = BuildIdFactory();

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
        if (memento is IHasIdentity hasIdentity
            && hasIdentity.IdentityType == TId.IdentityType
            && IdFactory is not null)
        {
            return (IdFactory(hasIdentity.BoxedId), true);
        }

        return (default, false);
    }

    /// <summary>
    /// Creates a new <typeparamref name="TSelf"/> instance via <see cref="RuntimeHelpers.GetUninitializedObject"/>.
    /// </summary>
    internal static TSelf CreateUninitializedInstance() =>
        (TSelf)RuntimeHelpers.GetUninitializedObject(typeof(TSelf));

    /// <summary>
    /// Throws <see cref="ValidationException"/> if the error collection is non-empty.
    /// </summary>
    internal static void ThrowIfInvalid(IReadOnlyCollection<IError> errors)
    {
        if (errors.Count > 0)
        {
            throw new ValidationException(typeof(TSelf), errors);
        }
    }

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
}
