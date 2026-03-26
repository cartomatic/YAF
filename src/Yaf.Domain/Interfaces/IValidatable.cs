namespace Yaf.Domain.Interfaces;

/// <summary>
/// Domain objects that can validate their own state.
/// </summary>
public interface IValidatable
{
    /// <summary>
    /// Validates the current state of this domain object.
    /// Returns an empty collection if the state is valid.
    /// </summary>
    /// <returns>A collection of validation errors, empty if valid.</returns>
    IReadOnlyCollection<IError> Validate();
}
