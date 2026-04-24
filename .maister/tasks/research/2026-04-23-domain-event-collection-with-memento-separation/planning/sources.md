# Research Sources

## Codebase Sources

### File Patterns
- `src/Yaf.Domain/AggregateRoot*.cs` -- Aggregate root hierarchy with event collection
- `src/Yaf.Domain/Entity*.cs` -- Entity base classes
- `src/Yaf.Domain/Interfaces/IDomainEvent.cs` -- Domain event contract
- `src/Yaf.Domain/Interfaces/IMemento.cs` -- Memento interface
- `src/Yaf.Domain/Interfaces/IHydratable.cs` -- Hydration interface
- `src/Yaf.Domain/Interfaces/IMementoBase.cs` -- Memento base interface
- `src/Yaf.Domain/MementoBase.cs` -- Memento base implementation
- `src/Yaf.Domain/Helpers/MementoHelper.cs` -- Memento mapping utilities
- `src/Yaf.Domain/ValueObject{TSelf,TMemento}.cs` -- Value object memento pattern

### Key Files (Event Lifecycle)
- `src/Yaf.Domain/AggregateRoot.cs` -- `AddDomainEvent()`, `DomainEvents`, `ClearDomainEvents()` -- the event accumulation API
- `src/Yaf.Domain/AggregateRoot{TId,TSelf,TMemento}.cs` -- Memento variant with `Snapshot()` / `Restore()` -- the persistence boundary
- `src/Yaf.Domain/Interfaces/IDomainEvent.cs` -- Event contract with context envelope (ICorrelated, ITenantScoped, IActorScoped, IActivityScoped)

### Key Files (Cross-Cutting)
- `src/Yaf.Domain/Interfaces/IAccountable.cs` -- Audit trail
- `src/Yaf.Domain/Interfaces/IHasTimestamps.cs` -- Temporal tracking
- `src/Yaf.Domain/Interfaces/IHasSoftDelete.cs` -- Soft delete
- `src/Yaf.Domain/Interfaces/IHasVersionInfo.cs` -- Optimistic concurrency
- `src/Yaf.Domain/Interfaces/IValidatable.cs` -- Validation contract

### Directories
- `src/Yaf.Domain/` -- Domain layer root
- `src/Yaf.Domain/Interfaces/` -- All domain contracts
- `src/Yaf.Domain/Helpers/` -- Memento helper utilities
- `tests/Yaf.Domain.Tests/` -- Domain unit tests

## Documentation Sources

### ADRs (Authoritative)
- `docs/adr/domain/20260324-1113-domain-events-and-integration-events.md` -- Domain event dispatch lifecycle, explicit dispatch after SaveChanges before CommitAsync, context envelope, UoW coordination
- `docs/adr/domain/20260324-1032-domain-building-blocks.md` -- AggregateRoot owns event collection, entity hierarchy
- `docs/adr/architecture/20260324-1146-application-layer-patterns.md` -- Context providers, UoW mention, application service patterns

### Project Documentation
- `.maister/docs/project/architecture.md` -- System architecture, planned infrastructure layer
- `.maister/docs/project/roadmap.md` -- Infrastructure layer priority (EF Core adapter, UoW, event publishing)
- `.maister/docs/project/tech-stack.md` -- .NET 10, C# 12+, xUnit/AwesomeAssertions

### Standards
- `.maister/docs/standards/backend/domain-patterns.md` -- Entity encapsulation, memento pattern conventions, Result pattern
- `.maister/docs/standards/backend/csharp-conventions.md` -- C# coding conventions applicable to infrastructure code
- `.maister/docs/standards/global/minimal-implementation.md` -- Build only what is needed

## Configuration Sources
- `Directory.Build.props` -- Shared build settings, dependency constraints
- `.editorconfig` -- Code style rules (relevant for implementation guidance)
- `src/Yaf.Domain/Yaf.Domain.csproj` -- Domain project dependencies (should be zero)

## External Sources

### .NET DDD Reference Architectures
- Microsoft eShopOnContainers / eShop -- EF Core-based domain event dispatch pattern (uses ChangeTracker to find aggregates with events)
- Jason Taylor Clean Architecture template -- MediatR-based event dispatch
- Ardalis Clean Architecture template -- Domain event dispatching patterns
- Steve Smith (Ardalis) DDD fundamentals -- Repository + UoW patterns

### EF Core Mechanisms
- EF Core SaveChangesInterceptor documentation -- Hook into save pipeline
- EF Core ChangeTracker API -- Tracking entity state changes
- EF Core DbContext.SavingChanges / SavedChanges events

### DDD Pattern Resources
- Vaughn Vernon "Implementing Domain-Driven Design" -- Domain event patterns
- Jimmy Bogard domain events blog posts -- Dispatching strategies
- Kamil Grzybek "Modular Monolith" -- Domain event dispatch with UoW

### Key Pattern Keywords for Search
- "domain event collection without EF Core tracking"
- "domain events memento pattern repository"
- "aggregate root event dispatch unit of work .NET"
- "repository base class domain event registration"
- "domain event collector scoped service .NET"
