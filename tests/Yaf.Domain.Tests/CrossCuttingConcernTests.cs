using AwesomeAssertions;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain.Tests;

#region Test Types

internal record UserId(Guid Value) : TypedId<Guid>(Value);

internal record OrderId(Guid Value) : TypedId<Guid>(Value);

// --- Full entity: all cross-cutting interfaces ---

internal interface IOrderMemento :
    IHasIdentity<Guid>,
    IHasAccountability<Guid>,
    IHasTimestamps,
    IHasSoftDelete<Guid>,
    IHasTenantId<Guid>
{
    string Description { get; set; }
}

internal class OrderMemento : IOrderMemento
{
    public Guid Id { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid ModifiedBy { get; set; }
    public DateTimeOffset? CreatedAtUtc { get; set; }
    public DateTimeOffset? ModifiedAtUtc { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public Guid DeletedBy { get; set; }
    public Guid TenantId { get; set; }
    public string Description { get; set; } = string.Empty;
}

internal class Order :
    Entity<OrderId, Order, IOrderMemento>,
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

    private Order(OrderId id, string description, TenantId tenantId) : base(id)
    {
        Description = description;
        TenantId = tenantId;
    }

    public static Order Create(OrderId id, string description, TenantId tenantId) =>
        new(id, description, tenantId);

    protected override void SnapshotCore(IOrderMemento memento) =>
        memento.Description = Description;

    protected override void RestoreCore(IOrderMemento memento) =>
        Description = memento.Description;

    protected override void HydrateCore(IOrderMemento memento) =>
        Description = memento.Description;

    public override IReadOnlyCollection<IError> GetValidationErrors() =>
        string.IsNullOrWhiteSpace(Description)
            ? [new OrderError("EMPTY_DESC", "Description cannot be empty")]
            : [];

    private record OrderError(string Code, string Message) : IError;
}

// --- Timestamps-only entity ---

internal interface ISimpleMemento : IHasIdentity<Guid>, IHasTimestamps
{
    string Value { get; set; }
}

internal class SimpleMemento : ISimpleMemento
{
    public Guid Id { get; set; }
    public DateTimeOffset? CreatedAtUtc { get; set; }
    public DateTimeOffset? ModifiedAtUtc { get; set; }
    public string Value { get; set; } = string.Empty;
}

internal class SimpleEntity :
    Entity<OrderId, SimpleEntity, ISimpleMemento>,
    ITimestamped
{
    public string Value { get; private set; } = string.Empty;
    public DateTimeOffset? CreatedAtUtc { get; private set; }
    public DateTimeOffset? ModifiedAtUtc { get; private set; }

    private SimpleEntity(OrderId id, string value) : base(id) { Value = value; }

    public static SimpleEntity Create(OrderId id, string value) => new(id, value);

    protected override void SnapshotCore(ISimpleMemento memento) => memento.Value = Value;
    protected override void RestoreCore(ISimpleMemento memento) => Value = memento.Value;
    protected override void HydrateCore(ISimpleMemento memento) => Value = memento.Value;
    public override IReadOnlyCollection<IError> GetValidationErrors() => [];
}

// --- Soft-deletable standalone (no ITimestamped, no IAccountable) ---

internal interface IStandaloneDeleteMemento : IHasIdentity<Guid>, IHasSoftDelete<Guid>
{
    string Name { get; set; }
}

internal class StandaloneDeleteMemento : IStandaloneDeleteMemento
{
    public Guid Id { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public Guid DeletedBy { get; set; }
    public string Name { get; set; } = string.Empty;
}

internal class StandaloneDeleteEntity :
    Entity<OrderId, StandaloneDeleteEntity, IStandaloneDeleteMemento>,
    ISoftDeletable<UserId>
{
    public string Name { get; private set; } = string.Empty;
    public DateTimeOffset? DeletedAtUtc { get; private set; }
    public UserId? DeletedBy { get; private set; }

    private StandaloneDeleteEntity(OrderId id, string name) : base(id) { Name = name; }

    public static StandaloneDeleteEntity Create(OrderId id, string name) => new(id, name);

    protected override void SnapshotCore(IStandaloneDeleteMemento memento) => memento.Name = Name;
    protected override void RestoreCore(IStandaloneDeleteMemento memento) => Name = memento.Name;
    protected override void HydrateCore(IStandaloneDeleteMemento memento) => Name = memento.Name;
    public override IReadOnlyCollection<IError> GetValidationErrors() => [];
}

// --- Entity without cross-cutting interfaces (backward compatibility) ---

internal interface IPlainMemento : IHasIdentity<Guid>
{
    string Label { get; set; }
}

internal class PlainMemento : IPlainMemento
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
}

internal class PlainEntity : Entity<OrderId, PlainEntity, IPlainMemento>
{
    public string Label { get; private set; } = string.Empty;

