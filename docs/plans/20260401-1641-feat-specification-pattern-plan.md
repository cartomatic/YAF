---
title: "feat: Add Specification Pattern to Domain Layer"
type: feat
status: rejected
date: 2026-04-01
---

# feat: Add Specification Pattern to Domain Layer

## Overview

Add the Specification pattern (`ISpecification<T>`) to `Yaf.Domain` as a composable, expression-based predicate abstraction for querying and filtering domain objects. This is a core DDD building block that encapsulates business rules as reusable, combinable objects that can be evaluated in-memory or translated to database queries via EF Core.

The implementation is **layered**: Phase 1 delivers the predicate core with And/Or/Not composition. Phase 2 extends it with `IQuerySpecification<T>` for ordering, paging, and includes.

## Problem Statement / Motivation

Currently `Yaf.Domain` provides entity, aggregate root, and value object base types but has no way to express reusable query criteria or business rule predicates. Without specifications:

- Repository interfaces would need method-per-query (`FindActiveOrders`, `FindByCustomer`), leading to bloated interfaces
- Business rules that determine "which entities match a condition" have no standard home
- Query logic cannot be composed (e.g., "active orders" AND "for customer X" AND "placed this month")
- Domain services that need to filter entities must depend on infrastructure

The Specification pattern solves all of these by encapsulating predicates as first-class domain objects.

## Proposed Solution

### Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Predicate type | `Expression<Func<T, bool>>` | Translateable to SQL by EF Core. `System.Linq.Expressions` is BCL — no external dependency. `IsSatisfiedBy` derived by compiling the expression. |
| Interface scope | Layered | `ISpecification<T>` = predicate + composition. `IQuerySpecification<T>` extends with ordering, paging, includes. |
| Generic constraint | Unconstrained `T` | Allows specs over entities, value objects, DTOs. Repository adds its own `AggregateRoot` constraint. Avoids cascading `TId` parameter. |
| Validation integration | Deferred | `ISpecification<T>` stays a pure predicate. `IValidatingSpecification<T>` can be added later if needed. |
| Composition mechanism | Extension methods + internal sealed decorators | `spec.And(other)` returns internal `AndSpecification<T>`. Consistent with `ValidatableExtensions` pattern. |
| Base class | Abstract `Specification<T>` | Handles delegate caching for `IsSatisfiedBy`. Consumers override `Expression<Func<T, bool>> Criteria`. |

### Architecture

```
Yaf.Domain/
├── Interfaces/
│   ├── ISpecification.cs          # ISpecification<T> — Phase 1
│   └── IQuerySpecification.cs     # IQuerySpecification<T> — Phase 2
├── Specifications/                # New folder
│   ├── Specification.cs           # Abstract base class — Phase 1
│   ├── QuerySpecification.cs      # Abstract base class — Phase 2
│   ├── OrderByExpression.cs       # Ordering descriptor — Phase 2
│   └── IncludeExpression.cs       # Include descriptor — Phase 2
├── Extensions/
│   └── SpecificationExtensions.cs # And/Or/Not composition — Phase 1
└── Helpers/
    └── ExpressionHelper.cs        # Parameter rebinding for composition — Phase 1
```

## Technical Considerations

### Expression Tree Composition

Combining two `Expression<Func<T, bool>>` with `&&` or `||` requires parameter rebinding — each expression has its own parameter instance. An `ExpressionVisitor` subclass (`ParameterReplacer`) rebinds the second expression's parameter to match the first. This is the most technically nuanced part of the implementation.

```
// Pseudocode — And composition
var param = left.Parameters[0];
var rebound = new ParameterReplacer(right.Parameters[0], param).Visit(right.Body);
return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(left.Body, rebound), param);
```

### Delegate Caching

`IsSatisfiedBy(T entity)` compiles the expression to a `Func<T, bool>`. Compilation is expensive, so the abstract `Specification<T>` base class caches the compiled delegate using `Lazy<Func<T, bool>>` for thread-safe lazy initialization.

### Identity Specifications

`Specification<T>.All` (always true) and `Specification<T>.None` (always false) serve as identity elements for Or/And composition and as useful default values.

