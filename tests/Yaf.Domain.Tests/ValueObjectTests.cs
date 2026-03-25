using Yaf.Domain.Interfaces;

namespace Yaf.Domain.Tests;

#region Test fixtures

public record SimpleColor(int R, int G, int B) : ValueObject;

public class ColorMemento
{
    public int R { get; set; }
    public int G { get; set; }
    public int B { get; set; }
}

public record MementoColor : ValueObject<MementoColor, ColorMemento>, IMemento<MementoColor, ColorMemento>
{
    public int R { get; init; }
    public int G { get; init; }
    public int B { get; init; }

    private MementoColor(int r, int g, int b)
    {
        R = r;
        G = g;
        B = b;
    }

    public static MementoColor Create(int r, int g, int b) => new(r, g, b);

    public override void Snapshot(ColorMemento memento)
    {
        memento.R = R;
        memento.G = G;
        memento.B = B;
    }

    public static MementoColor Restore(ColorMemento memento) => new(memento.R, memento.G, memento.B);
}

#endregion

public class ValueObjectMarkerTests
{
    [Fact]
    public void SimpleValueObject_WithSameValues_AreEqual()
    {
        var color1 = new SimpleColor(255, 0, 0);
        var color2 = new SimpleColor(255, 0, 0);

        Assert.Equal(color1, color2);
    }

    [Fact]
    public void SimpleValueObject_WithDifferentValues_AreNotEqual()
    {
        var red = new SimpleColor(255, 0, 0);
        var blue = new SimpleColor(0, 0, 255);

        Assert.NotEqual(red, blue);
    }

    [Fact]
    public void SimpleValueObject_IsValueObject()
    {
        var color = new SimpleColor(255, 0, 0);

        Assert.IsAssignableFrom<ValueObject>(color);
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

        Assert.Equal(10, memento.R);
        Assert.Equal(20, memento.G);
        Assert.Equal(30, memento.B);
    }

    [Fact]
    public void MementoValueObject_DoesNotImplementIHydrateable()
    {
        var color = MementoColor.Create(1, 2, 3);

        Assert.IsNotAssignableFrom<IHydrateable<ColorMemento>>(color);
    }

    [Fact]
    public void MementoValueObject_Restore_CreatesEquivalentObject()
    {
        var memento = new ColorMemento { R = 10, G = 20, B = 30 };

        var color = MementoColor.Restore(memento);

        Assert.Equal(10, color.R);
        Assert.Equal(20, color.G);
        Assert.Equal(30, color.B);
    }

    [Fact]
    public void MementoValueObject_SnapshotThenRestore_RoundTrips()
    {
        var original = MementoColor.Create(100, 150, 200);
        var memento = new ColorMemento();

        original.Snapshot(memento);
        var restored = MementoColor.Restore(memento);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void MementoValueObject_Restore_CanBeCalledGenerically()
    {
        var memento = new ColorMemento { R = 42, G = 43, B = 44 };

        var restored = RestoreGeneric<MementoColor, ColorMemento>(memento);

        Assert.Equal(42, restored.R);
        Assert.Equal(43, restored.G);
        Assert.Equal(44, restored.B);
    }

    private static TSelf RestoreGeneric<TSelf, TMemento>(TMemento memento)
        where TSelf : IMemento<TSelf, TMemento>
        where TMemento : class
    {
        return TSelf.Restore(memento);
    }
}
