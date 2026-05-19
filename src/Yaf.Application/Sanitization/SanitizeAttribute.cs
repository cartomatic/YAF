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
/// </remarks>
[AttributeUsage(
    AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Struct,
    AllowMultiple = false,
    Inherited = true)]
public sealed class SanitizeAttribute : Attribute;
