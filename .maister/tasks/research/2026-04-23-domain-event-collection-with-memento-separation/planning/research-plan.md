# Research Plan: Automated Domain Event Collection with Memento Separation

## Research Overview

**Research Question:** How can domain events be automatically collected from aggregate roots when the infrastructure layer only interacts with mementos (DTOs), without requiring manual developer effort in every repository?

**Research Type:** Mixed (technical codebase analysis + literature/best-practices research)

**Scope:**
- Included: domain event collection mechanisms, UoW patterns with event dispatch, repository patterns that track domain objects, infrastructure-level automation, .NET/C# specific patterns
- Excluded: integration events, message brokers, event sourcing, changing the memento pattern itself
- Constraints: zero-dependency domain layer, memento pattern stays, explicit dispatch per ADR, minimize developer effort

**Sub-Questions:**
1. What is the current shape of YAF's aggregate root event collection, and where does the lifecycle gap occur?
2. What patterns exist for repositories to register aggregates as event sources with a Unit of Work?
3. Can a generic/abstract repository base class automate event source registration transparently?
4. How do DDD frameworks that separate domain objects from persistence models solve event collection?
5. What .NET-specific mechanisms (EF Core interceptors, DI scopes, middleware) could bridge the gap?
6. What are the trade-offs (testability, developer effort, coupling, complexity) of each approach?

---

## Methodology

### Primary Approach
Multi-strategy research combining:
1. **Codebase analysis** -- understand current YAF domain event, memento, and aggregate root infrastructure to identify the exact gap
2. **DDD pattern research** -- investigate how established DDD frameworks and reference architectures handle event collection when domain objects are not ORM-tracked
3. **.NET ecosystem research** -- explore .NET-specific mechanisms (EF Core interceptors, scoped services, generic repository bases) that could automate collection
4. **Industry pattern research** -- find how other projects with aggregate/persistence separation solve this problem

### Fallback Strategies
- If codebase patterns alone are insufficient, widen to open-source .NET DDD projects (eShopOnContainers, Clean Architecture templates)
- If no established pattern exists for memento-separated event collection, synthesize a novel approach from component patterns

### Analysis Framework
- **Component identification**: What infrastructure pieces are needed (UoW, repository base, event collector)
- **Pattern comparison**: How each approach handles the aggregate-to-UoW registration problem
- **Trade-off analysis**: Automation level vs. coupling vs. testability vs. developer effort
- **Compatibility assessment**: How each approach fits YAF's constraints (zero-dep domain, memento pattern, explicit dispatch)

---

## Data Sources

See `planning/sources.md` for detailed source manifest.

**Summary:**
- **Codebase sources**: AggregateRoot hierarchy, IDomainEvent, memento pattern files, cross-cutting interfaces (15+ files)
- **ADR sources**: Domain events ADR, application layer patterns ADR, domain building blocks ADR (3 files)
- **Architecture docs**: Project architecture, roadmap, tech stack (3 files)
- **External sources**: .NET DDD reference architectures, EF Core interceptor documentation, open-source DDD frameworks

---

## Research Phases

### Phase 1: Broad Discovery
**Goal:** Map the current event lifecycle gap precisely

**Actions:**
- Read AggregateRoot hierarchy to understand event accumulation API (AddDomainEvent, DomainEvents, ClearDomainEvents)
- Read memento pattern implementation to understand the snapshot/restore boundary
- Read domain events ADR to confirm dispatch lifecycle requirements
- Identify where the aggregate reference is lost in the repository-to-UoW flow
- Check if any UoW or repository interfaces/contracts exist yet

**Expected Output:** Clear documentation of the lifecycle gap -- where the aggregate exists, where events live, and where the reference is lost

### Phase 2: Targeted Reading
**Goal:** Understand specific patterns that could bridge the gap

**Actions:**
- Investigate generic repository base patterns that could auto-register aggregates
- Research UoW patterns that maintain an aggregate tracker / event source registry
- Examine EF Core ChangeTracker and interceptor mechanisms for hooking into save
- Look at how MediatR-based projects collect events (even though YAF is mediator-agnostic)
- Study IServiceScope patterns for scoped event collection

