using AwesomeAssertions;
using Yaf.Domain.Helpers;

namespace Yaf.Domain.Tests;

public class BoxingHelperTests
{
    [Fact]
    public void Unbox_NullInput_ReturnsNull() =>
        BoxingHelper.Unbox<Guid>(null).Should().BeNull();

    [Fact]
    public void Unbox_MatchingType_ReturnsValue()
    {
        var guid = Guid.NewGuid();
        BoxingHelper.Unbox<Guid>(guid).Should().Be(guid);
    }

    [Fact]
    public void Unbox_WrongType_ThrowsArgumentException()
    {
        var action = () => BoxingHelper.Unbox<Guid>("not a guid");
        action.Should().Throw<ArgumentException>()
            .WithMessage("Expected Guid, got String.*");
    }

    [Fact]
    public void Unbox_IntType_ReturnsValue() =>
        BoxingHelper.Unbox<int>(42).Should().Be(42);

    [Fact]
    public void Unbox_DateTimeOffset_ReturnsValue() =>
        BoxingHelper.Unbox<DateTimeOffset>(DateTimeOffset.UtcNow).Should().NotBeNull();
}
