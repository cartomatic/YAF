using AwesomeAssertions;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain.Tests;

#region Test Types

internal record UserId(Guid Value) : TypedId(Value);

internal record TestOrderId(Guid Value) : TypedId(Value);

// --- Full entity: all cross-cutting interfaces, manual mapping ---

internal interface IOrderMemento :
    IHasIdentity,
    IHasAccountability,
    IHasTimestamps,
    IHasSoftDelete,
    IHasTenantId
{
    string Description { get; set; }
}

internal class OrderMemento : IOrderMemento
{
    public Guid? Id { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? ModifiedBy { get; set; }
    public DateTimeOffset? CreatedAtUtc { get; set; }
    public DateTimeOffset? ModifiedAtUtc { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }
    public Guid? TenantId { get; set; }
    public string Description { get; set; } = string.Empty;
}

internal class Order :
    Entity<TestOrderId, Order, IOrderMemento>,
    IAccountable<UserId>,
    ITimestamped,
    ISoftDeletable<UserId>,
    ITenantScoped<TenantId>
{
    public string Description { get; private set; } = string.Empty;
    public UserId? CreatedBy { get; private set; }
    public UserId? ModifiedBy { get; private set; }
    public DateTimeOffset? CreatedAtUtc { get; private set; }
    public DateTimeOffset? ModifiedAtUtc { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }
    public UserId? DeletedBy { get; private set; }
    public TenantId TenantId { get; private set; } = null!;

    private Order(TestOrderId id, string description, TenantId tenantId) : base(id)
    {
        Description = description;
        TenantId = tenantId;
    }

    public static Order Create(TestOrderId id, string description, TenantId tenantId) =>
        new(id, description, tenantId);

    // Identity, timestamps, accountability, and soft-delete are auto-mapped by the base class.
    // Only tenant and entity-specific properties need manual mapping.
    protected override void SnapshotCore(IOrderMemento memento)
    {
        memento.Description = Description;
        memento.TenantId = TenantId?.Value;
    }

    protected override void HydrateCore(IOrderMemento memento)
    {
        Description = memento.Description;
        TenantId = memento.TenantId is { } t ? new TenantId(t) : null!;
    }

    public override IReadOnlyCollection<IError> GetValidationErrors() =>
        string.IsNullOrWhiteSpace(Description)
            ? [new OrderError("EMPTY_DESC", "Description cannot be empty")]
            : [];

    private record OrderError(string Code, string Message) : IError;
}

// --- Plain entity without cross-cutting interfaces (backward compat) ---

internal interface IPlainMemento : IHasIdentity
{
    string Label { get; set; }
}

internal class PlainMemento : IPlainMemento
{
    public Guid? Id { get; set; }
    public string Label { get; set; } = string.Empty;
}

internal class PlainEntity : Entity<TestOrderId, PlainEntity, IPlainMemento>
{
    public string Label { get; private set; } = string.Empty;

    private PlainEntity(TestOrderId id, string label) : base(id) { Label = label; }

    public static PlainEntity Create(TestOrderId id, string label) => new(id, label);

    protected override void SnapshotCore(IPlainMemento memento) => memento.Label = Label;
    protected override void HydrateCore(IPlainMemento memento) => Label = memento.Label;
    public override IReadOnlyCollection<IError> GetValidationErrors() => [];
}

#endregion

public class AccountabilityTests
{
    [Fact]
    public void RoundTrip_AccountabilityFields()
    {
        var createdByGuid = Guid.NewGuid();
        var modifiedByGuid = Guid.NewGuid();
        var tenantGuid = Guid.NewGuid();
        var memento = new OrderMemento
        {
            Id = Guid.NewGuid(),
            CreatedBy = createdByGuid,
            ModifiedBy = modifiedByGuid,
            TenantId = tenantGuid,
            Description = "Test"
        };

        var order = Order.Restore(memento);

        order.CreatedBy.Should().NotBeNull();
        order.CreatedBy!.Value.Should().Be(createdByGuid);
        order.ModifiedBy.Should().NotBeNull();
        order.ModifiedBy!.Value.Should().Be(modifiedByGuid);

        var outputMemento = new OrderMemento();
        order.Snapshot(outputMemento);

        outputMemento.CreatedBy.Should().Be(createdByGuid);
        outputMemento.ModifiedBy.Should().Be(modifiedByGuid);
    }

