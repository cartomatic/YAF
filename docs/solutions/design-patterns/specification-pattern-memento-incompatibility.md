---
title: Specification Pattern Incompatible with Memento-Based Persistence
module: Yaf.Domain
component: ISpecification<T>, memento pattern
problem_type: design-pattern
severity: medium
tags:
  - specification-pattern
  - domain-driven-design
  - memento-pattern
  - entity-framework-core
  - expression-translation
  - architectural-tension
  - persistence-layer
date_discovered: 2026-04-01
---

# Specification Pattern Incompatible with Memento-Based Persistence

## Symptom

The team planned to add the Specification pattern (`ISpecification<T>`) to `Yaf.Domain` to provide composable, reusable predicates for querying and business rule evaluation. The pattern appeared necessary for:

- Keeping `IRepository<T>` generic (no method-per-query proliferation)
- Composable business rules (`activeOrders.And(forCustomer(id))`)
- Testable query logic independent of persistence

## Investigation

1. **Codebase analysis** — Examined existing architecture: Entity, AggregateRoot, ValueObject with memento pattern (`IMemento<TSelf, TMemento>`), cross-cutting concerns via interface detection in `MementoHelper`.

2. **Specification pattern requirements** — Designed the contract: `Expression<Func<T, bool>>` for EF Core translateability, composable And/Or/Not, unconstrained generic `T`.

3. **Layered design** — Proposed `ISpecification<T>` (predicate) + `IQuerySpecification<T>` (ordering, paging, includes).

4. **Repository integration model** — Traced the data flow: public API accepts domain objects, repository internally snapshots/restores via mementos, EF Core operates on `TMemento`.

5. **Expression translation gap identified** — Specifications express predicates against domain types (`Expression<Func<Order, bool>>`), but EF Core queries memento types (`IQueryable<OrderMemento>`). Convention-based expression rewriting (property name matching) is fragile and breaks on computed properties.

6. **In-memory evaluation assessed** — Entity methods, domain services, and `IValidatable`/`IError` already cover in-memory business rule evaluation. No gap exists that specifications would fill.

7. **"Require 2+ concrete use cases" applied** — No concrete use case identified that isn't better served by existing mechanisms.

## Root Cause

The Specification pattern assumes the ORM maps domain objects directly, so `Expression<Func<T, bool>>` passes straight to `IQueryable<T>`. With memento-based persistence, EF Core operates exclusively on `TMemento` types, creating a fundamental type gap:

```
Specification: Expression<Func<Order, bool>>      (domain type)
EF Core:       IQueryable<OrderMemento>.Where(...) (memento type)
                    ↑
            These are different types — no automatic translation
```

Bridging this gap via convention-based expression rewriting (matching property names, inferring conversions) is fragile and fails silently on computed properties and domain-level business logic that has no memento equivalent.

## Resolution

**Decision: Do not implement the Specification pattern.**

EF Core on mementos already provides `IQueryable<TMemento>` with full LINQ — composable, translatable filtering. A specification pattern wrapping this adds abstraction without value.

The pattern can be revisited if:

- **(a) Persistence architecture changes** — If the framework transitions to direct domain object mapping, the type gap closes and `Expression<Func<T, bool>>` becomes directly translatable.
- **(b) Concrete composition needs emerge** — If in-memory business rule composition requirements arise that aren't served by entity methods, domain services, or `IValidatable`.

## Key Insight

The Specification pattern's core value proposition — centralizing business rules as composable, persistence-translatable predicates — is at odds with memento-based architecture. The pattern relocates the type translation problem from the repository to the specification layer while adding an extra indirection. Existing mechanisms (entity methods for domain logic, EF Core LINQ for persistence queries) are simpler and more cohesive.

## Prevention Checklist

Before adopting any DDD pattern that bridges domain and infrastructure layers:

- [ ] **Map the pattern's type contracts** — What type does the pattern assume is queryable? Is that type what you actually persist/query?
- [ ] **Audit your persistence layer's type model** — Explicitly list what types EF Core (or your ORM) queries against
- [ ] **Spike before committing** — Write one specification against a domain type, then attempt to execute it against the persistence type. 2-hour timebox.
- [ ] **Apply the "2+ concrete use cases" rule** — Identify real scenarios where the pattern adds value over existing mechanisms
- [ ] **Estimate translation cost** — If domain and persistence types differ, what's the expression rewriting burden? Is it fragile?

## Decision Framework

### When IS the Specification Pattern Appropriate?

| Scenario | Fit |
|----------|-----|
| EF Core maps domain objects directly | Compatible — specs query the same types |
| In-memory filtering of loaded collections | Compatible — LINQ-to-Objects, no translation |
| Simple CRUD with domain-schema parity | Compatible — straightforward property mapping |
| Document DBs with domain-shaped documents | Compatible — if LINQ provider translates predicates |

### When is it NOT Appropriate?

| Scenario | Fit |
|----------|-----|
| **Memento-based persistence** | Incompatible — domain types aren't queried |
| Event sourcing (event stream only) | Incompatible — specs don't query events |
| Heavy ORM impedance (computed properties, unmapped fields) | Risky — silent translation failures |
| Speculative abstraction (no concrete use cases) | Premature — adds coupling without proven value |

## Related Documents

- [State Management: Memento Pattern ADR](../../adr/domain/20260324-1104-state-management-memento-pattern.md) — Memento contract that creates the type gap
- [Persistence Strategy ADR](../../adr/infrastructure/20260324-1229-persistence-strategy.md) — Repository and EF Core architecture
- [Domain Building Blocks ADR](../../adr/domain/20260324-1032-domain-building-blocks.md) — Entity, AggregateRoot base types
- [Cross-Cutting Infrastructure ADR](../../adr/infrastructure/20260324-1249-cross-cutting-infrastructure.md) — All cross-cutting concerns operate on mementos
- [TypedId Guid-Only Simplification](typedid-guid-only-simplification.md) — "Require 2+ concrete use cases" principle
- [DDD Concepts Brainstorm](../../brainstorms/20260322-2036-yaf-ddd-concepts-brainstorm.md) — Section 8: original specification concept
- [Rejected Specification Plan](../../plans/20260401-1641-feat-specification-pattern-plan.md) — Full plan with detailed design (status: rejected)