    private PlainEntity(OrderId id, string label) : base(id) { Label = label; }

    public static PlainEntity Create(OrderId id, string label) => new(id, label);

    protected override void SnapshotCore(IPlainMemento memento) => memento.Label = Label;
    protected override void RestoreCore(IPlainMemento memento) => Label = memento.Label;
    protected override void HydrateCore(IPlainMemento memento) => Label = memento.Label;
    public override IReadOnlyCollection<IError> GetValidationErrors() => [];
}

// --- Entity with domain interfaces but memento without matching memento interfaces (graceful skip) ---

internal interface IMismatchMemento : IHasIdentity<Guid>
{
    string Data { get; set; }
}

internal class MismatchMemento : IMismatchMemento
{
    public Guid Id { get; set; }
    public string Data { get; set; } = string.Empty;
}

internal class MismatchEntity :
    Entity<OrderId, MismatchEntity, IMismatchMemento>,
    IAccountable<UserId>,
    ITimestamped
{
    public string Data { get; private set; } = string.Empty;
    public UserId? CreatedBy { get; private set; }
    public UserId? ModifiedBy { get; private set; }
    public DateTimeOffset? CreatedAtUtc { get; private set; }
    public DateTimeOffset? ModifiedAtUtc { get; private set; }

    private MismatchEntity(OrderId id, string data) : base(id) { Data = data; }

    public static MismatchEntity Create(OrderId id, string data) => new(id, data);

    protected override void SnapshotCore(IMismatchMemento memento) => memento.Data = Data;
    protected override void RestoreCore(IMismatchMemento memento) => Data = memento.Data;
    protected override void HydrateCore(IMismatchMemento memento) => Data = memento.Data;
    public override IReadOnlyCollection<IError> GetValidationErrors() => [];
}

#endregion

public class AccountabilityTests
{
    [Fact]
    public void Snapshot_WritesCreatedByAndModifiedBy_ToMemento()
    {
        var userId = new UserId(Guid.NewGuid());
        var tenantId = new TenantId(Guid.NewGuid());
        var order = CreateOrderWithAccountability(userId, tenantId);
        var memento = new OrderMemento();

        order.Snapshot(memento);

        memento.CreatedBy.Should().Be(userId.Value);
        memento.ModifiedBy.Should().Be(default(Guid));
    }

    [Fact]
    public void Restore_ReconstructsTypedActorIds()
    {
        var createdByGuid = Guid.NewGuid();
        var modifiedByGuid = Guid.NewGuid();
        var memento = new OrderMemento
        {
            Id = Guid.NewGuid(),
            CreatedBy = createdByGuid,
            ModifiedBy = modifiedByGuid,
            TenantId = Guid.NewGuid(),
            Description = "Test"
        };

        var order = Order.Restore(memento);

        order.CreatedBy.Should().NotBeNull();
        order.CreatedBy!.Value.Should().Be(createdByGuid);
        order.ModifiedBy.Should().NotBeNull();
        order.ModifiedBy!.Value.Should().Be(modifiedByGuid);
    }

    [Fact]
    public void Restore_DefaultCreatedBy_RemainsNull()
    {
        var memento = new OrderMemento
        {
            Id = Guid.NewGuid(),
            CreatedBy = default,
            TenantId = Guid.NewGuid(),
            Description = "Test"
        };

        var order = Order.Restore(memento);

        order.CreatedBy.Should().BeNull();
        order.ModifiedBy.Should().BeNull();
    }

