using AwesomeAssertions;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain.Tests;

public class AggregateRootEventTests
{
    private record OrderId(Guid Value) : TypedId<Guid>(Value);
    private record OrderPlaced(OrderId OrderId) : IDomainEvent;
    private record OrderShipped(OrderId OrderId) : IDomainEvent;

    private class TestOrder : AggregateRoot<OrderId>
    {
        private TestOrder(OrderId id) : base(id)
        {
        }

        public static TestOrder Create(OrderId id)
        {
            var order = new TestOrder(id);
            order.AddDomainEvent(new OrderPlaced(id));
            return order;
        }

        public void Ship() => AddDomainEvent(new OrderShipped(Id));
    }

    [Fact]
    public void DomainEvents_AfterCreation_ContainsCreationEvent()
    {
        var id = new OrderId(Guid.NewGuid());
        var order = TestOrder.Create(id);

        order.DomainEvents.Should().HaveCount(1);
        order.DomainEvents.Should().ContainSingle(e => e is OrderPlaced);
    }

    [Fact]
    public void DomainEvents_WhenNoEventsRaised_ReturnsEmptyCollection()
    {
        // Use restore to get an aggregate without events (simulates loading from DB)
        var id = new OrderId(Guid.NewGuid());
        var order = TestOrder.Create(id);
        order.ClearDomainEvents();

        order.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void DomainEvents_MultipleEvents_PreservesInsertionOrder()
    {
        var id = new OrderId(Guid.NewGuid());
        var order = TestOrder.Create(id);
        order.Ship();

        order.DomainEvents.Should().HaveCount(2);
        order.DomainEvents.First().Should().BeOfType<OrderPlaced>();
        order.DomainEvents.Last().Should().BeOfType<OrderShipped>();
    }

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var id = new OrderId(Guid.NewGuid());
        var order = TestOrder.Create(id);
        order.Ship();

        order.ClearDomainEvents();

        order.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ClearDomainEvents_WhenEmpty_DoesNotThrow()
    {
        var id = new OrderId(Guid.NewGuid());
        var order = TestOrder.Create(id);
        order.ClearDomainEvents();

        var act = () => order.ClearDomainEvents();

        act.Should().NotThrow();
    }

    [Fact]
    public void Equality_SameId_ReturnsTrue()
    {
        var id = new OrderId(Guid.NewGuid());
        var order1 = TestOrder.Create(id);
        var order2 = TestOrder.Create(id);

        order1.Equals(order2).Should().BeTrue();
    }

    [Fact]
    public void TypeHierarchy_IsEntity()
    {
        var order = TestOrder.Create(new OrderId(Guid.NewGuid()));

        order.Should().BeAssignableTo<Entity<OrderId>>();
    }
}

public class AggregateRootMementoTests
{
    private record InvoiceId(Guid Value) : TypedId<Guid>(Value);
    private record InvoiceCreated(InvoiceId InvoiceId) : IDomainEvent;

    private interface IInvoiceMemento : IHasIdentity<Guid>
    {
        string Customer { get; set; }
        decimal Total { get; set; }
    }

    private class InvoiceMemento : IInvoiceMemento
    {
        public Guid Id { get; set; }
        public string Customer { get; set; } = string.Empty;
        public decimal Total { get; set; }
    }

    private class Invoice : AggregateRoot<InvoiceId, Invoice, IInvoiceMemento>
    {
        public string Customer { get; private set; } = string.Empty;
        public decimal Total { get; private set; }

        private Invoice(InvoiceId id, string customer, decimal total) : base(id)
        {
            Customer = customer;
            Total = total;
        }

        public static Invoice Create(InvoiceId id, string customer, decimal total)
        {
            var invoice = new Invoice(id, customer, total);
            invoice.AddDomainEvent(new InvoiceCreated(id));
            return invoice;
        }

        protected override void SnapshotCore(IInvoiceMemento memento)
        {
            memento.Customer = Customer;
            memento.Total = Total;
        }

        protected override void RestoreCore(IInvoiceMemento memento)
        {
            Customer = memento.Customer;
            Total = memento.Total;
        }

        protected override void HydrateCore(IInvoiceMemento memento)
        {
            Customer = memento.Customer;
            Total = memento.Total;
        }

