# Session Diary: Domain Event Collection Research

**Date:** 2026-04-23 / 2026-04-24
**Duration:** ~2 hours
**Branch:** `docs/domain-event-collection-research`

## Goal

Research how to automatically collect domain events from aggregate roots when the infrastructure layer only interacts with mementos (DTOs), not domain objects. The memento pattern separates domain objects from storage, so EF Core's ChangeTracker cannot see domain events. The aim was to find approaches that minimize developer effort — ideally fully automated with zero per-repository boilerplate.

## What Was Achieved

### Research Phase (maister:research workflow)
- Completed full 6-phase research orchestrator workflow
- 4 parallel information gatherers investigated: YAF codebase, DDD literature (8 patterns), .NET ecosystem (8 mechanisms), industry approaches (11 patterns)
- Cross-referenced synthesis identified strong convergence (4/4 sources) on a single solution class
- Brainstorming skipped (research already converged — no competing viable approaches)

### Key Finding: The "Lost Reference" Problem
The fundamental challenge: after a repository converts an aggregate to a memento for EF Core, the aggregate reference (which carries domain events) is lost. Every compatible solution works by maintaining an additional reference to the aggregate somewhere in the infrastructure layer's scoped lifetime.

### Recommended Architecture: Three-Component Composition
1. **Scoped `IAggregateTracker`** — holds aggregate root references within a UoW scope
2. **`MementoRepository<TAgg,TId,TMem>` base class** — auto-tracks on every load/add/save
3. **Custom `IDomainEventDispatcher`** — mediator-agnostic, resolves handlers from DI

### Design Phase
- High-level architecture design with C4 Level 1/2 diagrams
- Complete data flow diagram (load → event raise → save → dispatch → clear → commit)
- 5 MADR-format architectural decision records (tracking vs. copying, base class vs. decorator, interface placement, dispatcher choice, dispatch timing)
- 3 concrete examples (order placement, multi-aggregate UoW, unmodified aggregate)

### Artifacts Produced
All under `.maister/tasks/research/2026-04-23-domain-event-collection-with-memento-separation/`:
- `planning/` — research brief, plan, sources inventory
- `analysis/findings/` — 4 category-specific findings documents
- `analysis/synthesis.md` — cross-referenced analysis with confidence assessments
- `outputs/research-report.md` — comprehensive research report
- `outputs/high-level-design.md` — architecture design
- `outputs/decision-log.md` — 5 ADRs

## What Went Well

- **Strong convergence across sources** — all 4 independent research streams arrived at the same answer, giving high confidence (90-95%) in the recommendation
- **Parallel gathering was efficient** — 4 agents ran simultaneously, each investigating different angles
- **Domain layer completeness validated** — the existing `AggregateRoot<TId>` API (`AddDomainEvent`/`DomainEvents`/`ClearDomainEvents`) needs zero changes
- **User feedback on design was minimal** — only one minor correction (DbSet property → DbContext access), indicating the research and design were well-aligned with expectations

## What Went Wrong

### Committed directly to main
After completing the research, I committed directly to `main` instead of creating a feature branch first. The user caught this immediately. I undid the commit (`git reset --soft HEAD~1`), created branch `docs/domain-event-collection-research`, and recommitted there. **Root cause:** I didn't check the current branch before committing, despite the project using a branch-based PR workflow (documented in contributing conventions). Saved a feedback memory to prevent recurrence.

## Planning & Review Analysis

- The research plan's 4-category gathering strategy (codebase, DDD patterns, .NET ecosystem, external approaches) was well-scoped — each category contributed unique insights while cross-validating others
- Brainstorming was correctly identified as low-value given the strong convergence — skipping it saved time without losing alternatives
- The design phase produced actionable output — the component architecture, interface signatures, and data flow are ready to feed into a development workflow
- One design refinement from user review: removing the abstract `DbSet` property in favor of exposing `DbContext` directly — a pragmatic improvement for repositories needing contextual queries

## Communication Assessment

- **Clear from the user:** The research question was well-articulated with specific constraints (memento separation, automation goal, ADR requirements). The single design feedback was precise and actionable.
- **Could improve (Claude):** Should have proactively created a branch before committing — this is a basic workflow step documented in the project's contributing conventions. The branch-based workflow was visible in the git history and CLAUDE.md but I overlooked it in the commit flow.
- **Well-handled:** Phase gate questions were concise and gave the user control over optional phases (brainstorming, design). The user's "a couple of minor comments" → "sorry, ended up with one" exchange was handled without friction.

## Other Notes

- The standard .NET pattern (EF Core ChangeTracker scanning for entities with domain events) is definitively incompatible with memento separation — this is the central insight that makes YAF's approach novel
- No widely-documented open-source .NET framework specifically solves domain event collection for memento-separated architectures — YAF would be implementing a somewhat novel (but well-grounded) combination of known patterns
- Open questions deferred to implementation: cascading event dispatch, cross-aggregate event ordering, integration event conversion timing
