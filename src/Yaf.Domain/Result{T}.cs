namespace Yaf.Domain;

/// <summary>
/// Represents the outcome of an operation that returns a value of type <typeparamref name="T"/>.
/// Either a success with a value, or a failure with one or more <see cref="Error"/> instances.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
/// <remarks>
/// <para>
/// <c>default(Result&lt;T&gt;)</c> is treated as a failure with a sentinel error indicating
/// the result was not properly initialized. Always use <see cref="Result.Success{T}"/>
/// or <see cref="Result.Failure{T}(Error)"/> to create instances.
/// </para>
/// <para>
/// Implicit conversions exist from <typeparamref name="T"/> (creating a success)
/// and from <see cref="Error"/> (creating a single-error failure).
/// </para>
/// </remarks>
public readonly struct Result<T> : IEquatable<Result<T>>
    where T : notnull
{
    private static readonly Error[] UninitializedErrors = [Result.Uninitialized];

    private readonly T? _value;
    private readonly Error[]? _errors;
    private readonly bool _isSuccess;

    private Result(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
        _errors = null;
        _isSuccess = true;
    }

    private Result(Error[] errors)
    {
        _value = default;
        _errors = errors;
        _isSuccess = false;
    }

    /// <summary>
    /// Gets a value indicating whether this result represents a successful outcome.
    /// </summary>
    public bool IsSuccess => _isSuccess;

    /// <summary>
    /// Gets a value indicating whether this result represents a failed outcome.
    /// Returns <c>true</c> for <c>default(Result&lt;T&gt;)</c>.
    /// </summary>
    public bool IsFailure => !_isSuccess;

    /// <summary>
    /// Gets the success value.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the result is a failure or uninitialized.</exception>
    public T Value => _isSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value on a failed result.");

    /// <summary>
    /// Gets the first error. When the failure contains multiple errors, returns the first one.
    /// For <c>default(Result&lt;T&gt;)</c>, returns a sentinel uninitialized error.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the result is a success.</exception>
    public Error Error => _isSuccess
        ? throw new InvalidOperationException("Cannot access Error on a successful result.")
        : (_errors ?? UninitializedErrors)[0];

    /// <summary>
    /// Gets all errors as a read-only list.
    /// For <c>default(Result&lt;T&gt;)</c>, returns a single-element collection with a sentinel error.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the result is a success.</exception>
    public IReadOnlyList<Error> Errors => _isSuccess
        ? throw new InvalidOperationException("Cannot access Errors on a successful result.")
        : _errors ?? UninitializedErrors;

    /// <summary>
    /// Implicitly converts a value to a successful <see cref="Result{T}"/>.
    /// </summary>
    /// <param name="value">The success value. Must not be null.</param>
    public static implicit operator Result<T>(T value) => new(value);

    /// <summary>
    /// Implicitly converts an <see cref="Error"/> to a failed <see cref="Result{T}"/>.
    /// </summary>
    /// <param name="error">The error. Must not be null.</param>
    public static implicit operator Result<T>(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>([error]);
    }

    /// <summary>
    /// Creates a successful <see cref="Result{T}"/> with the specified value.
    /// </summary>
    internal static Result<T> CreateSuccess(T value) => new(value);

    /// <summary>
    /// Creates a failed <see cref="Result{T}"/> with the specified errors.
    /// </summary>
    internal static Result<T> CreateFailure(Error[] errors) => new(errors);

    /// <inheritdoc />
    public bool Equals(Result<T> other)
    {
        if (_isSuccess != other._isSuccess)
            return false;

        if (_isSuccess)
            return EqualityComparer<T>.Default.Equals(_value!, other._value!);

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
        obj is Result<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        if (_isSuccess)
            return HashCode.Combine(true, EqualityComparer<T>.Default.GetHashCode(_value!));

        var hash = new HashCode();
        hash.Add(false);
        foreach (var error in _errors ?? UninitializedErrors)
            hash.Add(error);
        return hash.ToHashCode();
    }

    /// <summary>
    /// Determines whether two <see cref="Result{T}"/> instances are equal.
    /// </summary>
    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="Result{T}"/> instances are not equal.
    /// </summary>
    public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString()
    {
        if (_isSuccess)
            return $"Success({_value})";

        if (_errors is null)
            return $"Result<{typeof(T).Name}>(Uninitialized)";

        return $"Failure({string.Join(", ", _errors.Select(e => e.Code))})";
    }
}
