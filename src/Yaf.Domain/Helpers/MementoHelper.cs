using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain.Helpers;

/// <summary>
/// Shared memento building blocks used by both <see cref="Entity{TId,TSelf,TMemento}"/>
/// and <see cref="AggregateRoot{TId,TSelf,TMemento}"/> for identity bridging and
/// conditional delegate construction.
/// </summary>
internal static class MementoHelper<TId, TSelf, TMemento>
    where TId : ITypedId
    where TSelf : Entity<TId>
    where TMemento : class
{
    private static readonly Func<Guid, TId>? _idFactory = BuildIdFactory();

    // --- Identity ---

    /// <summary>
    /// Writes the entity's identity to the memento if the memento implements <see cref="IHasIdentity"/>.
    /// </summary>
    internal static void WriteIdentity(TMemento memento, TId id)
    {
        if (memento is IHasIdentity hasIdentity)
        {
            hasIdentity.Id = id.Value;
        }
    }

    /// <summary>
    /// Reads the identity from the memento and constructs a <typeparamref name="TId"/>.
    /// </summary>
    internal static (TId? id, bool success) ReadIdentity(TMemento memento)
    {
        if (memento is IHasIdentity hasIdentity)
        {
            if (_idFactory is null)
            {
                throw new InvalidOperationException(
                    $"{typeof(TId).Name} must have a public constructor accepting a single " +
                    $"Guid parameter for automatic identity restoration. " +
                    $"Use positional record syntax: record {typeof(TId).Name}(Guid Value)");
            }

            if (hasIdentity.Id is null)
            {
                return (default, true);
            }

            return (_idFactory(hasIdentity.Id.Value), true);
        }

        return (default, false);
    }

    /// <summary>
    /// Creates a new <typeparamref name="TSelf"/> instance via <see cref="RuntimeHelpers.GetUninitializedObject"/>.
    /// </summary>
    internal static TSelf CreateUninitializedInstance() =>
        (TSelf)RuntimeHelpers.GetUninitializedObject(typeof(TSelf));

    // --- Conditional delegate builders ---
    // These check whether TSelf/TMemento implement the required interfaces before
    // compiling delegates. Returns null when the concern does not apply, avoiding
    // unnecessary reflection.

    /// <summary>
    /// Builds a compiled property writer if <typeparamref name="TSelf"/> implements
    /// <typeparamref name="TDomain"/> and <typeparamref name="TMemento"/> implements <typeparamref name="TMem"/>.
    /// </summary>
    internal static Action<TSelf, object?>? BuildWriter<TDomain, TMem>(string propertyName) =>
        typeof(TDomain).IsAssignableFrom(typeof(TSelf)) && typeof(TMem).IsAssignableFrom(typeof(TMemento))
            ? ReflectionHelper.BuildPropertyWriter<TSelf>(propertyName)
            : null;

    /// <summary>
    /// Builds a compiled property writer if <typeparamref name="TSelf"/> implements
    /// the open generic domain interface and <typeparamref name="TMemento"/> implements the memento interface.
    /// </summary>
    internal static Action<TSelf, object?>? BuildWriter(Type openGenericDomain, Type mementoInterface, string propertyName) =>
        ReflectionHelper.FindGenericInterface(typeof(TSelf), openGenericDomain) is not null
            && mementoInterface.IsAssignableFrom(typeof(TMemento))
            ? ReflectionHelper.BuildPropertyWriter<TSelf>(propertyName)
            : null;

    /// <summary>
    /// Builds a compiled property reader if <typeparamref name="TSelf"/> implements
    /// the open generic domain interface and <typeparamref name="TMemento"/> implements the memento interface.
    /// </summary>
    internal static Func<TSelf, object?>? BuildReader(Type openGenericDomain, Type mementoInterface, string propertyName) =>
        ReflectionHelper.FindGenericInterface(typeof(TSelf), openGenericDomain) is not null
            && mementoInterface.IsAssignableFrom(typeof(TMemento))
            ? ReflectionHelper.BuildPropertyReader<TSelf>(propertyName)
            : null;

    /// <summary>
    /// Builds a <see cref="TypedIdBridge{TSelf}"/> for a single typed ID property if
    /// <typeparamref name="TSelf"/> implements the open generic domain interface and
    /// <typeparamref name="TMemento"/> implements the memento interface.
    /// </summary>
    internal static TypedIdBridge<TSelf>? BuildBridge(Type openGenericDomain, Type mementoInterface, string propertyName) =>
        TypedIdBridge<TSelf>.TryBuild<TMemento>(openGenericDomain, mementoInterface, propertyName);

    private static Func<Guid, TId>? BuildIdFactory()
    {
        var constructor = typeof(TId).GetConstructor([typeof(Guid)]);

        if (constructor is null)
        {
            return null;
        }

        var param = Expression.Parameter(typeof(Guid), "value");
        var body = Expression.New(constructor, param);
        return Expression.Lambda<Func<Guid, TId>>(body, param).Compile();
    }
}
