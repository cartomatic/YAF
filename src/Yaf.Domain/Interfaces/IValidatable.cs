namespace Yaf.Domain.Interfaces;

/// <summary>
/// Domain objects that can validate their own state.
/// </summary>
/// <remarks>
/// Use the <c>IsValid()</c> extension for a boolean check
/// and <c>ThrowIfInvalid()</c> extension for a guard clause.
/// </remarks>
public interface IValidatable
{
    /// <summary>
    /// Returns validation errors for the current state of this domain object.
    /// An empty collection indicates valid state.
    /// </summary>
    /// <returns>A collection of validation errors, empty if valid.</returns>
    IReadOnlyCollection<Error> GetValidationErrors();
}