    [Fact]
    public void Hydrate_UpdatesAccountabilityFields()
    {
        var tenantId = new TenantId(Guid.NewGuid());
        var order = CreateOrderWithAccountability(new UserId(Guid.NewGuid()), tenantId);
        var newCreatedBy = Guid.NewGuid();
        var newModifiedBy = Guid.NewGuid();
        var memento = new OrderMemento
        {
            Id = order.Id.Value,
            CreatedBy = newCreatedBy,
            ModifiedBy = newModifiedBy,
            TenantId = tenantId.Value,
            Description = "Updated"
        };

        order.Hydrate(memento);

        order.CreatedBy!.Value.Should().Be(newCreatedBy);
        order.ModifiedBy!.Value.Should().Be(newModifiedBy);
    }

    private static Order CreateOrderWithAccountability(UserId userId, TenantId tenantId)
    {
        var order = Order.Create(new OrderId(Guid.NewGuid()), "Test", tenantId);
        // Simulate infrastructure setting CreatedBy via memento round-trip
        var memento = new OrderMemento();
        order.Snapshot(memento);
        memento.CreatedBy = userId.Value;
        order.Hydrate(memento);
        return order;
    }
}

public class TimestampTests
{
    [Fact]
    public void Snapshot_WritesTimestamps_ToMemento()
    {
        var now = DateTimeOffset.UtcNow;
        var memento = new SimpleMemento
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = now,
            ModifiedAtUtc = now.AddMinutes(5),
            Value = "Test"
        };

        var entity = SimpleEntity.Restore(memento);
        var outputMemento = new SimpleMemento();
        entity.Snapshot(outputMemento);

        outputMemento.CreatedAtUtc.Should().Be(now);
        outputMemento.ModifiedAtUtc.Should().Be(now.AddMinutes(5));
    }

    [Fact]
    public void Restore_NullTimestamps_StayNull()
    {
        var memento = new SimpleMemento
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = null,
            ModifiedAtUtc = null,
            Value = "Test"
        };

        var entity = SimpleEntity.Restore(memento);

        entity.CreatedAtUtc.Should().BeNull();
        entity.ModifiedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Restore_PopulatesTimestamps()
    {
        var now = DateTimeOffset.UtcNow;
        var memento = new SimpleMemento
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = now,
            ModifiedAtUtc = now.AddHours(1),
            Value = "Test"
        };

        var entity = SimpleEntity.Restore(memento);

        entity.CreatedAtUtc.Should().Be(now);
        entity.ModifiedAtUtc.Should().Be(now.AddHours(1));
    }

    [Fact]
    public void NewEntity_HasNullTimestamps()
    {
        var entity = SimpleEntity.Create(new OrderId(Guid.NewGuid()), "Test");

        entity.CreatedAtUtc.Should().BeNull();
        entity.ModifiedAtUtc.Should().BeNull();
    }
}

public class SoftDeleteTests
{
    [Fact]
    public void Snapshot_WritesSoftDeleteFields_ToMemento()
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
        var outputMemento = new OrderMemento();
        order.Snapshot(outputMemento);

        outputMemento.DeletedAtUtc.Should().Be(deletedAtUtc);
        outputMemento.DeletedBy.Should().Be(deletedByGuid);
    }

    [Fact]
    public void Restore_NotDeleted_NullFields()
    {
        var memento = new OrderMemento
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Description = "Test",
            DeletedAtUtc = null,
            DeletedBy = default
        };

        var order = Order.Restore(memento);

        order.DeletedAtUtc.Should().BeNull();
        order.DeletedBy.Should().BeNull();
    }

    [Fact]
    public void Restore_SoftDeleted_PopulatesFields()
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
        order.DeletedBy.Should().NotBeNull();
        order.DeletedBy!.Value.Should().Be(deletedByGuid);
    }

    [Fact]
    public void Standalone_SoftDeletable_WorksWithoutTimestampedOrAccountable()
    {
        var deletedAtUtc = DateTimeOffset.UtcNow;
        var deletedByGuid = Guid.NewGuid();
        var memento = new StandaloneDeleteMemento
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            DeletedAtUtc = deletedAtUtc,
            DeletedBy = deletedByGuid
        };

        var entity = StandaloneDeleteEntity.Restore(memento);

        entity.DeletedAtUtc.Should().Be(deletedAtUtc);
        entity.DeletedBy!.Value.Should().Be(deletedByGuid);
        entity.Name.Should().Be("Test");
    }

    [Fact]
    public void Standalone_SoftDeletable_RoundTrips()
    {
        var deletedAtUtc = DateTimeOffset.UtcNow;
        var deletedByGuid = Guid.NewGuid();
        var memento = new StandaloneDeleteMemento
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            DeletedAtUtc = deletedAtUtc,
            DeletedBy = deletedByGuid
        };

        var entity = StandaloneDeleteEntity.Restore(memento);
        var outputMemento = new StandaloneDeleteMemento();
        entity.Snapshot(outputMemento);

        outputMemento.DeletedAtUtc.Should().Be(deletedAtUtc);
        outputMemento.DeletedBy.Should().Be(deletedByGuid);
    }
}