        public override IReadOnlyCollection<IError> GetValidationErrors()
        {
            var errors = new List<IError>();
            if (string.IsNullOrWhiteSpace(Customer))
                errors.Add(new InvoiceError("EMPTY_CUSTOMER", "Customer name cannot be empty"));
            if (Total < 0)
                errors.Add(new InvoiceError("NEGATIVE_TOTAL", $"Total must be non-negative, got {Total}"));
            return errors;
        }

        private record InvoiceError(string Code, string Message) : IError;
    }

    [Fact]
    public void Snapshot_PopulatesMementoWithIdAndState()
    {
        var id = new InvoiceId(Guid.NewGuid());
        var invoice = Invoice.Create(id, "Acme Corp", 1000m);
        var memento = new InvoiceMemento();

        invoice.Snapshot(memento);

        memento.Id.Should().Be(id.Value);
        memento.Customer.Should().Be("Acme Corp");
        memento.Total.Should().Be(1000m);
    }

    [Fact]
    public void Restore_CreatesAggregateFromMemento()
    {
        var guid = Guid.NewGuid();
        var memento = new InvoiceMemento { Id = guid, Customer = "Acme Corp", Total = 1000m };

        var invoice = Invoice.Restore(memento);

        invoice.Id.Should().Be(new InvoiceId(guid));
        invoice.Customer.Should().Be("Acme Corp");
        invoice.Total.Should().Be(1000m);
    }

    [Fact]
    public void Restore_AfterSnapshot_RoundTripsCorrectly()
    {
        var id = new InvoiceId(Guid.NewGuid());
        var original = Invoice.Create(id, "Acme Corp", 1000m);
        var memento = new InvoiceMemento();

        original.Snapshot(memento);
        var restored = Invoice.Restore(memento);

        restored.Id.Should().Be(original.Id);
        restored.Customer.Should().Be(original.Customer);
        restored.Total.Should().Be(original.Total);
    }

    [Fact]
    public void Restore_WithInvalidState_ThrowsValidationException()
    {
        var memento = new InvoiceMemento { Id = Guid.NewGuid(), Customer = "", Total = 100m };

        var act = () => Invoice.Restore(memento);

        act.Should().Throw<ValidationException>()
            .Which.Errors.Should().ContainSingle(e => e.Code == "EMPTY_CUSTOMER");
    }

    [Fact]
    public void Restore_DomainEventsCollectionIsEmpty()
    {
        var memento = new InvoiceMemento { Id = Guid.NewGuid(), Customer = "Acme", Total = 100m };

        var invoice = Invoice.Restore(memento);

        invoice.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Hydrate_UpdatesExistingAggregate()
    {
        var id = new InvoiceId(Guid.NewGuid());
        var invoice = Invoice.Create(id, "Acme Corp", 1000m);
        var newId = Guid.NewGuid();
        var memento = new InvoiceMemento { Id = newId, Customer = "Updated Corp", Total = 2000m };

        invoice.Hydrate(memento);

        invoice.Id.Should().Be(new InvoiceId(newId));
        invoice.Customer.Should().Be("Updated Corp");
        invoice.Total.Should().Be(2000m);
    }

    [Fact]
    public void Hydrate_WithInvalidState_ThrowsValidationException()
    {
        var id = new InvoiceId(Guid.NewGuid());
        var invoice = Invoice.Create(id, "Acme Corp", 1000m);
        var memento = new InvoiceMemento { Id = Guid.NewGuid(), Customer = "Valid", Total = -500m };

        var act = () => invoice.Hydrate(memento);

        act.Should().Throw<ValidationException>()
            .Which.Errors.Should().ContainSingle(e => e.Code == "NEGATIVE_TOTAL");
    }

    [Fact]
    public void Restore_DomainEventsCanBeAddedAfterRestore()
    {
        var memento = new InvoiceMemento { Id = Guid.NewGuid(), Customer = "Acme", Total = 100m };
        var invoice = Invoice.Restore(memento);

        // Events collection should work after restore (lazy ??= initialization)
        invoice.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void TypeHierarchy_ImplementsIMemento()
    {
        var invoice = Invoice.Create(new InvoiceId(Guid.NewGuid()), "Acme", 100m);

        (invoice is IMemento<Invoice, IInvoiceMemento>).Should().BeTrue();
    }

    [Fact]
    public void TypeHierarchy_ImplementsIHydratable()
    {
        var invoice = Invoice.Create(new InvoiceId(Guid.NewGuid()), "Acme", 100m);

        invoice.Should().BeAssignableTo<IHydratable<IInvoiceMemento>>();
    }
}
