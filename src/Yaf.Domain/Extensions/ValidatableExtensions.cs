using Yaf.Domain.Interfaces;

namespace Yaf.Domain.Extensions;

/// <summary>
/// Extension methods for <see cref="IValidatable"/>.
/// </summary>
public static class ValidatableExtensions
{
    /// <summary>
    /// Validates the domain object and throws <see cref="ValidationException"/> if the state is invalid.
    /// </summary>
    /// <param name="validatable">The domain object to validate.</param>
    /// <exception cref="ValidationException">Thrown when validation produces one or more errors.</exception>
    public static void ThrowIfInvalid(this IValidatable validatable)
    {
        ArgumentNullException.ThrowIfNull(validatable);

        var errors = validatable.Validate();
        if (errors.Count > 0)
        {
            throw new ValidationException(validatable.GetType(), errors);
        }
    }
}
