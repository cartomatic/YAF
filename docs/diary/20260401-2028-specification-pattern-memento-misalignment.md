# Session Diary: Specification Pattern & Memento Misalignment

**Date:** 2026-04-01
**Duration:** ~1 hour

---

## Goal

Plan the implementation of the Specification pattern (`ISpecification<T>`) for the `Yaf.Domain` layer — a composable, expression-based predicate abstraction for querying and filtering domain objects.

## What Was Achieved

- Full plan created at `docs/plans/20260401-1641-feat-specification-pattern-plan.md` (now status: rejected)
- Discovered a fundamental architectural tension between the Specification pattern and YAF's memento-based persistence model
- Made a deliberate decision NOT to implement the pattern, with clear rationale documented
- Created a solution document at `docs/solutions/design-patterns/specification-pattern-memento-incompatibility.md` capturing the insight for future reference

## What Went Well

- **The planning workflow caught an architectural mismatch before any code was written.** This is the ideal outcome — a 15-minute conversation during planning prevented what could have been days of implementation followed by the same realization.
- **Collaborative design decisions.** Asking the user about key design choices (Expression vs Func, fat vs thin interface, generic constraints) before writing the plan ensured alignment.
- **The user's architectural intuition was sharp.** They raised the memento-specification tension themselves ("let's discuss the specification in context of a memento object"), which led directly to the key insight.
- **Applying the project's own principles.** The "require 2+ concrete use cases" learning from the TypedId simplification was directly applicable and reinforced the decision.

## What Went Wrong

- **The initial planning flow proceeded too far before surfacing the architectural question.** The full plan (with phases, acceptance criteria, test plans) was written before the memento tension was discussed. Ideally, the persistence-layer compatibility question should have been raised during the idea refinement phase, not after the plan was complete.
- **Root cause:** The planning workflow's research phase focused on the domain layer in isolation. It identified that no specification pattern existed in the codebase but didn't proactively analyze whether the pattern was compatible with the persistence architecture documented in ADRs. The SpecFlow analyzer raised the expression-vs-memento question but framed it as a design choice rather than a potential blocker.
- **Overcomplicated the initial question flow.** The first few AskUserQuestion rounds were too generic ("New feature" → "Domain layer addition" → "Which pattern?"). The user already knew what they wanted — specification pattern. A more direct entry would have saved time.

## Planning & Review Analysis

### What Was Sound From the Start
- The technical design decisions (Expression-based, unconstrained T, layered interfaces, extension methods for composition) were all architecturally sound in isolation
- The SpecFlow analysis identified 10 important gaps and questions, several of which proved relevant to the final decision

### What Required Further Discussion
- The specification-to-repository integration — this was flagged by the SpecFlow analyzer but treated as a "to be resolved" rather than a potential deal-breaker
- The persistence model compatibility — this turned out to be the critical factor

### What Effective Changes Resulted
- The user's question about memento context led to a productive discussion that surfaced the real issue
- The decision to reject was well-reasoned and documented, turning a "failed plan" into a valuable architectural learning

## Other Notes

- **The persistence strategy ADR** (`docs/adr/infrastructure/20260324-1229-persistence-strategy.md`) documents `ISpecification<T>` as part of the repository contract. This ADR may need updating to reflect the tension discovered here — the specification concept in that ADR assumes domain-type queryability that memento persistence doesn't provide.
- **Future consideration:** If YAF ever needs composable domain predicates (e.g., for complex domain service filtering), a simpler `Func<T, bool>`-based approach without the Expression overhead might suffice, since in-memory evaluation doesn't need EF Core translation.
- **The "compound" documentation workflow** worked well for capturing this insight immediately while context was fresh.

## Communication Assessment

**What was clear:**
- The user's direction was consistently clear — they answered design questions decisively and raised the memento concern directly
- The user's correction ("the more I think about it, the more I lean towards a conclusion I do not need specification") was direct and well-timed

**What was ambiguous:**
- Nothing significant — the session had good back-and-forth

**What could improve:**
- **Claude side:** Should have proactively raised the memento-specification tension during research, before writing the full plan. The persistence ADR was available and documented the memento approach — cross-referencing it against the specification pattern's assumptions would have surfaced the issue earlier.
- **Process:** The planning workflow should include a "pattern-architecture compatibility check" step before detailed planning. Question to ask: "Does this pattern assume the ORM maps the same types the domain logic operates on?"
