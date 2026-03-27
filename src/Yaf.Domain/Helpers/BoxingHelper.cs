namespace Yaf.Domain.Helpers;

/// <summary>
/// Shared unboxing logic for memento interface DIM implementations.
/// </summary>
internal static class BoxingHelper
{
    /// <summary>
    /// Unboxes a value to <typeparamref name="T"/>?, returning <see langword="null"/> for null input
    /// and throwing <see cref="ArgumentException"/> on type mismatch.
    /// </summary>
    internal static T? Unbox<T>(object? value) where T : struct =>
        value switch
        {
            null => null,
            T typed => typed,
            _ => throw new ArgumentException(
                $"Expected {typeof(T).Name}, got {value.GetType().Name}.", nameof(value))
        };
}
