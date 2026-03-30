using AwesomeAssertions;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain.Tests;

public class ErrorConstructionTests
{
    [Fact]
    public void ValidCodeAndMessage_CreatesError()
    {
        var error = new Error("ERR_001", "Something went wrong.");

        error.Code.Should().Be("ERR_001");
        error.Message.Should().Be("Something went wrong.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void InvalidCode_ThrowsArgumentException(string? code)
    {
        var act = () => new Error(code!, "Valid message");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void InvalidMessage_ThrowsArgumentException(string? message)
    {
        var act = () => new Error("VALID_CODE", message!);

        act.Should().Throw<ArgumentException>();
    }
}

public class ErrorEqualityTests
{
    [Fact]
    public void SameCodeAndMessage_AreEqual()
    {
        var error1 = new Error("ERR_001", "Something went wrong.");
        var error2 = new Error("ERR_001", "Something went wrong.");

        error1.Should().Be(error2);
        (error1 == error2).Should().BeTrue();
    }

    [Fact]
    public void DifferentCode_AreNotEqual()
    {
        var error1 = new Error("ERR_001", "Same message");
        var error2 = new Error("ERR_002", "Same message");

        error1.Should().NotBe(error2);
    }

    [Fact]
    public void DifferentMessage_AreNotEqual()
    {
        var error1 = new Error("ERR_001", "Message A");
        var error2 = new Error("ERR_001", "Message B");

        error1.Should().NotBe(error2);
    }

    [Fact]
    public void ImplementsIError()
    {
        var error = new Error("CODE", "Message");

        (error is IError).Should().BeTrue();
    }
}

public class ErrorSourceTests
{
    [Fact]
    public void IErrorSource_CanBeAppliedToClassWithErrorMembers()
    {
        var source = new TestErrorSource();

        (source is IErrorSource).Should().BeTrue();
        TestErrorSource.NotFound.Code.Should().Be("NOT_FOUND");
        TestErrorSource.NotFound.Message.Should().Be("The requested resource was not found.");
    }

    [Fact]
    public void IErrorSource_ErrorsDiscoverableViaReflection()
    {
        var errorFields = typeof(TestErrorSource)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => typeof(IError).IsAssignableFrom(f.FieldType))
            .Select(f => (IError)f.GetValue(null)!)
            .ToList();

        errorFields.Should().HaveCount(2);
        errorFields.Should().Contain(e => e.Code == "NOT_FOUND");
        errorFields.Should().Contain(e => e.Code == "ALREADY_EXISTS");
    }

    private class TestErrorSource : IErrorSource
    {
        public static readonly IError NotFound = new Error("NOT_FOUND", "The requested resource was not found.");
        public static readonly IError AlreadyExists = new Error("ALREADY_EXISTS", "The resource already exists.");
    }
}
