# Research Brief: Automated Domain Event Collection with Memento Separation

## Research Question

How can domain events be automatically collected from aggregate roots when the infrastructure layer only interacts with mementos (DTOs), without requiring manual developer effort in every repository?

## Research Type

**Mixed** — combines technical codebase analysis (current YAF patterns) with literature/best-practices research (industry approaches to this problem).

## Context

YAF uses a memento pattern to separate domain objects from storage:
- `AggregateRoot<TId>` accumulates domain events via `AddDomainEvent()` (protected)
- `AggregateRoot<TId, TSelf, TMemento>` adds memento-based snapshot/restore
- Repositories map domain objects ↔ mementos for persistence
- EF Core only sees mementos (DTOs), NOT the domain objects themselves
- ADR `20260324-1113-domain-events-and-integration-events.md` mandates:
  - Explicit dispatch after SaveChanges, before CommitAsync
  - Events dispatched within the transaction (handler failures cause rollback)
  - Context envelope automatically attached

## The Problem

The domain event lifecycle (from ADR) assumes infrastructure can access the aggregate root's `DomainEvents` collection. But with memento separation:

1. **Repository.Save(aggregate)** → converts aggregate to memento → persists memento via EF Core
2. After step 1, the aggregate reference exists only in the repository method scope
3. The Unit of Work (which coordinates SaveChanges + event dispatch) has no reference to the original aggregate
4. Making every repository manually register its aggregates as event sources is error-prone and violates the "automated" goal

## Scope

### Included
- Domain event collection mechanisms
- Unit of Work patterns with event dispatch
- Repository patterns that track domain objects
- Infrastructure-level automation approaches
- .NET/C# specific patterns and libraries
- How other DDD frameworks handle this with non-ORM-mapped domain objects

### Excluded
- Integration events (deferred per ADR)
- Message broker implementation
- Event sourcing (different architectural choice)
- Changing the memento pattern itself

### Constraints
- Zero-dependency domain layer (YAF core constraint)
- Memento pattern must remain (architectural decision)
- Explicit dispatch per ADR (not implicit SaveChanges override)
- Must minimize developer effort — ideally zero additional code per repository

## Success Criteria

1. At least 3 distinct approaches identified with trade-offs
2. Each approach evaluated against: automation level, developer effort, testability, compatibility with memento pattern
3. Clear recommendation for YAF's specific constraints
4. Evidence-based findings with source citations
