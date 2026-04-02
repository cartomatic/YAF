namespace Yaf.Domain.Interfaces;

/// <summary>
/// Represents a domain error with a code and human-readable message.
/// </summary>
internal interface IError
{
    /// <summary>
    /// Machine-readable error code.
    /// </summary>
    string Code { get; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    string Message { get; }
}
