# Session Diary: YAF Library Design & DDD Concepts Brainstorm

**Date:** 2026-03-22
**Duration:** ~2.5 hours
**Branch:** `docs/yaf-library-design-brainstorm`

## Goal

Research .NET 10 web API best practices and brainstorm the design of YAF as a backbone library — first at the package/architecture level, then drilling into DDD concepts and their layer assignments.

## What Was Achieved

### Research Phase
- Launched 3 parallel research agents: repo analyst, .NET 10 best practices researcher, and installed skills explorer
- Best practices researcher produced a comprehensive research document covering Clean Architecture, DDD, Minimal APIs, CQRS, async messaging, containerization, and library design patterns (`docs/research/20260322-0935-dotnet10-web-api-framework-best-practices.md`)
- A fourth research agent was later launched specifically for DDD tactical patterns beyond the user's initial list

### Brainstorm 1: Library Design (`20260322-1755-yaf-library-design-brainstorm.md`)
- Established the "layered: opinionated core + optional modules" approach
- Defined 5 core packages: Yaf.Domain, Yaf.Application, Yaf.Infrastructure, Yaf.Api, Yaf.ServiceDefaults
- Key technology choices: Wolverine (not MediatR) as first CQRS adapter, EF Core for persistence, Aspire service defaults as separate package
- Package dependency graph explicitly documented
- Controllers first, Minimal APIs as future module
- xUnit + TestContainers for testing

### Brainstorm 2: DDD Concepts (`20260322-2036-yaf-ddd-concepts-brainstorm.md`)
- Catalogued 22 domain concepts across Domain/Application/Infrastructure layers
- Notable design decisions:
  - Memento pattern with infrastructure-driven state snapshots (infra passes memento to domain, not the other way around)
  - Context envelope (tenant, identity, correlation, activity) automatically on all events
  - Graveyard table instead of per-table soft delete
  - Custom Result<T> in Domain (zero external dependencies)
  - Policies as first-class corrective business rules
  - Generic Builder<T> for fluent domain object construction
  - Error hierarchy with auto-discovery via IErrorSource
  - Multi-tenancy from day one

### Outputs
- 2 brainstorm documents committed
- 1 research document committed
- Changelog updated
- All on `docs/yaf-library-design-brainstorm` branch (2 commits)

## What Went Well

- **Parallel research agents** worked effectively. Launching 3 agents simultaneously (repo, best practices, skills) saved significant time. The best practices agent took ~6 minutes but produced a thorough, well-sourced document while dialogue continued with the user.
- **One question at a time** approach kept the conversation focused. The user gave rich, detailed answers when not overwhelmed with multiple questions. Several answers included unexpected nuances (e.g., the memento inversion, graveyard table pattern) that wouldn't have surfaced with multiple-choice alone.
- **The user's "Other" responses were the most valuable.** When presented with options, the user frequently chose "Other" and provided a more nuanced answer than any option offered. This happened for: CQRS approach (custom abstractions), packaging (Clean Architecture-aligned names), memento design (infrastructure-driven), error catalog (IErrorSource marker), accountability IDs (generic typed), soft delete (graveyard table), and versioning (time-travel via mementos). The brainstorm workflow's multiple-choice format was a good starting point, but the real design emerged from the user's custom responses.
- **Document review caught real issues.** The review phase identified the missing dependency graph (critical for planning), the Aspire packaging ambiguity, and the Result<T> ownership question. These would have caused confusion during implementation.
- **Research agent timing.** The DDD research agent ran in the background while dialogue continued. When it returned, its findings (specifications, smart enums, policies, multi-tenancy, Result pattern) complemented the user's list well and prompted useful additions.

## What Went Wrong

- **Section renumbering was tedious.** When inserting Policies as section 10, I had to manually renumber 12 sections one by one. Should have rewritten the entire concept catalog section in one edit rather than individual find-and-replace operations. This was wasteful — 12 edits where 1 would have sufficed.
- **MediatR → Wolverine replacement was messy.** Using `replace_all` for `Yaf.Application.MediatR` caught the package name but created an artifact (`Yaf.Application.Wolverine | Wolverine adapter for CQRS + messaging` duplicated with the original). Had to do cleanup passes. A more careful targeted approach would have been cleaner.
- **Research agent for DDD patterns took too long.** The best practices agent took ~6.5 minutes and the DDD patterns agent took ~2.5 minutes. During the wait, I asked clarifying questions which was productive, but the user had to wait at one point while I checked the output file multiple times. Could have been more transparent about the wait.
- **Initial memento design missed the encapsulation point.** I proposed `ToMemento()` / `FromMemento()` (domain produces memento), but the user correctly pointed out this doesn't preserve encapsulation — infrastructure can't grab data out of a domain object. The user's insight (infrastructure passes a memento for domain to populate) is architecturally superior. I should have caught this tension during the initial categorization.

## Other Notes

- The user prefers record-based typed IDs and is clear that EF Core mapping should go through mementos, not directly on domain objects. This is an unconventional but well-reasoned approach — it means Yaf.Domain truly has zero ORM coupling.
- The "graveyard table" pattern for deletions is unusual but elegant. Main tables stay clean, deleted data is recoverable, and it pairs naturally with the memento pattern (serialize the memento at deletion time).
- The user explicitly chose "everything in v1" for the DDD concepts scope. This is ambitious (22 concepts) but reasonable for a framework — the foundation needs to be complete.
- Wolverine was chosen over MediatR mid-session (initially MediatR was selected, then the user changed their mind during review). The brainstorm documents were updated accordingly.
- The user's concept of "business event log" is distinct from domain events — it's an append-only activity stream for business visibility, not a reactive mechanism. Good to keep this distinction sharp during planning.

## Communication Assessment

**What was clear:**
- The user's initial feature description was comprehensive — a bullet-point list of 16 concepts with brief descriptions. This gave a strong starting point.
- The user's corrections were direct and specific. When something was wrong (memento direction, MediatR→Wolverine), the user stated exactly what to change and why.
- The "Other" responses with detailed notes were the richest source of design decisions. The user thinks architecturally and provides rationale with their choices.

**What was ambiguous:**
- The initial memento pattern description ("mementos should be used to offload and restore state; expected mementos should be defined by domain objects") was open to interpretation. It took a follow-up question to clarify the dependency direction. The phrase "defined by domain objects" could mean "domain defines the type" or "domain defines the contract" — the user meant the latter.
- "Business events" initially sounded like a reactive event type. The user's rephrasing to "businessEventLog" made the intent much clearer — it's a log, not an event bus.

**What could improve:**
- **Claude side:** I should have questioned the memento encapsulation earlier — the `ToMemento()` pattern I proposed contradicts the "domain is fully encapsulated" philosophy stated in the design. The tension was there to see; I just didn't catch it.
- **Claude side:** Bulk text replacements (MediatR → Wolverine) need more care. Should preview the changes before committing them.
- **Human side:** Nothing significant — the user was engaged, provided rich context, and redirected promptly when needed. The session flowed well.

**Overall:** This was a highly productive session. The brainstorm workflow's structure (research → dialogue → capture → review) worked well for this scope. The one-question-at-a-time approach respected the user's attention while still covering significant ground. Two substantial design documents produced in ~2.5 hours is a good pace for foundational architecture work.
