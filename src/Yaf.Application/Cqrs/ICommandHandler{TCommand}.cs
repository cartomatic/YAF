using Yaf.Domain;

namespace Yaf.Application.Cqrs;

/// <summary>
/// Handles a void <see cref="ICommand"/> and returns a <see cref="Result"/> indicating
/// success or failure.
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
/// </para>
/// </remarks>
/// <typeparam name="TCommand">The command type handled by this implementation.</typeparam>
public interface ICommandHandler<in TCommand> where TCommand : ICommand
{
    /// <summary>
    /// Handles the supplied <paramref name="command"/>.
    /// </summary>
    /// <param name="command">The command to process. Must not be null.</param>
    /// <param name="cancellationToken">A token observed for cooperative cancellation.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> producing a <see cref="Result"/> that represents
    /// either success or one or more <see cref="Error"/> values describing why the
    /// command failed.
    /// </returns>
    Task<Result> Handle(TCommand command, CancellationToken cancellationToken);
}