public class TenantScopedTests
{
    [Fact]
    public void Snapshot_WritesTenantId_ToMemento()
    {
        var tenantGuid = Guid.NewGuid();
        var memento = new OrderMemento
        {
            Id = Guid.NewGuid(),
            TenantId = tenantGuid,
            Description = "Test"
        };

        var order = Order.Restore(memento);
        var outputMemento = new OrderMemento();
        order.Snapshot(outputMemento);

        outputMemento.TenantId.Should().Be(tenantGuid);
    }

    [Fact]
    public void Restore_ReconstructsTenantId()
    {
        var tenantGuid = Guid.NewGuid();
        var memento = new OrderMemento
        {
            Id = Guid.NewGuid(),
            TenantId = tenantGuid,
            Description = "Test"
        };

        var order = Order.Restore(memento);

        order.TenantId.Should().NotBeNull();
        order.TenantId.Value.Should().Be(tenantGuid);
    }

    [Fact]
    public void RoundTrip_PreservesTenantId()
    {
        var tenantGuid = Guid.NewGuid();
        var order = Order.Create(new OrderId(Guid.NewGuid()), "Test", new TenantId(tenantGuid));
        var memento = new OrderMemento();

        order.Snapshot(memento);

        memento.TenantId.Should().Be(tenantGuid);

        var restored = Order.Restore(memento);
        restored.TenantId.Value.Should().Be(tenantGuid);
    }
}

public class AllCombinedTests
{
    [Fact]
    public void FullRoundTrip_AllInterfaces()
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

        // Restore
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

        // Snapshot back
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

public class GracefulDegradationTests
{
    [Fact]
    public void EntityWithInterfaces_MementoWithout_SilentlySkips()
    {
        var memento = new MismatchMemento
        {
            Id = Guid.NewGuid(),
            Data = "Test"
        };

        // Should not throw — entity has IAccountable/ITimestamped but memento lacks IHasAccountability/IHasTimestamps
        var entity = MismatchEntity.Restore(memento);

        entity.Data.Should().Be("Test");
        entity.CreatedBy.Should().BeNull();
        entity.ModifiedBy.Should().BeNull();
        entity.CreatedAtUtc.Should().BeNull();
        entity.ModifiedAtUtc.Should().BeNull();
    }

    [Fact]
    public void EntityWithInterfaces_MementoWithout_Snapshot_SilentlySkips()
    {
        var entity = MismatchEntity.Create(new OrderId(Guid.NewGuid()), "Test");
        var memento = new MismatchMemento();

        // Should not throw
        entity.Snapshot(memento);

        memento.Data.Should().Be("Test");
    }
}

public class BackwardCompatibilityTests
{
    [Fact]
    public void PlainEntity_WithoutCrossCuttingInterfaces_WorksIdentically()
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
    public void PlainEntity_Hydrate_WorksIdentically()
    {
        var entity = PlainEntity.Create(new OrderId(Guid.NewGuid()), "Original");
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
    public void IHasVersionHistory_IsVerifiable()
    {
        var memento = new FullyVersionedMemento();

        (memento is IHasVersionHistory).Should().BeTrue();
        (memento is IHasVersionInfo).Should().BeTrue();
    }

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
