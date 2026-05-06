namespace Yaf.Application.Cqrs;

/// <summary>
/// Marker for a command that mutates state and produces no return value beyond
/// success or failure.
/// </summary>
/// <remarks>
/// <para>
/// Commands express the intent to change application state. A handler implementing
/// <see cref="ICommandHandler{TCommand}"/> processes the command and returns a
/// <see cref="Yaf.Domain.Result"/> indicating success or one or more errors.
/// </para>
/// <para>
/// Use <see cref="ICommand{TResult}"/> when the command must return a typed value
/// (for example, the identifier of a newly created aggregate). The non-generic
/// <see cref="ICommand"/> is the common base — pipeline behaviors that target
/// every command can constrain on it regardless of the result type.
/// </para>
/// <para>
/// This non-generic marker is a YAF-specific addition over the original CQRS
/// abstraction proposal, retained so that void commands can flow through the same
/// pipeline as typed commands without an artificial unit return.
/// </para>
/// </remarks>
public interface ICommand;
