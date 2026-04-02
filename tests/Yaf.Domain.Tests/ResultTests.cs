using AwesomeAssertions;
using Yaf.Domain.Interfaces;

namespace Yaf.Domain.Tests;

#region Success Tests

public class ResultSuccessTests
{
    [Fact]
    public void Success_WithValue_IsSuccessTrue()
    {
        var result = Result.Success("hello");

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void Success_WithValueType_WorksCorrectly()
    {
        var result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Success_RejectsNullReferenceType()
    {
        var act = () => Result.Success<string>(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void NonGeneric_Success_IsSuccessTrue()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
    }
}

#endregion

#region Failure Tests

public class ResultFailureTests
{
    [Fact]
    public void SingleError_IsFailureTrue()
    {
        var error = new Error("ERR", "Something failed");
        var result = Result.Failure<string>(error);

        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void MultiError_IsFailureTrue()
    {
        var errors = new List<Error>
        {
            new("ERR1", "First"),
            new("ERR2", "Second")
        };
        var result = Result.Failure<string>(errors);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Error_OnSingleErrorFailure_ReturnsTheError()
    {
        var error = new Error("ERR", "Message");
        var result = Result.Failure<string>(error);

        result.Error.Should().Be(error);
    }

    [Fact]
    public void Error_OnMultiErrorFailure_ReturnsFirstError()
    {
        var first = new Error("ERR1", "First");
        var second = new Error("ERR2", "Second");
        var result = Result.Failure<string>(new List<Error> { first, second });

        result.Error.Should().Be(first);
    }

    [Fact]
    public void Errors_OnSingleErrorFailure_ReturnsSingleElementCollection()
    {
        var error = new Error("ERR", "Message");
        var result = Result.Failure<string>(error);

        result.Errors.Should().HaveCount(1);
        result.Errors[0].Should().Be(error);
    }

    [Fact]
    public void Errors_OnMultiErrorFailure_ReturnsAllErrorsInOrder()
    {
        var first = new Error("ERR1", "First");
        var second = new Error("ERR2", "Second");
        var third = new Error("ERR3", "Third");
        var result = Result.Failure<string>(new List<Error> { first, second, third });

        result.Errors.Should().HaveCount(3);
        result.Errors[0].Should().Be(first);
        result.Errors[1].Should().Be(second);
        result.Errors[2].Should().Be(third);
    }

    [Fact]
    public void Failure_RejectsNullError()
    {
        var act = () => Result.Failure<string>((Error)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Failure_RejectsEmptyCollection()
    {
        var act = () => Result.Failure<string>(new List<Error>());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Failure_RejectsNullCollection()
    {
        var act = () => Result.Failure<string>((IReadOnlyCollection<Error>)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Failure_RejectsCollectionWithNullElements()
    {
        var act = () => Result.Failure<string>(new List<Error> { new("ERR", "Valid"), null! });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NonGeneric_Failure_WithSingleError_WorksCorrectly()
    {
        var error = new Error("ERR", "Message");
        var result = Result.Failure(error);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void NonGeneric_Failure_WithMultipleErrors_WorksCorrectly()
    {
        var errors = new List<Error> { new("ERR1", "First"), new("ERR2", "Second") };
        var result = Result.Failure(errors);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().HaveCount(2);
    }
}

#endregion

#region Access Tests

public class ResultAccessTests
{
    [Fact]
    public void Value_OnFailure_ThrowsInvalidOperationException()
    {
        var result = Result.Failure<string>(new Error("ERR", "Failed"));

        var act = () => result.Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Error_OnSuccess_ThrowsInvalidOperationException()
    {
        var result = Result.Success("ok");

        var act = () => result.Error;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Errors_OnSuccess_ThrowsInvalidOperationException()
    {
        var result = Result.Success("ok");

        var act = () => result.Errors;

        act.Should().Throw<InvalidOperationException>();
    }
}

#endregion

#region Default Tests

public class ResultDefaultTests
{
    [Fact]
    public void DefaultGeneric_IsFailure()
    {
        Result<string> result = default;

        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void DefaultGeneric_Value_ThrowsInvalidOperationException()
    {
        Result<string> result = default;

        var act = () => result.Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void DefaultGeneric_Error_ReturnsSentinelError()
    {
        Result<string> result = default;

        result.Error.Code.Should().Be("Yaf.Domain.Result.Uninitialized");
        result.Error.Message.Should().Be("Result was not properly initialized.");
    }

    [Fact]
    public void DefaultGeneric_Errors_ReturnsSingleElementWithSentinel()
    {
        Result<string> result = default;

        result.Errors.Should().HaveCount(1);
        result.Errors[0].Code.Should().Be("Yaf.Domain.Result.Uninitialized");
    }

    [Fact]
    public void DefaultNonGeneric_IsFailure_WithSentinelError()
    {
        Result result = default;

        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Yaf.Domain.Result.Uninitialized");
        result.Errors.Should().HaveCount(1);
    }
}

#endregion

#region Implicit Conversion Tests

public class ResultImplicitConversionTests
{
    [Fact]
    public void T_ImplicitlyConvertsToSuccessResult()
    {
        Result<string> result = "hello";

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void ValueType_ImplicitlyConvertsToSuccessResult()
    {
        Result<int> result = 42;

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Error_ImplicitlyConvertsToGenericFailureResult()
    {
        var error = new Error("ERR", "Failed");
        Result<string> result = error;

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Error_ImplicitlyConvertsToNonGenericFailureResult()
    {
        var error = new Error("ERR", "Failed");
        Result result = error;

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void NullT_ViaImplicitConversion_ThrowsArgumentNullException()
    {
        var act = () =>
        {
            Result<string> result = (string)null!;
        };

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void NullError_ViaImplicitConversion_ThrowsArgumentNullException()
    {
        var act = () =>
        {
            Result<string> result = (Error)null!;
        };

        act.Should().Throw<ArgumentNullException>();
    }
}

#endregion

#region Equality Tests

public class ResultEqualityTests
{
    [Fact]
    public void TwoSuccessResults_SameValue_AreEqual()
    {
        var a = Result.Success("hello");
        var b = Result.Success("hello");

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void TwoSuccessResults_DifferentValues_AreNotEqual()
    {
        var a = Result.Success("hello");
        var b = Result.Success("world");

        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void TwoFailureResults_SameErrors_AreEqual()
    {
        var error = new Error("ERR", "Message");
        var a = Result.Failure<string>(error);
        var b = Result.Failure<string>(error);

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void TwoFailureResults_DifferentErrors_AreNotEqual()
    {
        var a = Result.Failure<string>(new Error("ERR1", "First"));
        var b = Result.Failure<string>(new Error("ERR2", "Second"));

        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void SuccessAndFailure_AreNotEqual()
    {
        var success = Result.Success("ok");
        var failure = Result.Failure<string>(new Error("ERR", "Failed"));

        success.Equals(failure).Should().BeFalse();
    }

    [Fact]
    public void DefaultResults_AreEqualToEachOther()
    {
        Result<string> a = default;
        Result<string> b = default;

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_ConsistentWithEquality()
    {
        var a = Result.Success("hello");
        var b = Result.Success("hello");

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void OperatorEquals_ReturnsTrue_ForEqualResults()
    {
        var a = Result.Success("hello");
        var b = Result.Success("hello");

        (a == b).Should().BeTrue();
    }

    [Fact]
    public void OperatorNotEquals_ReturnsTrue_ForDifferentResults()
    {
        var a = Result.Success("hello");
        var b = Result.Success("world");

        (a != b).Should().BeTrue();
    }

    [Fact]
    public void EqualsObject_WorksCorrectly()
    {
        var a = Result.Success("hello");
        object b = Result.Success("hello");

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void NonGeneric_TwoSuccessResults_AreEqual()
    {
        var a = Result.Success();
        var b = Result.Success();

        (a == b).Should().BeTrue();
    }

    [Fact]
    public void NonGeneric_SuccessAndFailure_AreNotEqual()
    {
        var success = Result.Success();
        var failure = Result.Failure(new Error("ERR", "Failed"));

        (success == failure).Should().BeFalse();
    }
}

#endregion

#region ToString Tests

public class ResultToStringTests
{
    [Fact]
    public void GenericSuccess_ReturnsSuccessWithValue()
    {
        var result = Result.Success("hello");

        result.ToString().Should().Be("Success(hello)");
    }

    [Fact]
    public void NonGenericSuccess_ReturnsSuccess()
    {
        var result = Result.Success();

        result.ToString().Should().Be("Success");
    }

    [Fact]
    public void FailureSingleError_ReturnsFailureWithCode()
    {
        var result = Result.Failure<string>(new Error("NOT_FOUND", "Not found"));

        result.ToString().Should().Be("Failure(NOT_FOUND)");
    }

    [Fact]
    public void FailureMultipleErrors_ReturnsFailureWithAllCodes()
    {
        var result = Result.Failure<string>(new List<Error>
        {
            new("ERR1", "First"),
            new("ERR2", "Second")
        });

        result.ToString().Should().Be("Failure(ERR1, ERR2)");
    }

    [Fact]
    public void GenericDefault_ReturnsUninitializedWithTypeName()
    {
        Result<string> result = default;

        result.ToString().Should().Be("Result<String>(Uninitialized)");
    }

    [Fact]
    public void NonGenericDefault_ReturnsUninitialized()
    {
        Result result = default;

        result.ToString().Should().Be("Result(Uninitialized)");
    }
}

#endregion

#region Validatable Integration Tests

public class ResultValidatableIntegrationTests
{
    [Fact]
    public void ValidationErrors_PassDirectlyToResultFailure()
    {
        var validatable = new TestValidatable(isValid: false);
        var errors = validatable.GetValidationErrors();

        var result = Result.Failure<string>(errors);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().HaveCount(1);
        result.Error.Code.Should().Be("INVALID");
    }

    private class TestValidatable : IValidatable
    {
        private readonly bool _isValid;

        public TestValidatable(bool isValid) => _isValid = isValid;

        public IReadOnlyCollection<Error> GetValidationErrors() =>
            _isValid ? [] : [new Error("INVALID", "Validation failed")];
    }
}

#endregion
