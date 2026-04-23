# Development Roadmap

## Current State
- **Version**: 0.1.0 (pre-release)
- **Key Features**: TypedId, Entity, AggregateRoot, ValueObject, Error, Result, memento pattern, cross-cutting concern interfaces
- **Recent Updates**: Result pattern implementation, error source/encryption markers, base memento classes

## Planned Enhancements (Next 3-6 Months)

### High Priority
- [ ] **Infrastructure layer** — Persistence abstraction, repository pattern, unit of work `[Effort: L]`
- [ ] **EF Core adapter** — Concrete persistence implementation with migrations support `[Effort: L]`
- [ ] **ASP.NET Core API layer** — Controller patterns, mapping, middleware `[Effort: L]`
- [ ] **Example web application** — Reference implementation showcasing the framework `[Effort: M]`

### Medium Priority
- [ ] **NuGet package metadata** — Package icon, description, README for nuget.org `[Effort: S]`
- [ ] **Developer quick-start guide** — Getting started documentation `[Effort: S]`
- [ ] **Observability hooks** — Structured logging and distributed tracing integration points `[Effort: M]`
- [ ] **Domain event publishing** — Infrastructure for dispatching domain events `[Effort: M]`

### Technical Debt
- [ ] **Finalize ADRs** — Review and accept "under review" architecture decision records `[Effort: S]`
- [ ] **Sub-namespace organization** — Consider splitting Yaf.Domain root as it grows `[Effort: S]`
- [ ] **Test naming conventions** — Standardize test method naming patterns `[Effort: S]`

## Future Considerations
- **Additional DDD patterns**: Repository, UnitOfWork, DomainEventPublisher
- **Performance benchmarks**: Entity equality, event accumulation, Result pattern overhead
- **Multi-database support**: Beyond EF Core (Dapper, raw ADO.NET)
- **Containerization**: Docker support and cloud-ready deployment patterns

---
**Effort Scale**: `S`: 2-3 days | `M`: 1 week | `L`: 2+ weeks
