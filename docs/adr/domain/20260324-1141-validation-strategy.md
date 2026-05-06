# Validation Strategy

- **Timestamp:** 2026-03-24 11:41
- **Status:** accepted
- **Scope:** domain
- **Stakeholders:** Proposed by: Claude Code, Decided by: @cartomatic

---

## Summary

YAF validates at three levels: domain self-validation (invariants enforced within domain objects), application pipeline validation (commands/queries validated before handler execution), and API input validation (FluentValidation at the HTTP boundary). Each level serves a different purpose and catches different classes of errors. Validation failures flow through the `Result<T>` / `IError` pattern — no validation exceptions.

## Drivers

1. **Defense in depth** — Invalid data should be caught as early as possible, but each layer validates what it owns. The API validates input shape, the application validates business preconditions, the domain enforces invariants.
2. **Separation of concerns** — Input format validation ("is this a valid email?") is different from business rule validation ("can this customer place an order?"). They belong in different layers.
3. **Consistent error reporting** — Validation failures should flow through the same `Result<T>` / `IError` pipeline as all other errors, producing consistent ProblemDetails responses.
4. **Consumer ergonomics** — FluentValidation is well-known and expressive. Consumers shouldn't have to learn a custom validation framework for input validation.
5. **Testability** — Domain invariants should be testable without HTTP or a DI container. Application validators should be testable without infrastructure.

## Options

### Domain Validation

| Option | Assessment |
|--------|------------|
| **Self-validating domain objects** | **Selected.** Domain objects enforce their own invariants in constructors, factory methods, and state-changing methods. Returns `Result<T>` on failure. `IValidatable` interface for explicit validation triggers. |
| External domain validators only | Separates validation from the object it protects. Invariants leak outside the aggregate boundary. |
| Data Annotations on domain objects | Framework dependency in Domain. Mixes infrastructure concerns with domain logic. |

### Application Pipeline Validation

| Option | Assessment |
|--------|------------|
| **`IValidator<T>` pipeline before handler execution** | **Selected.** Validators run as a pipeline step before the command/query handler. Failures short-circuit — handler never executes. Returns `Result` with `ValidationError`. |
| Validation inside handlers | Mixes validation with business logic. Same precondition checks repeated across handlers. |
| No pipeline validation | Pushes all validation to domain or API layer. Application-level preconditions (e.g., "does this entity exist?") have no home. |

### API Input Validation

| Option | Assessment |
|--------|------------|
| **FluentValidation integrated at the API boundary** | **Selected.** Validates request DTOs before they reach the application layer. Expressive rule syntax, well-documented, good AI familiarity. |
| Data Annotations | Limited expressiveness. No conditional rules, no cross-property validation, no async validation. |
| Manual validation in controllers | Repetitive, inconsistent, hard to test. |

## Recommendation

Three-level validation: domain self-validation for invariants, application pipeline for business preconditions, FluentValidation at the API boundary for input shape. All failures flow through `Result<T>` / `IError`.

## Consequences

**Positive:**
- Invalid data is caught at the earliest appropriate layer
- Domain invariants are self-contained and testable without infrastructure
- Application pipeline validation prevents unnecessary domain operations (fail fast)
- FluentValidation at the API boundary gives rich, expressive input validation with minimal code
- Consistent error reporting — all validation failures produce the same ProblemDetails structure

