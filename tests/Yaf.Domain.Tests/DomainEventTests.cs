using AwesomeAssertions;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain.Tests;

public class DomainEventInterfaceTests
{
    private record TestEvent : IDomainEvent
    {
        public Guid EventId { get; init; }
        public DateTimeOffset OccurredAtUtc { get; init; }
        public Guid CorrelationId { get; init; }
        public TenantId TenantId { get; init; } = null!;
        public ActorId ActorId { get; init; } = null!;
        public string? ActivityId { get; init; }
    }

    [Fact]
    public void IDomainEvent_CarriesEventId()
    {
        var eventId = Guid.NewGuid();
        var evt = new TestEvent { EventId = eventId };

        evt.EventId.Should().Be(eventId);
    }

    [Fact]
    public void IDomainEvent_CarriesOccurredAtUtc()
    {
        var now = DateTimeOffset.UtcNow;
        var evt = new TestEvent { OccurredAtUtc = now };

        evt.OccurredAtUtc.Should().Be(now);
    }

    [Fact]
    public void IDomainEvent_ImplementsICorrelated()
    {
        var correlationId = Guid.NewGuid();
        var evt = new TestEvent { CorrelationId = correlationId };

        (evt is ICorrelated).Should().BeTrue();
        ((ICorrelated)evt).CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public void IDomainEvent_ImplementsIActorScoped()
    {
        var actorId = new ActorId(Guid.NewGuid());
        var evt = new TestEvent { ActorId = actorId };

        (evt is IActorScoped).Should().BeTrue();
        ((IActorScoped)evt).ActorId.Should().Be(actorId);
    }

    [Fact]
    public void IDomainEvent_ImplementsIActivityScoped()
    {
        var activityId = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
        var evt = new TestEvent { ActivityId = activityId };

        (evt is IActivityScoped).Should().BeTrue();
        ((IActivityScoped)evt).ActivityId.Should().Be(activityId);
    }

    [Fact]
    public void IDomainEvent_ImplementsIActivityScoped_NullWhenNoTrace()
    {
        var evt = new TestEvent();

        evt.ActivityId.Should().BeNull();
    }

    [Fact]
    public void IDomainEvent_ImplementsITenantScoped()
    {
        var tenantId = new TenantId(Guid.NewGuid());
        var evt = new TestEvent { TenantId = tenantId };

        (evt is ITenantScoped).Should().BeTrue();
        evt.TenantId.Should().Be(tenantId);
    }
}

public class DomainEventGenericTests
{
    private record OrderData(string OrderNumber, decimal Total);

    private record OrderPlacedEvent : IDomainEvent<OrderData>
    {
        public Guid EventId { get; init; }
        public DateTimeOffset OccurredAtUtc { get; init; }
        public Guid CorrelationId { get; init; }
        public TenantId TenantId { get; init; } = null!;
        public ActorId ActorId { get; init; } = null!;
        public string? ActivityId { get; init; }
        public required OrderData Data { get; init; }
    }

    [Fact]
    public void IDomainEventOfT_CarriesTypedData()
    {
        var data = new OrderData("ORD-001", 99.99m);
        var evt = new OrderPlacedEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            TenantId = new TenantId(Guid.NewGuid()),
            ActorId = new ActorId(Guid.NewGuid()),
            Data = data
        };

        evt.Data.Should().Be(data);
        evt.Data.OrderNumber.Should().Be("ORD-001");
        evt.Data.Total.Should().Be(99.99m);
    }

    [Fact]
    public void IDomainEventOfT_IsAssignableToIDomainEvent()
    {
        var evt = new OrderPlacedEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            TenantId = new TenantId(Guid.NewGuid()),
            ActorId = new ActorId(Guid.NewGuid()),
            Data = new OrderData("ORD-001", 50m)
        };

        (evt is IDomainEvent).Should().BeTrue();
    }

    [Fact]
    public void IDomainEventOfT_Covariance_AssignableToBaseType()
    {
        var evt = new OrderPlacedEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            TenantId = new TenantId(Guid.NewGuid()),
            ActorId = new ActorId(Guid.NewGuid()),
            Data = new OrderData("ORD-001", 50m)
        };

        // Covariance: IDomainEvent<OrderData> is assignable to IDomainEvent<object>
        IDomainEvent<object> covariant = evt;

        covariant.Data.Should().BeOfType<OrderData>();
        covariant.EventId.Should().Be(evt.EventId);
    }
}
