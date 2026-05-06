namespace Yaf.Application.Cqrs;

/// <summary>
/// Marker for a command that mutates state and produces a typed result value on success.
/// </summary>
/// <remarks>
/// <para>
/// Inherits <see cref="ICommand"/> so that pipeline behaviors can target every command
/// uniformly regardless of result type. A handler implementing
/// <see cref="ICommandHandler{TCommand,TResult}"/> processes the command and returns a
/// <see cref="Yaf.Domain.Result{T}"/> wrapping <typeparamref name="TResult"/>.
/// </para>
/// <para>
/// <typeparamref name="TResult"/> is declared <c>out</c> (covariant). Because the marker
/// itself has no members, <typeparamref name="TResult"/> appears in no constraining
/// position; covariance permits an <c>ICommand&lt;Derived&gt;</c> reference to be assigned
/// to an <c>ICommand&lt;Base&gt;</c> variable. This mirrors the
/// <see cref="Yaf.Domain.Interfaces.IDomainEvent{T}"/> convention.
/// </para>
/// </remarks>
/// <typeparam name="TResult">
/// The type of the value produced by the command on success. Constrained to
/// <c>notnull</c> to match <see cref="Yaf.Domain.Result{T}"/>'s constraint, since
/// the matching <see cref="ICommandHandler{TCommand,TResult}"/> wraps the value in
/// <see cref="Yaf.Domain.Result{T}"/>.
/// </typeparam>
public interface ICommand<out TResult> : ICommand
    where TResult : notnull;