**Negative:**
- Three validation layers can feel like duplication (mitigated: each layer validates different things)
- Developers must understand which validation belongs where — wrong placement reduces effectiveness
- FluentValidation is a third-party dependency in the API layer (acceptable — it's the de facto standard and stays in the outer layer)

## Conclusion

### Validation Levels

| Level | Layer | What It Validates | Mechanism | Package |
|-------|-------|-------------------|-----------|---------|
| **Domain** | Domain | Invariants — business rules that must always hold | Self-validation in methods and constructors, `IValidatable` | Yaf.Domain |
| **Application** | Application | Business preconditions — things that must be true before a handler runs | `IValidator<TCommand>` pipeline step | Yaf.Application |
| **API** | Adapter | Input shape — format, required fields, ranges, patterns | FluentValidation on request DTOs | Yaf.Api |

### Domain Self-Validation

Domain objects enforce invariants at construction and during state changes:

```
// In factory method
public static Result<Order> Create(CustomerId customerId, IReadOnlyList<OrderItem> items)
{
    if (items.Count == 0)
        return Result.Failure<Order>(OrderErrors.NoItems);
    // ...
}

// In state-changing method
public Result AddItem(Product product, int quantity)
{
    if (quantity <= 0)
        return Result.Failure(OrderErrors.InvalidQuantity);
    if (_items.Count >= MaxItems)
        return Result.Failure(OrderErrors.TooManyItems);
    // ...
}
```

`IValidatable` provides an explicit validation trigger for scenarios where an aggregate needs full consistency checking:

```
public interface IValidatable
{
    Result Validate();
}
```

### Application Pipeline Validation

`IValidator<T>` runs before the command/query handler:

```
// Yaf.Application.Validation
public interface IValidator<in T>
{
    Task<Result> ValidateAsync(T instance, CancellationToken cancellationToken);
}
```

The signature is asynchronous — application-level validation typically requires repository lookups, claims-based permission checks, or other I/O. A synchronous validator interface would force every implementation that needs to look up state to either block or carry an awkward sync/async split. The `Result` (non-generic) return type signals that validation produces pass/fail with errors, not a transformed value: the original instance flows onward unchanged.

The CQRS pipeline automatically runs all registered validators for a command/query before dispatching to the handler. If any validator returns a failure, the handler is never called and the validation errors are returned.

**Examples of application-level validation:**
- "Does the referenced entity exist?" (requires repository lookup)
- "Does the current user have permission for this operation?" (requires context)
- "Is this combination of parameters valid?" (cross-field business rules that don't belong in input validation)

### API Input Validation (FluentValidation)

FluentValidation rules on request DTOs at the HTTP boundary:

```
public class PlaceOrderRequestValidator : AbstractValidator<PlaceOrderRequest>
{
    public PlaceOrderRequestValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
        });
    }
}
```

Integrated into the pipeline via Yaf.Api's `AddYaf()` setup. Validation failures are automatically mapped to 400 Bad Request ProblemDetails responses.

### Error Flow

```
API Request
  → FluentValidation (input shape)
    ✗ → 400 Bad Request (ProblemDetails with validation details)
    ✓ → Map to Command/Query
      → Sanitization (clean inputs marked with [Sanitize])
        → Application IValidator<T> pipeline (business preconditions — validates sanitized data)
          ✗ → appropriate error (via Result<T> → ProblemDetails)
          ✓ → Handler executes
            → Domain invariants (self-validation)
              ✗ → 422 Unprocessable Entity (via Result<T> → ProblemDetails)
              ✓ → Success response
```

### What Validates Where

| Concern | Layer | Example |
|---------|-------|---------|
| Required fields, format, range | API (FluentValidation) | "CustomerId must not be empty" |
| String length, pattern matching | API (FluentValidation) | "Email must be a valid email format" |
| Entity existence | Application (IValidator) | "Customer with this ID must exist" |
| Permission checks | Application (IValidator) | "Current user must own this order" |
| Cross-field business rules | Application (IValidator) | "Express shipping only available for domestic orders" |
| Aggregate invariants | Domain (self-validation) | "Order cannot have zero items" |
| State transition rules | Domain (self-validation) | "Shipped order cannot be modified" |
| Value object constraints | Domain (construction) | "Money amount must be positive" |

## More Information

- [YAF DDD Concepts Brainstorm](../../brainstorms/20260322-2036-yaf-ddd-concepts-brainstorm.md) — validation (section 13)
- [ADR: Result and Error Pattern](20260324-1140-result-and-error-pattern.md) — Result\<T\> and error hierarchy
- [ADR: Technology Stack](../architecture/20260324-0948-technology-stack.md) — FluentValidation selection
- [ADR: API Adapter — Controllers](../api/20260324-1325-api-adapter-controllers.md) — ProblemDetails mapping for validation errors
