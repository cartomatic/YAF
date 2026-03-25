using AwesomeAssertions;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain.Tests;

#region Test fixtures

public record SimpleColor(int R, int G, int B) : ValueObject;

// Domain-level memento contract (interface — domain defines the shape)
public interface IColorMemento
{
    int R { get; set; }
    int G { get; set; }
    int B { get; set; }
}

// Infrastructure-level concrete memento
public class ColorMemento : IColorMemento
{
    public int R { get; set; }
    public int G { get; set; }
    public int B { get; set; }
}

// Domain-level value object with memento support
public record MementoColor : ValueObject<MementoColor, IColorMemento>, IMemento<MementoColor, IColorMemento>
{
    public int R { get; private set; }
    public int G { get; private set; }
    public int B { get; private set; }

    private MementoColor(int r, int g, int b)
    {
        R = r;
        G = g;
        B = b;
    }

    public static MementoColor Create(int r, int g, int b) => new(r, g, b);

    protected override void SnapshotInternal(IColorMemento memento)
    {
        memento.R = R;
        memento.G = G;
        memento.B = B;
    }

    protected override void RestoreInternal(IColorMemento memento)
    {
        R = memento.R;
        G = memento.G;
        B = memento.B;
    }
}

#endregion

public class ValueObjectMarkerTests
{
    [Fact]
    public void SimpleValueObject_WithSameValues_AreEqual()
    {
        var color1 = new SimpleColor(255, 0, 0);
        var color2 = new SimpleColor(255, 0, 0);

        color1.Should().Be(color2);
    }

    [Fact]
    public void SimpleValueObject_WithDifferentValues_AreNotEqual()
    {
        var red = new SimpleColor(255, 0, 0);
        var blue = new SimpleColor(0, 0, 255);

        red.Should().NotBe(blue);
    }

    [Fact]
    public void SimpleValueObject_IsValueObject()
    {
        var color = new SimpleColor(255, 0, 0);

        color.Should().BeAssignableTo<ValueObject>();
    }
}

public class ValueObjectMementoTests
{
    [Fact]
    public void MementoValueObject_Snapshot_PopulatesMemento()
    {
        var color = MementoColor.Create(10, 20, 30);
        var memento = new ColorMemento();

        color.Snapshot(memento);

        memento.R.Should().Be(10);
        memento.G.Should().Be(20);
        memento.B.Should().Be(30);
    }

    [Fact]
    public void MementoValueObject_DoesNotImplementIHydrateable()
    {
        var color = MementoColor.Create(1, 2, 3);

        color.Should().NotBeAssignableTo<IHydrateable<IColorMemento>>();
    }

    [Fact]
    public void MementoValueObject_ImplementsIMemento()
    {
        var color = MementoColor.Create(1, 2, 3);

        (color is IMemento<MementoColor, IColorMemento>).Should().BeTrue();
    }

    [Fact]
    public void MementoValueObject_Restore_CreatesEquivalentObject()
    {
        var memento = new ColorMemento { R = 10, G = 20, B = 30 };

        var color = MementoColor.Restore(memento);

        color.R.Should().Be(10);
        color.G.Should().Be(20);
        color.B.Should().Be(30);
    }

    [Fact]
    public void MementoValueObject_SnapshotThenRestore_RoundTrips()
    {
        var original = MementoColor.Create(100, 150, 200);
        var memento = new ColorMemento();

        original.Snapshot(memento);
        var restored = MementoColor.Restore(memento);

        restored.Should().Be(original);
    }

    [Fact]
    public void MementoValueObject_Restore_CanBeCalledViaConcreteType()
    {
        var memento = new ColorMemento { R = 42, G = 43, B = 44 };

        var restored = MementoColor.Restore(memento);

        restored.R.Should().Be(42);
        restored.G.Should().Be(43);
        restored.B.Should().Be(44);
    }
}
