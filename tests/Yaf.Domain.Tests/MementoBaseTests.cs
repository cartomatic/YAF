using AwesomeAssertions;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain.Tests;

public class MementoBasePropertyTests
{
    [Fact]
    public void AllProperties_ReadableAndWritable()
    {
        var id = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var modifiedBy = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow.AddHours(-1);
        var modifiedAt = DateTimeOffset.UtcNow;
        var version = Guid.NewGuid();

        var memento = new TestProductMemento
        {
            Id = id,
            CreatedBy = createdBy,
            ModifiedBy = modifiedBy,
            CreatedAtUtc = createdAt,
            ModifiedAtUtc = modifiedAt,
            Version = version
        };

        memento.Id.Should().Be(id);
        memento.CreatedBy.Should().Be(createdBy);
        memento.ModifiedBy.Should().Be(modifiedBy);
        memento.CreatedAtUtc.Should().Be(createdAt);
        memento.ModifiedAtUtc.Should().Be(modifiedAt);
        memento.Version.Should().Be(version);
    }

    [Fact]
    public void DefaultValues_AreNullOrEmpty()
    {
        var memento = new TestProductMemento();

        memento.Id.Should().BeNull();
        memento.CreatedBy.Should().BeNull();
        memento.ModifiedBy.Should().BeNull();
        memento.CreatedAtUtc.Should().BeNull();
        memento.ModifiedAtUtc.Should().BeNull();
        memento.Version.Should().Be(Guid.Empty);
    }

    [Fact]
    public void ImplementsIMementoBase()
    {
        var memento = new TestProductMemento();

        (memento is IMementoBase).Should().BeTrue();
    }

    [Fact]
    public void ImplementsAllConstituentInterfaces()
    {
        var memento = new TestProductMemento();

        (memento is IHasIdentity).Should().BeTrue();
        (memento is IHasAccountability).Should().BeTrue();
        (memento is IHasTimestamps).Should().BeTrue();
        (memento is IHasVersionInfo).Should().BeTrue();
    }
}

public class TenantMementoBasePropertyTests
{
    [Fact]
    public void TenantId_ReadableAndWritable()
    {
        var tenantId = Guid.NewGuid();
        var memento = new TestTenantProductMemento { TenantId = tenantId };

        memento.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public void InheritsAllBaseProperties()
    {
        var id = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var version = Guid.NewGuid();

        var memento = new TestTenantProductMemento
        {
            Id = id,
            CreatedBy = createdBy,
            Version = version,
            TenantId = Guid.NewGuid()
        };

        memento.Id.Should().Be(id);
        memento.CreatedBy.Should().Be(createdBy);
        memento.Version.Should().Be(version);
    }

    [Fact]
    public void ImplementsITenantMementoBase()
    {
        var memento = new TestTenantProductMemento();

        (memento is ITenantMementoBase).Should().BeTrue();
    }

    [Fact]
    public void ImplementsAllConstituentInterfaces()
    {
        var memento = new TestTenantProductMemento();

        (memento is IMementoBase).Should().BeTrue();
        (memento is IHasIdentity).Should().BeTrue();
        (memento is IHasAccountability).Should().BeTrue();
        (memento is IHasTimestamps).Should().BeTrue();
        (memento is IHasVersionInfo).Should().BeTrue();
        (memento is IHasTenantId).Should().BeTrue();
    }

    [Fact]
    public void DefaultTenantId_IsNull()
    {
        var memento = new TestTenantProductMemento();

        memento.TenantId.Should().BeNull();
    }
}

public class MementoBaseRoundTripTests
{
    [Fact]
    public void Entity_WithMementoBaseDerived_RoundTrips()
    {
        var id = new TestProductId(Guid.NewGuid());
        var product = TestProduct.Create(id, "Widget");
        var memento = new TestProductMemento();

        product.Snapshot(memento);

        memento.Id.Should().Be(id.Value);
        memento.Name.Should().Be("Widget");

        var restored = TestProduct.Restore(memento);

        restored.Id.Should().Be(id);
        restored.Name.Should().Be("Widget");
    }

