using Yaf.Domain;

namespace Yaf.Application.Cqrs;

/// <summary>
/// Handles an <see cref="IQuery{TResult}"/> and returns a <see cref="Result{T}"/>
/// carrying the produced value or one or more failure errors.
/// </summary>
/// <remarks>
/// <para>
/// Queries observe state and never modify it. The handler is invoked by the
/// application's mediator/dispatcher pipeline; cross-cutting behaviors such as
/// authorization, caching, and logging compose around it.
/// </para>
/// <para>
/// <typeparamref name="TQuery"/> is contravariant (<c>in</c>) so that a handler written
/// against a base query type can be substituted where a handler for a derived query is
/// required. <typeparamref name="TResult"/> is invariant because <see cref="Result{T}"/>
/// is invariant in its type parameter.
/// </para>
/// </remarks>
/// <typeparam name="TQuery">
/// The query type handled by this implementation. Must implement
/// <see cref="IQuery{TResult}"/> with a matching result type.
/// </typeparam>
/// <typeparam name="TResult">
/// The type of the value produced by the query on success. Constrained to
/// <c>notnull</c> to match <see cref="Result{T}"/>'s constraint.
/// </typeparam>
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
    where TResult : notnull
{
    /// <summary>
    /// Handles the supplied <paramref name="query"/> and produces a typed result.
    /// </summary>
    /// <param name="query">The query to process. Must not be null.</param>
    /// <param name="cancellationToken">A token observed for cooperative cancellation.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> producing a <see cref="Result{T}"/> that, on success,
    /// carries the produced <typeparamref name="TResult"/> value, or otherwise carries
    /// one or more <see cref="Error"/> values describing why the query failed.
    /// </returns>
    Task<Result<TResult>> Handle(TQuery query, CancellationToken cancellationToken);
}