    [Fact]
    public void NullAccountability_RoundTrips()
    {
        var memento = new OrderMemento
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Description = "Test"
        };

        var order = Order.Restore(memento);

        order.CreatedBy.Should().BeNull();
        order.ModifiedBy.Should().BeNull();
    }
}

public class TimestampTests
{
    [Fact]
    public void RoundTrip_TimestampFields()
    {
        var createdAt = DateTimeOffset.UtcNow.AddHours(-1);
        var modifiedAt = DateTimeOffset.UtcNow;
        var memento = new OrderMemento
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = createdAt,
            ModifiedAtUtc = modifiedAt,
            TenantId = Guid.NewGuid(),
            Description = "Test"
        };

        var order = Order.Restore(memento);

        order.CreatedAtUtc.Should().Be(createdAt);
        order.ModifiedAtUtc.Should().Be(modifiedAt);

        var outputMemento = new OrderMemento();
        order.Snapshot(outputMemento);

        outputMemento.CreatedAtUtc.Should().Be(createdAt);
        outputMemento.ModifiedAtUtc.Should().Be(modifiedAt);
    }

    [Fact]
    public void NullTimestamps_RoundTrips()
    {
        var memento = new OrderMemento
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Description = "Test"
        };

        var order = Order.Restore(memento);

        order.CreatedAtUtc.Should().BeNull();
        order.ModifiedAtUtc.Should().BeNull();
    }
}

public class SoftDeleteTests
{
    [Fact]
    public void RoundTrip_SoftDeleteFields()
    {
        var deletedAtUtc = DateTimeOffset.UtcNow;
        var deletedByGuid = Guid.NewGuid();
        var memento = new OrderMemento
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Description = "Test",
            DeletedAtUtc = deletedAtUtc,
            DeletedBy = deletedByGuid
        };

        var order = Order.Restore(memento);

        order.DeletedAtUtc.Should().Be(deletedAtUtc);
        order.DeletedBy!.Value.Should().Be(deletedByGuid);

        var outputMemento = new OrderMemento();
        order.Snapshot(outputMemento);

        outputMemento.DeletedAtUtc.Should().Be(deletedAtUtc);
        outputMemento.DeletedBy.Should().Be(deletedByGuid);
    }

    [Fact]
    public void NotDeleted_NullFields()
    {
        var memento = new OrderMemento
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Description = "Test"
        };

        var order = Order.Restore(memento);

        order.DeletedAtUtc.Should().BeNull();
        order.DeletedBy.Should().BeNull();
    }
}

public class TenantScopedTests
{
    [Fact]
    public void RoundTrip_TenantId()
    {
        var tenantGuid = Guid.NewGuid();
        var order = Order.Create(new TestOrderId(Guid.NewGuid()), "Test", new TenantId(tenantGuid));
        var memento = new OrderMemento();

        order.Snapshot(memento);

        memento.TenantId.Should().Be(tenantGuid);

        var restored = Order.Restore(memento);
        restored.TenantId.Value.Should().Be(tenantGuid);
    }
}