### No External Dependencies

All types use only BCL (`System.Linq.Expressions`, `System.Linq`). Zero NuGet packages. Consistent with `Yaf.Domain` conventions.

### Thread Safety

Specifications are effectively immutable after construction. The only mutable state is the `Lazy<T>` delegate cache, which is thread-safe by design.

### Includes — Infrastructure Concern

`IQuerySpecification<T>` will carry include expressions as data (`Expression<Func<T, object>>` descriptors). The domain layer stores them; the infrastructure layer's specification evaluator interprets them for EF Core's `.Include()`. This keeps the domain layer ORM-agnostic while enabling eager loading.

## Phase 1: Predicate Core + Composition

### Deliverables

#### `ISpecification<T>` Interface

```csharp
// src/Yaf.Domain/Interfaces/ISpecification.cs
namespace Yaf.Domain.Interfaces;

/// <summary>
/// Encapsulates a business rule or query predicate as a reusable, composable object.
/// </summary>
/// <typeparam name="T">The type of object this specification evaluates.</typeparam>
public interface ISpecification<T>
{
    /// <summary>
    /// Gets the expression tree representing the predicate criteria.
    /// Translateable to SQL when used with EF Core repositories.
    /// </summary>
    Expression<Func<T, bool>> Criteria { get; }

    /// <summary>
    /// Evaluates whether the specified entity satisfies this specification.
    /// </summary>
    bool IsSatisfiedBy(T entity);
}
```

#### `Specification<T>` Abstract Base Class

```csharp
// src/Yaf.Domain/Specifications/Specification.cs
namespace Yaf.Domain.Specifications;

/// <summary>
/// Base class for specifications. Handles delegate caching for in-memory evaluation.
/// </summary>
public abstract class Specification<T> : ISpecification<T>
{
    private readonly Lazy<Func<T, bool>> _compiled;

    protected Specification()
    {
        _compiled = new Lazy<Func<T, bool>>(() => Criteria.Compile());
    }

    /// <inheritdoc />
    public abstract Expression<Func<T, bool>> Criteria { get; }

    /// <inheritdoc />
    public bool IsSatisfiedBy(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return _compiled.Value(entity);
    }

    /// <summary>Always-true specification. Identity element for And composition.</summary>
    public static ISpecification<T> All { get; } = new AllSpecification<T>();

    /// <summary>Always-false specification. Identity element for Or composition.</summary>
    public static ISpecification<T> None { get; } = new NoneSpecification<T>();
}
```

#### Composition Extension Methods

```csharp
// src/Yaf.Domain/Extensions/SpecificationExtensions.cs
namespace Yaf.Domain.Extensions;

public static class SpecificationExtensions
{
    public static ISpecification<T> And<T>(this ISpecification<T> left, ISpecification<T> right);
    public static ISpecification<T> Or<T>(this ISpecification<T> left, ISpecification<T> right);
    public static ISpecification<T> Not<T>(this ISpecification<T> specification);
}
```

#### Internal Composition Types

- `AndSpecification<T>` — sealed, internal. Combines two specs with `Expression.AndAlso`.
- `OrSpecification<T>` — sealed, internal. Combines two specs with `Expression.OrElse`.
- `NotSpecification<T>` — sealed, internal. Negates a spec with `Expression.Not`.
- `AllSpecification<T>` — sealed, internal. Returns `_ => true`.
- `NoneSpecification<T>` — sealed, internal. Returns `_ => false`.

All internal composition types extend `Specification<T>` to inherit delegate caching.

#### Expression Helper

```csharp
// src/Yaf.Domain/Helpers/ExpressionHelper.cs
namespace Yaf.Domain.Helpers;

/// <summary>
/// Provides expression tree utilities for specification composition.
/// </summary>
internal static class ExpressionHelper
{
    internal static Expression<Func<T, bool>> Combine<T>(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right,
        Func<Expression, Expression, BinaryExpression> combiner);
}
```

### Phase 1 Test Plan

Tests in `tests/Yaf.Domain.Tests/SpecificationTests.cs`:

| Test Class | Tests |
|-----------|-------|
| `SpecificationCriteriaTests` | Single spec evaluates correctly; criteria expression is not null; expression is translateable (can be compiled and invoked) |
| `SpecificationIsSatisfiedByTests` | Returns true for matching entity; returns false for non-matching; throws `ArgumentNullException` on null entity; multiple calls reuse cached delegate |
| `SpecificationAndTests` | And of two matching = true; And of one non-matching = false; And with All = original; And preserves expression composability; throws on null argument |
| `SpecificationOrTests` | Or of one matching = true; Or of both non-matching = false; Or with None = original; throws on null argument |
| `SpecificationNotTests` | Not of matching = false; Not of non-matching = true; double Not = original semantics |
| `SpecificationAllTests` | All.IsSatisfiedBy always true; All.And(spec) = spec semantics; All.Criteria compiles to `_ => true` |
| `SpecificationNoneTests` | None.IsSatisfiedBy always false; None.Or(spec) = spec semantics |
| `SpecificationCompositionTests` | Deep nesting: `(A && B) \|\| !C`; three-level composition compiles and evaluates correctly; composed expression can be passed to `IQueryable.Where()` |

## Phase 2: Query Specification (Ordering, Paging, Includes)

### Deliverables

#### `IQuerySpecification<T>` Interface

```csharp
// src/Yaf.Domain/Interfaces/IQuerySpecification.cs
namespace Yaf.Domain.Interfaces;

/// <summary>
/// Extends <see cref="ISpecification{T}"/> with query shaping: ordering, paging, and includes.
/// </summary>
public interface IQuerySpecification<T> : ISpecification<T>
{
    /// <summary>Gets the ordering expressions applied to query results.</summary>
    IReadOnlyList<OrderByExpression<T>> OrderBy { get; }

    /// <summary>Gets the navigation property include expressions for eager loading.</summary>
    IReadOnlyList<IncludeExpression<T>> Includes { get; }

    /// <summary>Gets the number of results to skip, or null for no skip.</summary>
    int? Skip { get; }

    /// <summary>Gets the maximum number of results to return, or null for no limit.</summary>
    int? Take { get; }
}
```

#### `QuerySpecification<T>` Abstract Base Class

Extends `Specification<T>`, implements `IQuerySpecification<T>`. Provides a fluent protected API for subclasses:

```csharp
// Pseudocode — consumer usage
public sealed class RecentActiveOrdersSpec : QuerySpecification<Order>
{
    public RecentActiveOrdersSpec(int pageSize, int page)
    {
        AddOrderByDescending(o => o.CreatedAtUtc);
        ApplyPaging(page * pageSize, pageSize);
        AddInclude(o => o.LineItems);
    }

    public override Expression<Func<Order, bool>> Criteria =>
        o => o.IsActive;
}
```

#### Supporting Types

- `OrderByExpression<T>` — record holding `Expression<Func<T, object>>` + `OrderDirection` enum (Ascending/Descending)
- `IncludeExpression<T>` — record holding `Expression<Func<T, object>>` for navigation property paths
- `OrderDirection` — enum: `Ascending`, `Descending`

### Phase 2 Test Plan

Tests in `tests/Yaf.Domain.Tests/QuerySpecificationTests.cs`:

| Test Class | Tests |
|-----------|-------|
| `QuerySpecificationOrderingTests` | Single ascending; single descending; multiple orderings preserved in order; empty orderings by default |
| `QuerySpecificationPagingTests` | Skip and Take set correctly; null by default; zero skip is valid; negative values — decide behavior (throw or ignore) |
| `QuerySpecificationIncludeTests` | Single include; multiple includes; empty includes by default |
| `QuerySpecificationCriteriaTests` | Criteria still works (inherited from Specification); IsSatisfiedBy still works; composition still works on query specs |

## Acceptance Criteria

### Phase 1

