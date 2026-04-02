namespace Yaf.Domain;

/// <summary>
/// Represents the outcome of a void-equivalent operation.
/// Either a success or a failure with one or more <see cref="Error"/> instances.
/// Also provides static factory methods for creating both <see cref="Result"/>
/// and <see cref="Result{T}"/> instances.
/// </summary>
/// <remarks>
/// <c>default(Result)</c> is treated as a failure with a sentinel error indicating
/// the result was not properly initialized. Always use <see cref="Success()"/>
/// or <see cref="Failure(Error)"/> to create instances.
/// </remarks>
public readonly struct Result : IEquatable<Result>
{
    private static readonly Error[] UninitializedErrors =
        [new Error("Yaf.Domain.Result.Uninitialized", "Result was not properly initialized.")];

    private readonly Error[]? _errors;
    private readonly bool _isSuccess;

    private Result(bool success, Error[]? errors)
    {
        _isSuccess = success;
        _errors = errors;
    }

    /// <summary>
    /// Gets a value indicating whether this result represents a successful outcome.
    /// </summary>
    public bool IsSuccess => _isSuccess;

    /// <summary>
    /// Gets a value indicating whether this result represents a failed outcome.
    /// Returns <c>true</c> for <c>default(Result)</c>.
    /// </summary>
    public bool IsFailure => !_isSuccess;

    /// <summary>
    /// Gets the first error. When the failure contains multiple errors, returns the first one.
    /// For <c>default(Result)</c>, returns a sentinel uninitialized error.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the result is a success.</exception>
    public Error Error => _isSuccess
        ? throw new InvalidOperationException("Cannot access Error on a successful result.")
        : (_errors ?? UninitializedErrors)[0];

    /// <summary>
    /// Gets all errors as a read-only list.
    /// For <c>default(Result)</c>, returns a single-element collection with a sentinel error.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the result is a success.</exception>
    public IReadOnlyList<Error> Errors => _isSuccess
        ? throw new InvalidOperationException("Cannot access Errors on a successful result.")
        : _errors ?? UninitializedErrors;

    /// <summary>
    /// Implicitly converts an <see cref="Error"/> to a failed <see cref="Result"/>.
    /// </summary>
    /// <param name="error">The error. Must not be null.</param>
    public static implicit operator Result(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(false, [error]);
    }

    // ── Static factories ───────────────────────────────────────────────

    /// <summary>
    /// Creates a successful <see cref="Result"/>.
    /// </summary>
    public static Result Success() => new(true, null);

    /// <summary>
    /// Creates a successful <see cref="Result{T}"/> with the specified value.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="value">The success value. Must not be null.</param>
    public static Result<T> Success<T>(T value) where T : notnull =>
        Result<T>.CreateSuccess(value);

    /// <summary>
    /// Creates a failed <see cref="Result"/> with a single error.
    /// </summary>
    /// <param name="error">The error. Must not be null.</param>
    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(false, [error]);
    }

    /// <summary>
    /// Creates a failed <see cref="Result"/> with multiple errors.
    /// </summary>
    /// <param name="errors">The errors. Must not be null, empty, or contain null elements.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="errors"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="errors"/> is empty or contains null elements.
    /// </exception>
    public static Result Failure(IReadOnlyCollection<Error> errors)
    {
        var array = ValidateErrors(errors);
        return new Result(false, array);
    }

    /// <summary>
    /// Creates a failed <see cref="Result{T}"/> with a single error.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="error">The error. Must not be null.</param>
    public static Result<T> Failure<T>(Error error) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(error);
        return Result<T>.CreateFailure([error]);
    }

    /// <summary>
    /// Creates a failed <see cref="Result{T}"/> with multiple errors.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <param name="errors">The errors. Must not be null, empty, or contain null elements.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="errors"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="errors"/> is empty or contains null elements.
    /// </exception>
    public static Result<T> Failure<T>(IReadOnlyCollection<Error> errors) where T : notnull
    {
        var array = ValidateErrors(errors);
        return Result<T>.CreateFailure(array);
    }

    // ── Equality ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public bool Equals(Result other)
    {
        if (_isSuccess != other._isSuccess)
            return false;

        if (_isSuccess)
            return true;

        var left = _errors ?? UninitializedErrors;
        var right = other._errors ?? UninitializedErrors;

        if (left.Length != right.Length)
            return false;

        for (var i = 0; i < left.Length; i++)
        {
            if (!left[i].Equals(right[i]))
                return false;
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is Result other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        if (_isSuccess)
            return HashCode.Combine(true);

        var hash = new HashCode();
        hash.Add(false);
        foreach (var error in _errors ?? UninitializedErrors)
            hash.Add(error);
        return hash.ToHashCode();
    }

    /// <summary>
    /// Determines whether two <see cref="Result"/> instances are equal.
    /// </summary>
    public static bool operator ==(Result left, Result right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="Result"/> instances are not equal.
    /// </summary>
    public static bool operator !=(Result left, Result right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString()
    {
        if (_isSuccess)
            return "Success";

        if (_errors is null)
            return "Result(Uninitialized)";

        return $"Failure({string.Join(", ", _errors.Select(e => e.Code))})";
    }

    // ── Private helpers ────────────────────────────────────────────────

    private static Error[] ValidateErrors(IReadOnlyCollection<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 0)
            throw new ArgumentException("Error collection must not be empty.", nameof(errors));

        var array = errors.ToArray();

        for (var i = 0; i < array.Length; i++)
        {
            if (array[i] is null)
                throw new ArgumentException("Error collection must not contain null elements.", nameof(errors));
        }

        return array;
    }
}
