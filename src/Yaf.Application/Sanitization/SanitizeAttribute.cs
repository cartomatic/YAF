namespace Yaf.Application.Sanitization;

/// <summary>
/// Marks a property — or every applicable property of a type — as requiring
/// sanitization by an <see cref="ISanitizer"/> before validation and handler execution.
/// </summary>
/// <remarks>
/// <para>
/// <b>Per-property usage.</b> Apply <c>[Sanitize]</c> to an individual property to opt
/// it in to sanitization explicitly:
/// </para>
/// <code>
/// public sealed record RegisterUserCommand(
///     [property: Sanitize] string Email,
///     string DisplayName) : ICommand&lt;ActorId&gt;;
/// </code>
/// <para>
/// <b>Per-type usage.</b> Apply <c>[Sanitize]</c> to a class or record to opt every
/// supported string-shaped property in automatically. The sanitizer treats the
/// following property types as in-scope: <see cref="string"/>, <see cref="string"/>[]
/// (array), <see cref="System.Collections.Generic.List{T}"/> of
/// <see cref="string"/>, <see cref="System.Collections.Generic.IList{T}"/> of
/// <see cref="string"/>, and <see cref="System.Collections.Generic.IReadOnlyList{T}"/>
/// of <see cref="string"/>. Other property types are left untouched. Per-property
/// <c>[Sanitize]</c> attributes layered on top remain valid and are not double-applied.
/// </para>
/// <code>
/// [Sanitize]
/// public sealed record CreatePostCommand(
///     string Title,
///     string Body,
///     IReadOnlyList&lt;string&gt; Tags,
///     int Priority) : ICommand&lt;PostId&gt;;
/// // Title, Body, and Tags are sanitized automatically; Priority is left alone.
/// </code>
/// <para>
/// <b>Pipeline ordering.</b> Sanitization runs before
/// <see cref="Yaf.Application.Validation.IValidator{T}"/> in the application's
/// mediator/dispatcher pipeline (sanitize → validate → handle). Validators always
/// see normalized input.
/// </para>
/// <para>
/// <b>ADR drift — placement in <c>Yaf.Application</c> rather than <c>Yaf.Domain</c>.</b>
/// ADR-1146 originally placed sanitization abstractions in the Domain layer alongside
/// other cross-cutting markers. YAF drifts from that decision and hosts
/// <c>SanitizeAttribute</c> and <see cref="ISanitizer"/> in the Application layer
/// instead. Sanitization is a pipeline concern — it happens to commands, queries, and
/// DTOs as they enter the application, before the domain is touched. Domain entities
/// encapsulate invariants and never need to be re-shaped from the outside, so a
/// Domain-layer marker would have no domain consumers.
/// </para>
/// <para>
/// <b>Drift from earlier marker design.</b> An earlier revision used an
/// <c>ISanitizable</c> marker interface for opt-in. The attribute form was chosen
/// instead because (a) it expresses sanitization at the field level rather than only
/// at the type level, (b) it composes naturally with class-level opt-in for the common
/// "all strings" case, and (c) it carries no implementation surface that consumers
/// might be tempted to extend.
/// </para>
/// </remarks>
[AttributeUsage(
    AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Struct,
    AllowMultiple = false,
    Inherited = true)]
public sealed class SanitizeAttribute : Attribute;
