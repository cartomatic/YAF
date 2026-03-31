using System.Runtime.CompilerServices;
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

    /// <summary>
    /// Creates an error with an automatically generated code composed of
    /// the full name of <typeparamref name="T"/> and the declaring member name.
    /// </summary>
    /// <typeparam name="T">
    /// The type declaring this error. Used to derive the namespace and class portion of the code.
    /// </typeparam>
    /// <param name="message">Human-readable error message. Must not be null or whitespace.</param>
    /// <param name="memberName">
    /// Automatically populated by <see cref="CallerMemberNameAttribute"/>.
    /// Do not pass explicitly.
    /// </param>
    /// <returns>An <see cref="IError"/> with code in the format <c>Namespace.Type.MemberName</c>.</returns>
    /// <example>
    /// <code>
    /// // Declared in MyApp.Domain.Orders.Order:
    /// public static readonly IError EmptyCart = Error.Create&lt;Order&gt;("Cannot create an order with an empty cart.");
    /// // Produces code: "MyApp.Domain.Orders.Order.EmptyCart"
    /// </code>
    /// </example>
    public static IError Create<T>(string message, [CallerMemberName] string memberName = "") =>
        new Error(BuildCode(typeof(T), memberName), message);

    /// <summary>
    /// Creates an error with an automatically generated code using "Unspecified" as the member suffix.
    /// Useful as a catch-all error for a given type.
    /// </summary>
    /// <typeparam name="T">
    /// The type declaring this error. Used to derive the namespace and class portion of the code.
    /// </typeparam>
    /// <param name="message">Human-readable error message. Must not be null or whitespace.</param>
    /// <returns>An <see cref="IError"/> with code in the format <c>Namespace.Type.Unspecified</c>.</returns>
    /// <example>
    /// <code>
    /// // Declared in MyApp.Domain.Orders.Order:
    /// public static readonly IError Unknown = Error.Unspecified&lt;Order&gt;("An unexpected error occurred.");
    /// // Produces code: "MyApp.Domain.Orders.Order.Unspecified"
    /// </code>
    /// </example>
    public static IError Unspecified<T>(string message) =>
        new Error(BuildCode(typeof(T), nameof(Unspecified)), message);

    /// <summary>
    /// Builds a dot-separated error code from a type's full name and a member name.
    /// </summary>
    private static string BuildCode(Type type, string memberName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memberName);

        var typeName = type.FullName ?? type.Name;

        // Replace nested type separator (+) with dot for consistency
        typeName = typeName.Replace('+', '.');

        // Strip generic arity suffixes (e.g., `1, `2) for cleaner codes
        var backtickIndex = typeName.IndexOf('`');
        if (backtickIndex >= 0)
        {
            typeName = typeName[..backtickIndex];
        }

        return $"{typeName}.{memberName}";
    }
}
