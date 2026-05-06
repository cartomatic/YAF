using Yaf.Domain;

namespace Yaf.Application.Validation;

/// <summary>
/// Pipeline validator that asserts business preconditions on an instance of
/// <typeparamref name="T"/> before it reaches its handler.
/// </summary>
/// <remarks>
/// <para>
/// Validators run in the application's mediator/dispatcher pipeline after sanitization
/// and before the handler. A failed <see cref="Result"/> short-circuits the pipeline
/// and prevents the handler from executing; a successful <see cref="Result"/> permits
/// the pipeline to continue.
/// </para>
/// <para>
/// <typeparamref name="T"/> is contravariant (<c>in</c>) so that a validator written
/// against a base type can be substituted where a validator for a derived type is
/// required, enabling cross-cutting validators that target shared command or query
/// base types.
/// </para>
/// <para>
/// The contract is asynchronous (<see cref="Task{TResult}"/>) because application-level
/// validation commonly performs I/O — for example, repository lookups to confirm that
/// a referenced aggregate exists or that a uniqueness constraint is satisfied. This
/// is a deliberate drift from ADR-1141, which originally specified a synchronous
/// <c>Result Validate</c> signature; the application layer chose asynchronous semantics
/// to accommodate those out-of-process checks.
/// </para>
/// <para>
/// The return type is <see cref="Result"/> rather than <see cref="Result"/>&lt;T&gt;
/// because validation produces a pass/fail outcome with one or more <see cref="Error"/>
/// values, not a transformed value. The original instance is left untouched and flows
/// onward through the pipeline.
/// </para>
/// </remarks>
/// <typeparam name="T">The type validated by this implementation.</typeparam>
public interface IValidator<in T>
{
    /// <summary>
    /// Validates the supplied <paramref name="instance"/> against business preconditions.
    /// </summary>
    /// <param name="instance">
    /// The instance to validate. Implementations must throw
    /// <see cref="ArgumentNullException"/> when <paramref name="instance"/> is
    /// <see langword="null"/>.
    /// </param>
    /// <param name="cancellationToken">A token observed for cooperative cancellation.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> producing a <see cref="Result"/> that represents
    /// either success — permitting the pipeline to continue — or one or more
    /// <see cref="Error"/> values describing why validation failed.
    /// </returns>
    Task<Result> ValidateAsync(T instance, CancellationToken cancellationToken);
}