public class FullRoundTripTests
{
    [Fact]
    public void AllFields_RoundTrip()
    {
        var idGuid = Guid.NewGuid();
        var createdByGuid = Guid.NewGuid();
        var modifiedByGuid = Guid.NewGuid();
        var deletedByGuid = Guid.NewGuid();
        var tenantGuid = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow.AddHours(-2);
        var modifiedAt = DateTimeOffset.UtcNow.AddHours(-1);
        var deletedAt = DateTimeOffset.UtcNow;

        var memento = new OrderMemento
        {
            Id = idGuid,
            CreatedBy = createdByGuid,
            ModifiedBy = modifiedByGuid,
            CreatedAtUtc = createdAt,
            ModifiedAtUtc = modifiedAt,
            DeletedAtUtc = deletedAt,
            DeletedBy = deletedByGuid,
            TenantId = tenantGuid,
            Description = "Full test"
        };

        var order = Order.Restore(memento);

        order.Id.Value.Should().Be(idGuid);
        order.CreatedBy!.Value.Should().Be(createdByGuid);
        order.ModifiedBy!.Value.Should().Be(modifiedByGuid);
        order.CreatedAtUtc.Should().Be(createdAt);
        order.ModifiedAtUtc.Should().Be(modifiedAt);
        order.DeletedAtUtc.Should().Be(deletedAt);
        order.DeletedBy!.Value.Should().Be(deletedByGuid);
        order.TenantId.Value.Should().Be(tenantGuid);
        order.Description.Should().Be("Full test");

        var outputMemento = new OrderMemento();
        order.Snapshot(outputMemento);

        outputMemento.Id.Should().Be(idGuid);
        outputMemento.CreatedBy.Should().Be(createdByGuid);
        outputMemento.ModifiedBy.Should().Be(modifiedByGuid);
        outputMemento.CreatedAtUtc.Should().Be(createdAt);
        outputMemento.ModifiedAtUtc.Should().Be(modifiedAt);
        outputMemento.DeletedAtUtc.Should().Be(deletedAt);
        outputMemento.DeletedBy.Should().Be(deletedByGuid);
        outputMemento.TenantId.Should().Be(tenantGuid);
        outputMemento.Description.Should().Be("Full test");
    }
}

public class BackwardCompatibilityTests
{
    [Fact]
    public void PlainEntity_WithoutCrossCuttingInterfaces_Works()
    {
        var idGuid = Guid.NewGuid();
        var memento = new PlainMemento { Id = idGuid, Label = "Plain" };

        var entity = PlainEntity.Restore(memento);

        entity.Id.Value.Should().Be(idGuid);
        entity.Label.Should().Be("Plain");

        var outputMemento = new PlainMemento();
        entity.Snapshot(outputMemento);

        outputMemento.Id.Should().Be(idGuid);
        outputMemento.Label.Should().Be("Plain");
    }

    [Fact]
    public void PlainEntity_Hydrate_Works()
    {
        var entity = PlainEntity.Create(new TestOrderId(Guid.NewGuid()), "Original");
        var memento = new PlainMemento { Id = entity.Id.Value, Label = "Updated" };

        entity.Hydrate(memento);

        entity.Label.Should().Be("Updated");
    }
}

public class VersionInfoTests
{
    [Fact]
    public void IHasVersionInfo_VersionProperty_RoundTrips()
    {
        var version = Guid.NewGuid();
        var memento = new VersionedMemento { Version = version };

        memento.Version.Should().Be(version);
    }

    [Fact]
    public void IHasVersionHistory_IsVerifiable() =>
        (new FullyVersionedMemento() is IHasVersionHistory).Should().BeTrue();

    [Fact]
    public void IHasVersionHistory_IndependentFromIHasVersionInfo()
    {
        var historyOnly = new HistoryOnlyMemento();

        (historyOnly is IHasVersionHistory).Should().BeTrue();
        (historyOnly is IHasVersionInfo).Should().BeFalse();
    }

    private class VersionedMemento : IHasVersionInfo
    {
        public Guid Version { get; set; }
    }

    private class FullyVersionedMemento : IHasVersionInfo, IHasVersionHistory
    {
        public Guid Version { get; set; }
    }

    private class HistoryOnlyMemento : IHasVersionHistory;
}