- [ ] `ISpecification<T>` interface in `Yaf.Domain.Interfaces`
- [ ] `Specification<T>` abstract base class in `Yaf.Domain.Specifications`
- [ ] `And`, `Or`, `Not` extension methods in `Yaf.Domain.Extensions`
- [ ] `Specification<T>.All` and `Specification<T>.None` identity specifications
- [ ] Internal sealed composition types (`AndSpecification`, `OrSpecification`, `NotSpecification`)
- [ ] `ExpressionHelper` for parameter rebinding
- [ ] All public API has XML documentation (CS1591 enforced)
- [ ] All Phase 1 tests pass
- [ ] `Expression<Func<T, bool>>` from composed specifications can be passed to `IQueryable<T>.Where()` (verified in test)
- [ ] Zero external NuGet dependencies

### Phase 2

- [ ] `IQuerySpecification<T>` interface extending `ISpecification<T>`
- [ ] `QuerySpecification<T>` abstract base class
- [ ] `OrderByExpression<T>`, `IncludeExpression<T>`, `OrderDirection` supporting types
- [ ] All Phase 2 tests pass
- [ ] All public API has XML documentation

## Dependencies & Risks

**Dependencies:**
- None for Phase 1 — self-contained within `Yaf.Domain`
- Phase 2's `IncludeExpression<T>` is an infrastructure hint that will be consumed by a future specification evaluator in the Infrastructure layer

**Risks:**
- **Expression tree parameter rebinding** is the most technically nuanced part. Mitigation: well-tested `ExpressionHelper` with edge case coverage.
- **EF Core translation limits** for deeply nested composed expressions. Mitigation: test that composed expressions can be passed to `IQueryable.Where()`. Actual EF Core translation testing belongs in infrastructure layer tests.
- **Includes leak infrastructure concerns** into the domain. Mitigation: includes are stored as data descriptors (`Expression<Func<T, object>>`), not EF Core types. The domain doesn't reference `Microsoft.EntityFrameworkCore`.

## Rejection Rationale (2026-04-01)

During planning review, analysis of the specification pattern's interaction with YAF's memento-based persistence architecture revealed a fundamental tension:

1. **EF Core operates on mementos, not domain objects.** Specifications express predicates against domain types (`Expression<Func<Order, bool>>`), but the persistence layer queries memento types (`IQueryable<OrderMemento>`). Translating domain expressions to memento expressions requires fragile convention-based expression rewriting.

2. **EF Core already provides composable queries.** `IQueryable<TMemento>` with LINQ gives the infrastructure layer full composable, translatable filtering — specifications would just wrap what EF Core already does, adding abstraction without value.

3. **In-memory domain rules don't need the pattern yet.** Business rule evaluation can be handled by entity methods, domain services, or the existing `IValidatable`/`IError` pattern. The specification pattern adds value when there are concrete, reusable cross-cutting predicates — which don't exist yet.

4. **"Require 2+ concrete use cases" principle.** Per the project's own learnings from the TypedId simplification, generic abstractions should not be built speculatively. No concrete specification use case currently exists that isn't better served by existing mechanisms.

**Conclusion:** The specification pattern is architecturally sound but misaligned with YAF's memento-based persistence model. If the persistence approach changes (e.g., EF Core mapping domain objects directly), or if concrete in-memory business rule composition needs arise, this plan can be revisited.

## References & Research

### Internal References

- [DDD Concepts Brainstorm](../brainstorms/20260322-2036-yaf-ddd-concepts-brainstorm.md) — Section 8: Specifications
- [Domain Building Blocks ADR](../adr/domain/20260324-1032-domain-building-blocks.md) — Entity, AggregateRoot, ValueObject patterns
- [TypedId Guid-Only Simplification](../solutions/design-patterns/typedid-guid-only-simplification.md) — Avoid speculative generics
- Existing extension pattern: `src/Yaf.Domain/Extensions/ValidatableExtensions.cs`
- Existing helper pattern: `src/Yaf.Domain/Helpers/MementoHelper.cs`

### Key Codebase Patterns to Follow

- Interfaces in `Yaf.Domain.Interfaces` namespace, `Interfaces/` folder
- Internal sealed types for implementation details
- `ArgumentNullException.ThrowIfNull()` for guard clauses
- `Lazy<T>` for thread-safe lazy initialization
- Multiple test classes per file, grouped by concern
- Test naming: `Method_Scenario_ExpectedResult`
- Private nested types for test doubles
