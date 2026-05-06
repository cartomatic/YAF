using System.Reflection;
using AwesomeAssertions;
using Yaf.Application.Notifications;

namespace Yaf.Application.Tests.Notifications;

public class NotificationContractsTests
{
    [Fact]
    public void INotification_IsMarkerInterface_HasNoMembers()
    {
        var members = typeof(INotification).GetMembers(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        members.Should().BeEmpty();
    }

    [Fact]
    public async Task INotificationHandler_HandleMethod_ReturnsTask()
    {
        var handleMethod = typeof(INotificationHandler<SampleNotification>)
            .GetMethod(nameof(INotificationHandler<SampleNotification>.Handle))!;

        handleMethod.ReturnType.Should().Be(
            typeof(Task),
            "fan-out notifications return non-generic Task — no aggregated Result is produced");

        INotificationHandler<SampleNotification> handler = new SampleNotificationHandler();
        await handler.Handle(new SampleNotification(), CancellationToken.None);
    }

    [Fact]
    public void INotificationHandler_TNotification_IsContravariant()
    {
        var typeParameter = typeof(INotificationHandler<>)
            .GetGenericArguments()
            .Single(t => t.Name == "TNotification");

        typeParameter.GenericParameterAttributes
            .HasFlag(GenericParameterAttributes.Contravariant)
            .Should().BeTrue();
    }
}

#region Test Types

internal sealed record SampleNotification : INotification;

internal sealed class SampleNotificationHandler : INotificationHandler<SampleNotification>
{
    public Task Handle(SampleNotification notification, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

#endregion
