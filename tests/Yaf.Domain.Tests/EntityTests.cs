using AwesomeAssertions;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain.Tests;

public class EntityEqualityTests
{
    private record TestEntityId(Guid Value) : TypedId<Guid>(Value);
    private record OtherEntityId(Guid Value) : TypedId<Guid>(Value);

    private class TestEntity : Entity<TestEntityId>
    {
        public string Name { get; private set; } = string.Empty;

        private TestEntity(TestEntityId id, string name) : base(id)
        {
            Name = name;
        }

        public static TestEntity Create(TestEntityId id, string name) => new(id, name);
    }

    private class OtherEntity : Entity<TestEntityId>
    {
        private OtherEntity(TestEntityId id) : base(id)
        {
        }

        public static OtherEntity Create(TestEntityId id) => new(id);
    }

    [Fact]
    public void Equality_SameTypeAndId_ReturnsTrue()
    {
        var id = new TestEntityId(Guid.NewGuid());
        var entity1 = TestEntity.Create(id, "Alice");
        var entity2 = TestEntity.Create(id, "Bob");

        entity1.Equals(entity2).Should().BeTrue();
        (entity1 == entity2).Should().BeTrue();
    }

    [Fact]
    public void Equality_SameTypeDifferentId_ReturnsFalse()
    {
        var entity1 = TestEntity.Create(new TestEntityId(Guid.NewGuid()), "Alice");
        var entity2 = TestEntity.Create(new TestEntityId(Guid.NewGuid()), "Alice");

        entity1.Equals(entity2).Should().BeFalse();
        (entity1 != entity2).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentTypeSameId_ReturnsFalse()
    {
        var id = new TestEntityId(Guid.NewGuid());
        var entity1 = TestEntity.Create(id, "Alice");
        var entity2 = OtherEntity.Create(id);

        entity1.Equals(entity2).Should().BeFalse();
    }

    [Fact]
    public void Equality_WithNull_ReturnsFalse()
    {
        var entity = TestEntity.Create(new TestEntityId(Guid.NewGuid()), "Alice");

        entity.Equals(null).Should().BeFalse();
        (entity == null).Should().BeFalse();
        (null == entity).Should().BeFalse();
    }

    [Fact]
    public void Equality_SameReference_ReturnsTrue()
    {
        var entity = TestEntity.Create(new TestEntityId(Guid.NewGuid()), "Alice");
        var same = entity;

        entity.Equals(same).Should().BeTrue();
        (entity == same).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_SameId_ReturnsSameHash()
    {
        var id = new TestEntityId(Guid.NewGuid());
        var entity1 = TestEntity.Create(id, "Alice");
        var entity2 = TestEntity.Create(id, "Bob");

        entity1.GetHashCode().Should().Be(entity2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentId_ReturnsDifferentHash()
    {
        var entity1 = TestEntity.Create(new TestEntityId(Guid.NewGuid()), "Alice");
        var entity2 = TestEntity.Create(new TestEntityId(Guid.NewGuid()), "Alice");

        entity1.GetHashCode().Should().NotBe(entity2.GetHashCode());
    }

    [Fact]
    public void Id_IsAccessible()
    {
        var id = new TestEntityId(Guid.NewGuid());
        var entity = TestEntity.Create(id, "Alice");

        entity.Id.Should().Be(id);
    }

    [Fact]
    public void TypeHierarchy_ImplementsIEquatable()
    {
        var entity = TestEntity.Create(new TestEntityId(Guid.NewGuid()), "Alice");

        entity.Should().BeAssignableTo<IEquatable<Entity<TestEntityId>>>();
    }
}

public class EntityMementoTests
{
    private record ProductId(Guid Value) : TypedId<Guid>(Value);

    private interface IProductMemento : IHasIdentity<Guid>
    {
        string Name { get; set; }
        decimal Price { get; set; }
    }

    private class ProductMemento : IProductMemento
    {
        public Guid? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    private class Product : Entity<ProductId, Product, IProductMemento>
    {
        public string Name { get; private set; } = string.Empty;
        public decimal Price { get; private set; }

        private Product(ProductId id, string name, decimal price) : base(id)
        {
            Name = name;
            Price = price;
        }

        public static Product Create(ProductId id, string name, decimal price) => new(id, name, price);

        protected override void SnapshotCore(IProductMemento memento)
        {
            memento.Name = Name;
            memento.Price = Price;
        }

        protected override void HydrateCore(IProductMemento memento)
        {
            Name = memento.Name;
            Price = memento.Price;
        }

        public override IReadOnlyCollection<IError> GetValidationErrors()
        {
            var errors = new List<IError>();
            if (string.IsNullOrWhiteSpace(Name))
                errors.Add(new ProductError("EMPTY_NAME", "Product name cannot be empty"));
            if (Price < 0)
                errors.Add(new ProductError("NEGATIVE_PRICE", $"Price must be non-negative, got {Price}"));
            return errors;
        }

        private record ProductError(string Code, string Message) : IError;
    }

    [Fact]
    public void Snapshot_PopulatesMementoWithIdAndState()
    {
        var id = new ProductId(Guid.NewGuid());
        var product = Product.Create(id, "Widget", 9.99m);
        var memento = new ProductMemento();

        product.Snapshot(memento);

        memento.Id.Should().Be(id.Value);
        memento.Name.Should().Be("Widget");
        memento.Price.Should().Be(9.99m);
    }

    [Fact]
    public void Restore_CreatesEntityFromMemento()
    {
        var guid = Guid.NewGuid();
        var memento = new ProductMemento { Id = guid, Name = "Gadget", Price = 19.99m };

        var product = Product.Restore(memento);

        product.Id.Should().Be(new ProductId(guid));
        product.Name.Should().Be("Gadget");
        product.Price.Should().Be(19.99m);
    }

    [Fact]
    public void Restore_AfterSnapshot_RoundTripsCorrectly()
    {
        var id = new ProductId(Guid.NewGuid());
        var original = Product.Create(id, "Widget", 9.99m);
        var memento = new ProductMemento();

        original.Snapshot(memento);
        var restored = Product.Restore(memento);

        restored.Id.Should().Be(original.Id);
        restored.Name.Should().Be(original.Name);
        restored.Price.Should().Be(original.Price);
    }

    [Fact]
    public void Restore_WithInvalidState_ThrowsValidationException()
    {
        var memento = new ProductMemento { Id = Guid.NewGuid(), Name = "", Price = 5.00m };

        var act = () => Product.Restore(memento);

        act.Should().Throw<ValidationException>()
            .Which.Errors.Should().ContainSingle(e => e.Code == "EMPTY_NAME");
    }

    [Fact]
    public void Hydrate_UpdatesExistingEntity()
    {
        var id = new ProductId(Guid.NewGuid());
        var product = Product.Create(id, "Widget", 9.99m);
        var newId = Guid.NewGuid();
        var memento = new ProductMemento { Id = newId, Name = "Updated", Price = 14.99m };

        product.Hydrate(memento);

        product.Id.Should().Be(new ProductId(newId));
        product.Name.Should().Be("Updated");
        product.Price.Should().Be(14.99m);
    }

    [Fact]
    public void Hydrate_WithInvalidState_ThrowsValidationException()
    {
        var id = new ProductId(Guid.NewGuid());
        var product = Product.Create(id, "Widget", 9.99m);
        var memento = new ProductMemento { Id = Guid.NewGuid(), Name = "Valid", Price = -5.00m };

        var act = () => product.Hydrate(memento);

        act.Should().Throw<ValidationException>()
            .Which.Errors.Should().ContainSingle(e => e.Code == "NEGATIVE_PRICE");
    }

    [Fact]
    public void TypeHierarchy_ImplementsIMemento()
    {
        var product = Product.Create(new ProductId(Guid.NewGuid()), "Widget", 9.99m);

        (product is IMemento<Product, IProductMemento>).Should().BeTrue();
    }

    [Fact]
    public void TypeHierarchy_ImplementsIHydratable()
    {
        var product = Product.Create(new ProductId(Guid.NewGuid()), "Widget", 9.99m);

        product.Should().BeAssignableTo<IHydratable<IProductMemento>>();
    }
}

public class EntityManualIdMementoTests
{
    private record ItemId(Guid Value) : TypedId<Guid>(Value);

    private interface IItemMemento
    {
        Guid? Id { get; set; }
        string Label { get; set; }
    }

    private class ItemMemento : IItemMemento
    {
        public Guid? Id { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    private class Item : Entity<ItemId, Item, IItemMemento>
    {
        public string Label { get; private set; } = string.Empty;

        private Item(ItemId id, string label) : base(id)
        {
            Label = label;
        }

        public static Item Create(ItemId id, string label) => new(id, label);

        protected override void SnapshotCore(IItemMemento memento)
        {
            memento.Id = Id.Value;
            memento.Label = Label;
        }

        protected override void HydrateCore(IItemMemento memento)
        {
            Id = new ItemId(memento.Id!.Value);
            Label = memento.Label;
        }

        public override IReadOnlyCollection<IError> GetValidationErrors()
        {
            var errors = new List<IError>();
            if (string.IsNullOrWhiteSpace(Label))
                errors.Add(new ItemError("EMPTY_LABEL", "Label cannot be empty"));
            return errors;
        }

        private record ItemError(string Code, string Message) : IError;
    }

    [Fact]
    public void Restore_WithoutIHasIdentity_ConsumerHandlesIdManually()
    {
        var guid = Guid.NewGuid();
        var memento = new ItemMemento { Id = guid, Label = "Manual" };

        var item = Item.Restore(memento);

        item.Id.Should().Be(new ItemId(guid));
        item.Label.Should().Be("Manual");
    }

    [Fact]
    public void Snapshot_WithoutIHasIdentity_ConsumerHandlesIdManually()
    {
        var id = new ItemId(Guid.NewGuid());
        var item = Item.Create(id, "Manual");
        var memento = new ItemMemento();

        item.Snapshot(memento);

        memento.Id.Should().Be(id.Value);
        memento.Label.Should().Be("Manual");
    }

    [Fact]
    public void Hydrate_WithoutIHasIdentity_ConsumerHandlesIdManually()
    {
        var item = Item.Create(new ItemId(Guid.NewGuid()), "Original");
        var newGuid = Guid.NewGuid();
        var memento = new ItemMemento { Id = newGuid, Label = "Updated" };

        item.Hydrate(memento);

        item.Id.Should().Be(new ItemId(newGuid));
        item.Label.Should().Be("Updated");
    }
}