**Expected Output:** Catalog of candidate approaches with mechanism descriptions

### Phase 3: Deep Dive
**Goal:** Evaluate each candidate approach against YAF's constraints

**Actions:**
- For each approach: sketch the infrastructure code shape
- Evaluate: does it require manual code per repository? (automation level)
- Evaluate: does it add dependencies to the domain layer? (zero-dep constraint)
- Evaluate: does it work with memento separation? (memento constraint)
- Evaluate: does it support explicit dispatch before CommitAsync? (ADR constraint)
- Evaluate: testability -- can the event collection be unit-tested?

**Expected Output:** Comparison matrix with scored trade-offs

### Phase 4: Verification
**Goal:** Validate findings and identify recommended approach

**Actions:**
- Cross-reference findings across sources to confirm patterns
- Identify any gaps or inconsistencies in the analysis
- Validate that the recommended approach satisfies all success criteria
- Check for edge cases (multiple aggregates per UoW, nested transactions, aggregate loaded but not modified)

**Expected Output:** Final recommendation with rationale and edge-case analysis

---

## Gathering Strategy

### Instances: 4

| # | Category ID | Focus Area | Tools | Output Prefix |
|---|-------------|------------|-------|---------------|
| 1 | codebase | Current YAF domain event infrastructure: AggregateRoot event API, memento pattern boundary, cross-cutting interfaces, existing ADRs for dispatch lifecycle | Glob, Grep, Read | codebase |
| 2 | ddd-patterns | How DDD frameworks and reference projects handle domain event collection when domain objects are NOT directly ORM-tracked. Focus on: aggregate tracker patterns, UoW event source registries, repository base class patterns | WebSearch, WebFetch, Read | ddd-patterns |
| 3 | dotnet-ecosystem | .NET-specific mechanisms for bridging the gap: EF Core SaveChanges interceptors, scoped service patterns, generic repository bases in .NET, MediatR-style event collection (as reference, not dependency) | WebSearch, WebFetch, Read | dotnet-ecosystem |
| 4 | external-approaches | Industry patterns for event collection with aggregate/persistence separation: domain event outbox without ORM tracking, event collector services, middleware-based approaches, change-tracker-independent patterns | WebSearch, WebFetch, Read | external-approaches |

### Rationale
The four categories are chosen to cover the full research spectrum:
- **codebase** is essential to understand the exact constraints and gap in YAF's current implementation
- **ddd-patterns** targets the core architectural question -- this is fundamentally a DDD infrastructure pattern problem
- **dotnet-ecosystem** focuses on .NET-specific mechanisms since YAF is a .NET framework, and solutions must use .NET infrastructure
- **external-approaches** widens the net to find patterns that may not appear in standard DDD or .NET literature but solve the same fundamental problem

Separating DDD patterns from .NET ecosystem avoids overlap: DDD patterns are language-agnostic design patterns, while .NET ecosystem research focuses on specific framework APIs and libraries.

---

## Success Criteria

1. **At least 3 distinct approaches identified** -- each with a clear mechanism description
2. **Each approach evaluated against 4 dimensions:**
   - Automation level (zero manual code per repository = best)
   - Developer effort (one-time setup vs. per-repository boilerplate)
   - Testability (can event collection be unit-tested independently?)
   - Compatibility with memento pattern (works without ORM tracking domain objects)
3. **All approaches validated against ADR constraints:**
   - Zero-dependency domain layer preserved
   - Explicit dispatch after SaveChanges, before CommitAsync
   - Memento separation intact
4. **Clear recommendation** for YAF with rationale tied to evidence
5. **Edge cases addressed:** multiple aggregates per UoW, aggregate loaded but not modified, nested save operations

---

## Expected Outputs

1. **Research report** (`analysis/findings/`) -- per-category findings from each gatherer
2. **Synthesis report** -- combined analysis with comparison matrix
3. **Recommendation** -- selected approach with implementation sketch and rationale
4. **ADR draft** (if warranted) -- architectural decision record for the chosen event collection pattern
