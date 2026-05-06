namespace Yaf.Application.Cqrs;

/// <summary>
/// Marker for a read-only query that produces a typed result without mutating state.
/// </summary>
/// <remarks>
/// <para>
/// Queries are dispatched to an <see cref="IQueryHandler{TQuery,TResult}"/> and always
/// return a typed value via <see cref="Yaf.Domain.Result{T}"/>. They are dual to
/// <see cref="ICommand{TResult}"/>: commands change state and may also return a value;
/// queries observe state and never change it.
/// </para>
/// <para>
/// <typeparamref name="TResult"/> is declared <c>out</c> (covariant). Because the marker
/// has no members, <typeparamref name="TResult"/> appears in no constraining position;
/// covariance permits an <c>IQuery&lt;Derived&gt;</c> reference to be assigned to an
/// <c>IQuery&lt;Base&gt;</c> variable. This mirrors the
/// <see cref="ICommand{TResult}"/> and
/// <see cref="Yaf.Domain.Interfaces.IDomainEvent{T}"/> conventions.
/// </para>
/// </remarks>
/// <typeparam name="TResult">
/// The type of the value produced by the query on success. Constrained to
/// <c>notnull</c> to match <see cref="Yaf.Domain.Result{T}"/>'s constraint, since
/// the matching <see cref="IQueryHandler{TQuery,TResult}"/> wraps the value in
/// <see cref="Yaf.Domain.Result{T}"/>.
/// </typeparam>
public interface IQuery<out TResult>
    where TResult : notnull;
