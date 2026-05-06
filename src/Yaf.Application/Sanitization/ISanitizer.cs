namespace Yaf.Application.Sanitization;

/// <summary>
/// Pipeline component that produces a normalized copy of an instance whose properties —
/// or whose declaring type — are decorated with <see cref="SanitizeAttribute"/>, prior
/// to validation and handler execution.
/// </summary>
/// <remarks>
/// <para>
/// Sanitizers run in the application's mediator/dispatcher pipeline <b>before</b>
/// <see cref="Yaf.Application.Validation.IValidator{T}"/>. The pipeline order is:
/// sanitize → validate → handle. This ordering guarantees that validators see an
/// already-normalized instance and never have to defend against trivially correctable
/// shape issues such as leading whitespace or inconsistent casing.
/// </para>
/// <para>
/// <b>Opt-in via <see cref="SanitizeAttribute"/>.</b> Implementations inspect the
/// instance's runtime type for the attribute. When applied to a property, that single
/// property is sanitized. When applied to the type, every supported string-shaped
/// property — <see cref="string"/>, <see cref="string"/>[],
/// <see cref="System.Collections.Generic.List{T}"/> of <see cref="string"/>,
/// <see cref="System.Collections.Generic.IList{T}"/> of <see cref="string"/>, and
/// <see cref="System.Collections.Generic.IReadOnlyList{T}"/> of <see cref="string"/> —
/// is sanitized automatically. Types with neither annotation flow through unchanged.
/// </para>
/// <para>
/// <b>Synchronous by design.</b> Sanitization is an in-memory transformation — it
/// trims, lower-cases, normalizes, or rewrites string values using only data already
/// present on the instance itself. It does not perform I/O, look up state in a
/// repository, or call out to external services. A synchronous signature makes that
/// constraint visible at the type level: implementations that need to perform I/O are
/// almost certainly doing validation or enrichment, not sanitization, and belong in
/// <see cref="Yaf.Application.Validation.IValidator{T}"/> or a dedicated pipeline step.
/// </para>
/// <para>
/// <b>Return-a-copy semantics.</b> <see cref="Sanitize{T}(T)"/> returns the sanitized
/// instance rather than mutating the input. This is required for compatibility with
/// <c>record</c> types and other immutable shapes that lack public setters; an
/// implementation typically uses a non-destructive <c>with</c> expression to produce
/// the sanitized copy. Class-based payloads may legitimately have their string
/// properties rewritten in place, but consumers must treat the returned value as the
/// authoritative sanitized instance and discard the original reference.
/// </para>
/// </remarks>
public interface ISanitizer
{
    /// <summary>
    /// Returns a sanitized copy of <paramref name="instance"/>, applying the rules
    /// declared by any <see cref="SanitizeAttribute"/> annotations on the type or its
    /// properties.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the instance being sanitized. Types whose properties or type
    /// declaration carry <see cref="SanitizeAttribute"/> are processed; other types
    /// are returned unchanged.
    /// </typeparam>
    /// <param name="instance">
    /// The instance to sanitize. Implementations must throw
    /// <see cref="ArgumentNullException"/> when <paramref name="instance"/> is
    /// <see langword="null"/>.
    /// </param>
    /// <returns>
    /// The sanitized instance. Implementations should return a new value for
    /// <c>record</c> and other immutable shapes; the returned value, not the input,
    /// is the canonical instance for the remainder of the pipeline.
    /// </returns>
    T Sanitize<T>(T instance);
}
