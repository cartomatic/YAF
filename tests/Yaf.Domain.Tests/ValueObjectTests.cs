using AwesomeAssertions;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain.Tests;

public class ValueObjectMarkerTests
{
    private record SimpleColor(int R, int G, int B) : ValueObject;

    [Fact]
    public void Equality_WithSameValues_ReturnsTrue()
    {
        var color1 = new SimpleColor(255, 0, 0);
        var color2 = new SimpleColor(255, 0, 0);

        color1.Should().Be(color2);
    }

    [Fact]
    public void Equality_WithDifferentValues_ReturnsFalse()
    {
        var red = new SimpleColor(255, 0, 0);
        var blue = new SimpleColor(0, 0, 255);

        red.Should().NotBe(blue);
    }

    [Fact]
    public void TypeHierarchy_WhenInherited_IsAssignableToValueObject()
    {
        var color = new SimpleColor(255, 0, 0);

        color.Should().BeAssignableTo<ValueObject>();
    }
}

public class ValueObjectMementoTests
{
    private interface IColorMemento
    {
        int R { get; set; }
        int G { get; set; }
        int B { get; set; }
    }

    private class ColorMemento : IColorMemento
    {
        public int R { get; set; }
        public int G { get; set; }
        public int B { get; set; }
    }

    private record MementoColor : ValueObject<MementoColor, IColorMemento>
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

        protected override void SnapshotCore(IColorMemento memento)
        {
            memento.R = R;
            memento.G = G;
            memento.B = B;
        }

        protected override void HydrateCore(IColorMemento memento)
        {
            R = memento.R;
            G = memento.G;
            B = memento.B;
        }

        public override IReadOnlyCollection<Error> GetValidationErrors()
        {
            var errors = new List<Error>();

            if (R < 0 || R > 255)
                errors.Add(new Error("INVALID_R", $"R must be 0-255, got {R}"));
            if (G < 0 || G > 255)
                errors.Add(new Error("INVALID_G", $"G must be 0-255, got {G}"));
            if (B < 0 || B > 255)
                errors.Add(new Error("INVALID_B", $"B must be 0-255, got {B}"));

            return errors;
        }


    }

    [Fact]
    public void Snapshot_WithValidState_PopulatesMemento()
    {
        var color = MementoColor.Create(10, 20, 30);
        var memento = new ColorMemento();

        color.Snapshot(memento);

        memento.R.Should().Be(10);
        memento.G.Should().Be(20);
        memento.B.Should().Be(30);
    }

    [Fact]
    public void TypeHierarchy_AsValueObject_DoesNotImplementIHydratable()
    {
        var color = MementoColor.Create(1, 2, 3);

        color.Should().NotBeAssignableTo<IHydratable<IColorMemento>>();
    }

    [Fact]
    public void TypeHierarchy_AsValueObject_ImplementsIMemento()
    {
        var color = MementoColor.Create(1, 2, 3);

        (color is IMemento<MementoColor, IColorMemento>).Should().BeTrue();
    }

    [Fact]
    public void Restore_WithValidMemento_CreatesEquivalentObject()
    {
        var memento = new ColorMemento { R = 10, G = 20, B = 30 };

        var color = MementoColor.Restore(memento);

        color.R.Should().Be(10);
        color.G.Should().Be(20);
        color.B.Should().Be(30);
    }

    [Fact]
    public void Restore_AfterSnapshot_RoundTripsCorrectly()
    {
        var original = MementoColor.Create(100, 150, 200);
        var memento = new ColorMemento();

        original.Snapshot(memento);
        var restored = MementoColor.Restore(memento);

        restored.Should().Be(original);
    }

    [Fact]
    public void Restore_WithInvalidMemento_ThrowsValidationException()
    {
        var memento = new ColorMemento { R = 999, G = 20, B = 30 };

        var act = () => MementoColor.Restore(memento);

        act.Should().Throw<ValidationException>()
            .Which.Errors.Should().ContainSingle(e => e.Code == "INVALID_R");
    }
}
