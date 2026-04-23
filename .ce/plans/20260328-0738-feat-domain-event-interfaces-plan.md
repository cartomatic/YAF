---
title: "Domain event interfaces with cross-cutting context"
type: feat
status: completed
date: 2026-03-28
depends-on: 20260328-0738-refactor-typedid-guid-only-simplification-plan
---

# feat: Domain event interfaces with cross-cutting context

## Overview

Enrich `IDomainEvent` from a marker interface to a full contract carrying identity, timestamp, and cross-cutting context (correlation, tenant, user, activity). Introduce three new cross-cutting interfaces (`ICorrelated`, `IUserScoped`, `IActivityScoped`) and a generic `IDomainEvent<out T>` for events that carry a data payload.

## Problem Statement / Motivation

The current `IDomainEvent` is a marker interface with no members. The existing ADR (`20260324-1113`) specifies that context envelope (TenantId, IdentityId, CorrelationId, ActivityId, OccurredAtUtc) is attached by infrastructure at dispatch time.

This plan moves context **onto the event itself** because:

1. **Self-describing events** — An event should carry everything needed to understand it. A domain event without context is incomplete; consumers always need the context alongside the event data.
2. **Simpler infrastructure** — When the event carries its own context, dispatch infrastructure doesn't need to wrap events in an envelope or maintain a parallel context-attachment pipeline.
3. **Type safety** — Interfaces enforce that every event has the required context at compile time. No runtime "did infrastructure attach the envelope?" checks.
4. **Serialization-friendly** — Events with their context are self-contained for logging, auditing, event stores, and eventual integration event mapping.

**ADR amendment:** This plan supersedes the "context attached at dispatch time" decision in ADR `20260324-1113`. Context is now part of the event contract. Infrastructure still *populates* the values (from context providers), but the event *carries* them.

## Proposed Solution

### New cross-cutting interfaces

```
ICorrelated          — Guid CorrelationId
IUserScoped          — Guid UserId
IActivityScoped      — string? ActivityId
```

These are standalone, reusable interfaces — not domain-event-specific. Commands, queries, and integration events can implement them too.

### Enriched IDomainEvent

```
IDomainEvent : ICorrelated, ITenantScoped<TenantId>, IUserScoped, IActivityScoped
    Guid EventId
    DateTimeOffset OccurredAtUtc
```

Uses the framework's `TenantId` type for `ITenantScoped` (not generic — domain events use the YAF default tenant ID). `ICorrelated` and `IActivityScoped` use simple types (Guid, string) because correlation and activity IDs are operational/infrastructure concerns, not domain identities.

### Generic data variant

```
IDomainEvent<out T> : IDomainEvent
    T Data
```

Covariant `out T` enables polymorphic event handling (e.g., `IDomainEvent<object>` can hold any `IDomainEvent<OrderData>`).

## Technical Considerations

### Why ICorrelated, IUserScoped, IActivityScoped use simple types (not TypedId)

- **CorrelationId** is a W3C/OpenTelemetry concept — always a Guid. Not a domain entity identity.
- **UserId** on an event is the actor who triggered the operation — a plain Guid suffices. The domain entity (`IAccountable<TActorId>`) keeps typed IDs for compile-time safety within the aggregate, but events cross aggregate boundaries.
- **ActivityId** maps to `System.Diagnostics.Activity.Id` — a string in .NET. Nullable because not all contexts have an active trace.

### Why ITenantScoped uses TenantId (not Guid)

Domain events stay within a bounded context where the framework's `TenantId` type is available. This provides type safety without generic explosion. After the TypedId simplification (Plan 1), `TenantId.Value` is a `Guid` directly.

### AggregateRoot compatibility

`AggregateRoot<TId>` stores `List<IDomainEvent>?`. The enriched `IDomainEvent` is still an interface — no change to the collection type. `AddDomainEvent(IDomainEvent)` still works. The aggregate raises events; infrastructure populates context fields before or after add (implementation detail for a future infrastructure plan).

### Relationship to IAccountable

`IUserScoped` (who triggered this event/operation) is **not** the same as `IAccountable` (who created/modified an entity). They may carry the same user ID in many cases, but they are semantically distinct:

- `IAccountable.CreatedBy` — persisted on the entity, set by infrastructure during SaveChanges
- `IUserScoped.UserId` — on the event, identifies who caused the event to be raised

## Acceptance Criteria

