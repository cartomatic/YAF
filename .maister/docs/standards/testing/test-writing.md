## Test Writing

### Test Behavior
Focus on what code does, not how it does it, to allow safe refactoring.

### Risk-Based Testing
Prioritize testing based on business criticality and likelihood of bugs.

### Critical Path Focus
Ensure core user workflows and critical business logic are well-tested.

### xUnit Framework
xUnit as the test framework with AwesomeAssertions for fluent assertions. TestContainers for integration tests.

```xml
<PackageReference Include="xunit" />
<PackageReference Include="AwesomeAssertions" />
<PackageReference Include="Testcontainers" />
```

### Test Naming
`Method_Condition_ExpectedBehavior` with underscores. Test classes named `{Feature}{Concern}Tests`.

```csharp
public class OrderCreationTests
{
    [Fact]
    public void Create_WithValidName_ReturnsSuccessResult() { }

    [Fact]
    public void Create_WithEmptyName_ReturnsValidationError() { }
}
```

### One Test Project Per Source
Mirror `src/` structure under `tests/`. Each source project gets a corresponding test project.

```
src/Yaf.Domain/         --> tests/Yaf.Domain.Tests/
src/Yaf.Application/    --> tests/Yaf.Application.Tests/
```

### Real Databases
TestContainers with real database engines. No in-memory EF Core providers, no SQLite substitution for integration tests.

### Minimal Mocking
Mock only external boundaries (third-party APIs, email services, cloud providers). Use real implementations for everything else.

### Memento Round-Trip Tests
Every persisted entity must have: create -> snapshot -> restore -> verify equality.

```csharp
[Fact]
public void Memento_RoundTrip_PreservesAllProperties()
{
    var order = Order.Create("Test").Value;
    var memento = order.ToMemento();
    var restored = Order.Restore(memento);

    restored.Name.Should().Be(order.Name);
    restored.Id.Should().Be(order.Id);
}
```

### Arrange-Act-Assert
Implicit AAA pattern (no section comments needed). Use lambda-based act for exception tests.

```csharp
[Fact]
public void Create_WithNull_ThrowsArgumentNullException()
{
    var act = () => Order.Create(null!);

    act.Should().Throw<ArgumentNullException>();
}
```

### Test Helper Types
Private nested classes within test classes, or `#region Test Types` blocks with internal types. Keep test infrastructure close to tests that use it.

### XML Docs Exempt
CS1591 (missing XML documentation) is relaxed in test projects. Tests do not require XML doc comments.

### Container Reuse
TestContainers instances are shared across test classes via xUnit fixtures to avoid repeated container startup.
