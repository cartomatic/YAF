using Yaf.Domain.Interfaces;

namespace Yaf.Domain;

/// <summary>
/// Immutable error with a machine-readable code and human-readable message.
/// </summary>
public sealed record Error : IError
{
    /// <inheritdoc />
    public string Code { get; }

    /// <inheritdoc />
    public string Message { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Error"/> record.
    /// </summary>
    /// <param name="code">Machine-readable error code. Must not be null or whitespace.</param>
    /// <param name="message">Human-readable error message. Must not be null or whitespace.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="code"/> or <paramref name="message"/> is null, empty, or whitespace.
    /// </exception>
    public Error(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Code = code;
        Message = message;
    }
}
