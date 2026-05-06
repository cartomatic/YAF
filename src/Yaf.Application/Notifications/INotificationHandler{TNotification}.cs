namespace Yaf.Application.Notifications;

/// <summary>
/// Handles an <see cref="INotification"/> as one of potentially many independent
/// subscribers in a fan-out dispatch.
/// </summary>
/// <remarks>
/// <para>
/// Each handler runs independently of the others; the dispatcher does not aggregate
/// or coordinate their outcomes. For this reason <see cref="Handle"/> returns a
/// non-generic <see cref="Task"/> rather than <c>Task&lt;Yaf.Domain.Result&gt;</c> —
/// there is no single success/failure outcome to communicate back to the publisher.
/// Handlers that need to signal failure should throw, and infrastructure decides
/// whether to log, retry, or rethrow.
/// </para>
/// <para>
/// <typeparamref name="TNotification"/> is contravariant (<c>in</c>) so that a handler
/// for a base notification type can be substituted where a handler for a derived
/// notification is required.
/// </para>
/// </remarks>
/// <typeparam name="TNotification">The notification type handled by this implementation.</typeparam>
public interface INotificationHandler<in TNotification> where TNotification : INotification
{
    /// <summary>
    /// Handles the supplied <paramref name="notification"/>.
    /// </summary>
    /// <param name="notification">
    /// The notification to process. Implementations must throw
    /// <see cref="ArgumentNullException"/> when <paramref name="notification"/>
    /// is <see langword="null"/>.
    /// </param>
    /// <param name="cancellationToken">A token observed for cooperative cancellation.</param>
    /// <returns>
    /// A <see cref="Task"/> that completes when this handler has finished processing
    /// the notification. The dispatcher does not interpret the task's outcome beyond
    /// awaiting it; failures are surfaced via thrown exceptions.
    /// </returns>
    Task Handle(TNotification notification, CancellationToken cancellationToken);
}
