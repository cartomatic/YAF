namespace Yaf.Application.Notifications;

/// <summary>
/// Marker for an application-level notification dispatched to zero or more
/// independent handlers (fan-out semantics).
/// </summary>
/// <remarks>
/// <para>
/// Notifications express that something has happened. Unlike a
/// <see cref="Yaf.Application.Cqrs.ICommand"/>, a notification has no single owner —
/// any number of <see cref="INotificationHandler{TNotification}"/> implementations
/// may subscribe to it, and each runs independently. There is no aggregated outcome
/// returned to the dispatcher; failures surface via exceptions or are logged inside
/// the handler.
/// </para>
/// <para>
/// Notifications are the application-layer counterpart to domain events
/// (see <see cref="Yaf.Domain.Interfaces.IDomainEvent"/>). Domain events describe
/// state changes inside an aggregate; the application layer typically wraps or
/// translates them into notifications so that infrastructure concerns (sending
/// email, updating a read model, integrating with another bounded context) can
/// subscribe without coupling the domain to the application.
/// </para>
/// </remarks>
public interface INotification;
