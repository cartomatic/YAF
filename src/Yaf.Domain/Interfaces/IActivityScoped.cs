namespace Yaf.Domain.Interfaces;

/// <summary>
/// Carries the activity identifier of the in-process operation that originated this object.
/// </summary>
/// <remarks>
/// <para>
/// Maps to <see cref="System.Diagnostics.Activity.Id"/> in .NET's distributed tracing model.
/// The activity ID is a <see cref="string"/> because W3C trace context IDs are string-formatted
/// (e.g., <c>"00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"</c>).
/// </para>
/// <para>
/// Nullable because not all execution contexts have an active <see cref="System.Diagnostics.Activity"/>
/// (e.g., background jobs without tracing, test environments).
/// Infrastructure populates this from <c>IActivityIdProvider</c>.
/// </para>
/// <para>
/// This interface is standalone and reusable — domain events, commands, queries,
/// and integration events can all implement it.
/// </para>
/// </remarks>
public interface IActivityScoped
{
    /// <summary>
    /// The activity identifier from the originating operation.
    /// <see langword="null"/> when no active trace context is available.
    /// </summary>
    string? ActivityId { get; }
}
