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
    public void ImplementsIErrorInternally()
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
            .Where(f => typeof(Error).IsAssignableFrom(f.FieldType))
            .Select(f => (Error)f.GetValue(null)!)
            .ToList();

        errorFields.Should().HaveCount(2);
        errorFields.Should().Contain(e => e.Code == "NOT_FOUND");
        errorFields.Should().Contain(e => e.Code == "ALREADY_EXISTS");
    }

    private class TestErrorSource : IErrorSource
    {
        public static readonly Error NotFound = new Error("NOT_FOUND", "The requested resource was not found.");
        public static readonly Error AlreadyExists = new Error("ALREADY_EXISTS", "The resource already exists.");
    }
}

public class ErrorCreateFactoryTests
{
    [Fact]
    public void Create_SimpleType_ProducesFullyQualifiedCode()
    {
        var error = Error.Create<ErrorCreateFactoryTests>("Something went wrong.");

        error.Code.Should().Be("Yaf.Domain.Tests.ErrorCreateFactoryTests.Create_SimpleType_ProducesFullyQualifiedCode");
        error.Message.Should().Be("Something went wrong.");
    }

    [Fact]
    public void Create_CallerMemberName_ResolvesFieldName() =>
        SimpleErrorHolder.FieldError.Code.Should().Be($"{typeof(SimpleErrorHolder).FullName}.FieldError");

    [Fact]
    public void Create_CallerMemberName_ResolvesPropertyName() =>
        SimpleErrorHolder.PropertyError.Code.Should().Be($"{typeof(SimpleErrorHolder).FullName}.PropertyError");

    [Fact]
    public void Create_NestedType_ReplacesPlusSeparatorWithDot()
    {
        var error = Error.Create<OuterClass.InnerClass>("Nested error.");

        var expected = typeof(OuterClass.InnerClass).FullName!.Replace('+', '.');
        error.Code.Should().StartWith(expected);
    }

    [Fact]
    public void Create_GenericType_StripsBacktickAndArity()
    {
        var error = Error.Create<GenericHolder<string>>("Generic error.");

        error.Code.Should().Be("Yaf.Domain.Tests.GenericHolder.Create_GenericType_StripsBacktickAndArity");
    }

    [Fact]
    public void Create_MultiArityGenericType_StripsBacktickAndArity()
    {
        var error = Error.Create<MultiArityHolder<string, int>>("Multi-arity error.");

        error.Code.Should().Be("Yaf.Domain.Tests.MultiArityHolder.Create_MultiArityGenericType_StripsBacktickAndArity");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_InvalidMessage_ThrowsArgumentException(string? message)
    {
        var act = () => Error.Create<ErrorCreateFactoryTests>(message!);

        act.Should().Throw<ArgumentException>();
    }
}

public class ErrorUnspecifiedFactoryTests
{
    [Fact]
    public void Unspecified_ProducesCodeEndingWithUnspecified()
    {
        var error = Error.Unspecified<ErrorUnspecifiedFactoryTests>("An unexpected error.");

        error.Code.Should().Be("Yaf.Domain.Tests.ErrorUnspecifiedFactoryTests.Unspecified");
        error.Message.Should().Be("An unexpected error.");
    }

    [Fact]
    public void Unspecified_NestedType_ReplacesPlusSeparator()
    {
        var error = Error.Unspecified<OuterClass.InnerClass>("Nested unspecified.");

        var expected = typeof(OuterClass.InnerClass).FullName!.Replace('+', '.') + ".Unspecified";
        error.Code.Should().Be(expected);
    }

    [Fact]
    public void Unspecified_GenericType_StripsBacktickAndArity()
    {
        var error = Error.Unspecified<GenericHolder<int>>("Generic unspecified.");

        error.Code.Should().Be("Yaf.Domain.Tests.GenericHolder.Unspecified");
    }

    [Fact]
    public void Unspecified_MultiArityGenericType_StripsBacktickAndArity()
    {
        var error = Error.Unspecified<MultiArityHolder<string, int>>("Multi-arity unspecified.");

        error.Code.Should().Be("Yaf.Domain.Tests.MultiArityHolder.Unspecified");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Unspecified_InvalidMessage_ThrowsArgumentException(string? message)
    {
        var act = () => Error.Unspecified<ErrorUnspecifiedFactoryTests>(message!);

        act.Should().Throw<ArgumentException>();
    }
}

#region Test Types for Factory Methods

internal class SimpleErrorHolder
{
    public static readonly Error FieldError = Error.Create<SimpleErrorHolder>("A field error.");
    public static Error PropertyError => Error.Create<SimpleErrorHolder>("A property error.");
}

internal class OuterClass
{
    internal class InnerClass;
}

internal class GenericHolder<T>;

internal class MultiArityHolder<T1, T2>;

#endregion
