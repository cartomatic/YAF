namespace Yaf.Application.Context;

/// <summary>
/// Exposes the ambient activity identifier from .NET's distributed tracing model
/// for the in-flight operation.
/// </summary>
/// <remarks>
/// <para>
/// The activity identifier is the W3C trace context value carried by the current
/// <see cref="System.Diagnostics.Activity"/> (for example,
/// <c>"00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"</c>). Infrastructure
/// surfaces it through this provider so that pipeline behaviors and persistence
/// adapters can stamp commands, notifications, and entities implementing
/// <see cref="Yaf.Domain.Interfaces.IActivityScoped"/> with the originating trace
/// identifier. The identifier is exposed as <see cref="string"/> rather than
/// <see cref="Guid"/> because W3C trace context IDs are formatted strings, not GUIDs.
/// </para>
/// <para>
/// <see cref="ActivityId"/> is nullable because not every execution context has an
/// active <see cref="System.Diagnostics.Activity"/>. Background jobs without a
/// configured tracing pipeline, ad-hoc unit tests, and minimally instrumented hosts
/// legitimately run without one. Consumers must therefore tolerate a <see langword="null"/>
/// activity identifier and avoid materializing a synthetic value that would later be
/// indistinguishable from a real trace.
/// </para>
/// </remarks>
public interface IActivityIdProvider
{
    /// <summary>
    /// The W3C trace-context activity identifier of the current ambient operation,
    /// or <see langword="null"/> when no <see cref="System.Diagnostics.Activity"/>
    /// is active.
    /// </summary>
    string? ActivityId { get; }
}