    [Fact]
    public void Entity_WithMementoBase_CrossCuttingAutoMapped()
    {
        var createdBy = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow.AddHours(-1);
        var memento = new TestProductMemento
        {
            Id = Guid.NewGuid(),
            CreatedBy = createdBy,
            CreatedAtUtc = createdAt,
            Name = "Widget"
        };

        var product = TestProduct.Restore(memento);

        product.CreatedBy!.Value.Should().Be(createdBy);
        product.CreatedAtUtc.Should().Be(createdAt);

        var outputMemento = new TestProductMemento();
        product.Snapshot(outputMemento);

        outputMemento.CreatedBy.Should().Be(createdBy);
        outputMemento.CreatedAtUtc.Should().Be(createdAt);
    }

    [Fact]
    public void TenantEntity_WithTenantMementoBaseDerived_RoundTrips()
    {
        var id = new TestProductId(Guid.NewGuid());
        var tenantId = new TenantId(Guid.NewGuid());
        var product = TestTenantProduct.Create(id, "Gadget", tenantId);
        var memento = new TestTenantProductMemento();

        product.Snapshot(memento);

        memento.Id.Should().Be(id.Value);
        memento.Name.Should().Be("Gadget");
        memento.TenantId.Should().Be(tenantId.Value);

        var restored = TestTenantProduct.Restore(memento);

        restored.Id.Should().Be(id);
        restored.Name.Should().Be("Gadget");
        restored.TenantId.Should().Be(tenantId);
    }
}

#region Test Types

internal record TestProductId(Guid Value) : TypedId(Value);

internal class TestProductMemento : MementoBase
{
    public string Name { get; set; } = string.Empty;
}

internal class TestTenantProductMemento : TenantMementoBase
{
    public string Name { get; set; } = string.Empty;
}

internal class TestProduct :
    Entity<TestProductId, TestProduct, TestProductMemento>,
    IAccountable, IAccountableWriter,
    ITimestamped, ITimestampedWriter
{
    public string Name { get; private set; } = string.Empty;
    public ActorId? CreatedBy { get; set; }
    public ActorId? ModifiedBy { get; set; }
    public DateTimeOffset? CreatedAtUtc { get; set; }
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    private TestProduct(TestProductId id, string name) : base(id) { Name = name; }

    private TestProduct() { }

    public static TestProduct Create(TestProductId id, string name) => new(id, name);

    protected override void SnapshotCore(TestProductMemento memento) =>
        memento.Name = Name;

    protected override void HydrateCore(TestProductMemento memento) =>
        Name = memento.Name;

    public override IReadOnlyCollection<IError> GetValidationErrors() =>
        string.IsNullOrWhiteSpace(Name)
            ? [new Error("EMPTY_NAME", "Name cannot be empty.")]
            : [];
}

internal class TestTenantProduct :
    Entity<TestProductId, TestTenantProduct, TestTenantProductMemento>,
    IAccountable, IAccountableWriter,
    ITimestamped, ITimestampedWriter,
    ITenantScoped
{
    public string Name { get; private set; } = string.Empty;
    public ActorId? CreatedBy { get; set; }
    public ActorId? ModifiedBy { get; set; }
    public DateTimeOffset? CreatedAtUtc { get; set; }
    public DateTimeOffset? ModifiedAtUtc { get; set; }
    public TenantId TenantId { get; private set; } = null!;

    private TestTenantProduct(TestProductId id, string name, TenantId tenantId) : base(id)
    {
        Name = name;
        TenantId = tenantId;
    }

    private TestTenantProduct() { }

    public static TestTenantProduct Create(TestProductId id, string name, TenantId tenantId) =>
        new(id, name, tenantId);

    protected override void SnapshotCore(TestTenantProductMemento memento)
    {
        memento.Name = Name;
        memento.TenantId = TenantId?.Value;
    }

    protected override void HydrateCore(TestTenantProductMemento memento)
    {
        Name = memento.Name;
        TenantId = memento.TenantId is { } t ? new TenantId(t) : null!;
    }

    public override IReadOnlyCollection<IError> GetValidationErrors() =>
        string.IsNullOrWhiteSpace(Name)
            ? [new Error("EMPTY_NAME", "Name cannot be empty.")]
            : [];
}

#endregion
