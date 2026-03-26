using Yaf.Domain.Interfaces;

namespace Yaf.Domain.Extensions;

/// <summary>
/// Extension methods for <see cref="IValidatable"/>.
/// </summary>
public static class ValidatableExtensions
{
    /// <summary>
    /// Returns <c>true</c> if the domain object's state is valid (no validation errors).
    /// </summary>
    public static bool IsValid(this IValidatable validatable) =>
        validatable.GetValidationErrors().Count == 0;

    /// <summary>
    /// Validates the domain object and throws <see cref="ValidationException"/> if the state is invalid.
    /// </summary>
    /// <exception cref="ValidationException">Thrown when validation produces one or more errors.</exception>
    public static void ThrowIfInvalid(this IValidatable validatable)
    {
        if (!validatable.IsValid())
        {
            throw new ValidationException(validatable.GetType(), validatable.GetValidationErrors());
        }
    }
}