- [x] `ICorrelated` interface: `Guid CorrelationId { get; }` in `Yaf.Domain.Interfaces`
- [x] `IUserScoped` interface: `Guid UserId { get; }` in `Yaf.Domain.Interfaces`
- [x] `IActivityScoped` interface: `string? ActivityId { get; }` in `Yaf.Domain.Interfaces`
- [x] `IDomainEvent` enriched with: `Guid EventId`, `DateTimeOffset OccurredAtUtc`, inherits `ICorrelated`, `ITenantScoped<TenantId>`, `IUserScoped`, `IActivityScoped`
- [x] `IDomainEvent<out T>` extends `IDomainEvent` with `T Data { get; }`
- [x] Existing `AggregateRoot<TId>.AddDomainEvent(IDomainEvent)` still compiles and works
- [x] Existing domain event tests updated for new interface shape
- [x] New unit tests for each new interface
- [x] New unit tests for `IDomainEvent<T>` covariance
- [x] XML doc comments on all public API (CS1591 enforced)
- [x] ADR `20260324-1113` updated to reflect context-on-event decision

## Success Metrics

- Three new cross-cutting interfaces available for reuse across commands/queries/events
- Domain events are self-describing — carry all context needed for tracing, auditing, and replay
- `IDomainEvent<out T>` enables type-safe event payloads with covariant polymorphism
- No breaking changes to `AggregateRoot` event collection API

## Dependencies & Risks

**Dependencies:**

| Dependency | Status | Impact |
|-----------|--------|--------|
| Plan 1: TypedId Guid-only simplification | Pending | `ITenantScoped<TenantId>` on IDomainEvent requires simplified TypedId (otherwise TenantId carries `TypedId<Guid>` generic noise) |

**Risks:**

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Existing test events (`record OrderPlaced : IDomainEvent`) break | Certain | Low | Tests must implement new members — compiler-guided, straightforward |
| `IDomainEvent` becomes too heavy for simple in-process events | Low | Medium | Context is the minimum needed for traceability — five fields is not excessive |
| Covariance constraint on `IDomainEvent<out T>` limits `T` usage | Low | Low | `out T` only restricts `T` to output positions — events are read-only by nature |

## Implementation Notes

### Suggested order

1. **New interfaces** — `ICorrelated.cs`, `IUserScoped.cs`, `IActivityScoped.cs`
2. **Enrich IDomainEvent** — add properties, inherit new interfaces + `ITenantScoped<TenantId>`
3. **Add IDomainEvent<out T>** — generic data variant
4. **Update tests** — existing event test types, new tests for each interface
5. **Update ADR** — amend `20260324-1113` context envelope section

### Files affected

**Source (new):**
- `src/Yaf.Domain/Interfaces/ICorrelated.cs`
- `src/Yaf.Domain/Interfaces/IUserScoped.cs`
- `src/Yaf.Domain/Interfaces/IActivityScoped.cs`

**Source (modify):**
- `src/Yaf.Domain/Interfaces/IDomainEvent.cs` — from marker to full contract

**Source (unchanged):**
- `src/Yaf.Domain/AggregateRoot.cs` — `List<IDomainEvent>` still works
- `src/Yaf.Domain/Entity.cs` — no event involvement

**Tests (modify):**
- `tests/Yaf.Domain.Tests/AggregateRootTests.cs` — test event records need new members

**Tests (new):**
- `tests/Yaf.Domain.Tests/ICorrelatedTests.cs` (or grouped in a cross-cutting test file)
- `tests/Yaf.Domain.Tests/IDomainEventTests.cs`

**Docs (modify):**
- `docs/adr/domain/20260324-1113-domain-events-and-integration-events.md` — context envelope amendment

## References & Research

### Internal References

- `src/Yaf.Domain/Interfaces/IDomainEvent.cs` — current marker interface
- `src/Yaf.Domain/AggregateRoot.cs:24-58` — domain event collection and AddDomainEvent
- `src/Yaf.Domain/Interfaces/ITenantScoped.cs` — pattern reference for domain-side interface
- `docs/adr/domain/20260324-1113-domain-events-and-integration-events.md` — current ADR (to be amended)
- `docs/adr/infrastructure/20260324-1252-observability.md` — context providers (CorrelationId, ActivityId sources)

### Related Work

- Prerequisite: `docs/plans/20260328-0738-refactor-typedid-guid-only-simplification-plan.md`
- Previous: `docs/plans/20260327-0936-feat-cross-cutting-domain-interfaces-plan.md` — established cross-cutting interface patterns
