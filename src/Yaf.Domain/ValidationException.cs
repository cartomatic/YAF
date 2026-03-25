using Yaf.Domain.Interfaces;

namespace Yaf.Domain;

/// <summary>
/// Thrown when a domain object fails validation after memento restoration.
/// </summary>
public sealed class ValidationException : Exception
{
    /// <summary>
    /// The type that failed validation.
    /// </summary>
    public Type ObjectType { get; }

    /// <summary>
    /// The validation errors that caused the failure.
    /// </summary>
    public IReadOnlyCollection<IError> Errors { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ValidationException"/>.
    /// </summary>
    /// <param name="objectType">The type that failed validation.</param>
    /// <param name="errors">The validation errors.</param>
    public ValidationException(Type objectType, IReadOnlyCollection<IError> errors)
        : base(FormatMessage(objectType, errors))
    {
        ArgumentNullException.ThrowIfNull(objectType);
        ArgumentNullException.ThrowIfNull(errors);

        ObjectType = objectType;
        Errors = errors;
    }

    private static string FormatMessage(Type objectType, IReadOnlyCollection<IError> errors)
    {
        ArgumentNullException.ThrowIfNull(objectType);
        ArgumentNullException.ThrowIfNull(errors);

        return $"Validation failed for {objectType.Name}: {string.Join("; ", errors.Select(e => $"{e.Code}: {e.Message}"))}";
    }
}
