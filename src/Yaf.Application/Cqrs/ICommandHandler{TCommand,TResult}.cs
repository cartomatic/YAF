using Yaf.Domain;

namespace Yaf.Application.Cqrs;

/// <summary>
/// Handles an <see cref="ICommand{TResult}"/> and returns a <see cref="Result{T}"/>
/// wrapping the produced value or one or more failure errors.
/// </summary>
/// <remarks>
/// <para>
/// This handler is invoked by the application's mediator/dispatcher pipeline. The
/// pipeline orders sanitization and validation before the handler runs; the handler
/// itself contains business logic and produces the outcome.
/// </para>
/// <para>
/// <typeparamref name="TCommand"/> is contravariant (<c>in</c>) so that a handler for a
/// base command type can be substituted where a handler for a derived command is required.
/// <typeparamref name="TResult"/> is invariant — covariance was attempted and rejected by
/// the compiler (CS1961) because <typeparamref name="TResult"/> appears within
/// <see cref="Task{TResult}"/> wrapping <see cref="Result{T}"/>, both of which are
/// invariant in their type parameters.
/// </para>
/// </remarks>
/// <typeparam name="TCommand">
/// The command type handled by this implementation. Must implement
/// <see cref="ICommand{TResult}"/> with a matching result type.
/// </typeparam>
/// <typeparam name="TResult">
/// The type of the value produced by the command on success. Constrained to
/// <c>notnull</c> to match <see cref="Result{T}"/>'s constraint.
/// </typeparam>
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
    where TResult : notnull
{
    /// <summary>
    /// Handles the supplied <paramref name="command"/> and produces a typed result.
    /// </summary>
    /// <param name="command">The command to process. Must not be null.</param>
    /// <param name="cancellationToken">A token observed for cooperative cancellation.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> producing a <see cref="Result{T}"/> that, on success,
    /// carries the produced <typeparamref name="TResult"/> value, or otherwise carries
    /// one or more <see cref="Error"/> values describing why the command failed.
    /// </returns>
    Task<Result<TResult>> Handle(TCommand command, CancellationToken cancellationToken);
}
