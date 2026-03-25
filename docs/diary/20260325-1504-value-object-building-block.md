# Session Diary: ValueObject Building Block

**Date:** 2026-03-25
**Branch:** `feat/domain-building-blocks`
**Commits:** 11

---

## Goal

Implement the first domain building block for YAF: the `ValueObject` type with two variants (marker and memento-capable), along with the `IMemento` interface contract.

## What Was Achieved

- **Solution scaffolding** — `.slnx` solution (moved to `src/`), `Yaf.Domain` class library, `Yaf.Domain.Tests` xUnit project
- **ValueObject marker** — `public abstract record ValueObject` for simple value semantics
- **ValueObject\<TSelf, TMemento\>** — memento-capable variant with `SnapshotCore`/`RestoreCore`/`Validate` template methods
- **IMemento\<TSelf, TMemento\>** — interface with `Snapshot` + `static abstract Restore`, implemented by the base record
- **IHydratable\<TMemento\>** — separate interface for future entity hydration
- **IError + ValidationException** — post-restore validation hook
- **9 passing tests** using AwesomeAssertions
- **.editorconfig** with CS1591 enforcement for XML documentation
- **.gitignore** — standard .NET/VS + NCrunch + Claude Code local settings
- **Solution documentation** capturing the CRTP + static abstract investigation trail
- **Plan document** with all acceptance criteria checked off
- **Memento ADR updated** to reflect the two-interface split and new contract shape

## The Process — What Made This Session Interesting

This session was less about writing code and more about **iterative design through conversation**. The amount of code is small (~150 LOC of production code, ~160 LOC of tests), but the design decisions behind it were significant and evolved substantially through human-AI dialogue.

### Phase 1: Plan before code

Started with `/workflows:plan`. This forced explicit design decisions early:
- Two ValueObject variants (marker vs memento-capable)
- The memento interface shape (`Snapshot`/`Restore` vs the ADR's `Snapshot`/`Hydrate`)
- Whether to split `Hydrate` into a separate interface

The human redirected several initial assumptions during planning — the plan was revised 4+ times before any code was written. This was time well spent.

### Phase 2: Iterative interface design

The `IMemento` interface went through multiple shapes:
1. Started as `ISnapshotable<TSelf, TMemento>` (separate from entity `IMemento`)
2. Human said: unify into `IMemento<TSelf, TMemento>`, drop the old ADR interface
3. Human said: add `Hydrate` back as an instance method
4. Human said: split `Hydrate` to `IHydratable` so value objects don't carry a throwing stub
5. Human said: the internal methods should delegate through public ones (`Snapshot` → `SnapshotCore`)

Each iteration was a small, clear directive that changed the design meaningfully. The AI's role was to implement each change, verify it compiles and tests pass, and surface any issues.

### Phase 3: Fighting C# limitations (incorrectly)

The biggest time sink was the `static abstract` interface dispatch investigation. The AI incorrectly concluded that abstract classes cannot satisfy `static abstract` interface members, based on:
- CS0112 (`static abstract` on a class member — correct, that's invalid)
- Extrapolating this to mean abstract classes can't implement interfaces with `static abstract` members — **wrong**

This led to workarounds: removing `IMemento` from the base, requiring consumers to declare it, adding explicit interface implementations. All unnecessary.

**The human caught this** with a direct instruction: "simply implement a static method that can work with generic arguments." When pushed further: "please inherit from IMemento — the code should work just fine." And it did.

**Root cause of the error:** The AI conflated CS8920 (can't use interface with `static abstract` as generic type argument — which hit in a test assertion) with a broader limitation on class-level interface implementation. Two different errors, two different scopes. Precision in reading compiler errors matters.

### Phase 4: Multi-agent code review

Ran 4 review agents in parallel (architecture, simplicity, security, patterns). This surfaced:
- `IHydrateable` misspelling → `IHydratable`
- `*Internal` → `*Core` naming convention
- Null guard gaps
- The `GetUninitializedObject` validation concern → led to the `Validate()` hook

The review process was efficient — parallel agents with independent perspectives, synthesized into prioritized findings, then the human triaged what to act on.

### Phase 5: Correcting the record

After the human corrected the `static abstract` assumption, we documented the investigation trail in `docs/solutions/` — capturing not just the solution but also what was incorrectly assumed and why.

## What Went Well

- **Human-driven design iteration** — the plan changed 4+ times before code was written, each time getting simpler and more correct
- **The human caught a fundamental assumption error** that 4 review agents missed
- **Multi-agent review** was efficient for catching naming, convention, and defensive coding issues
- **Small commits** (11 commits) made it easy to track the evolution
- **Solution documentation** while context was fresh

## What Went Wrong

- **Incorrect C# assumption** — spent significant time on workarounds for a limitation that doesn't exist. The AI was too confident in its conclusion and didn't try the simplest approach first.
- **Error conflation** — CS8920 (generic type argument) was generalized to mean abstract classes can't implement interfaces with `static abstract`. This is precisely the kind of error that wastes the most time: it's plausible, it's internally consistent, and it's wrong.
- **Review agents didn't catch it** — 4 agents reviewed the code and none questioned the fundamental assumption. The architecture agent even praised the workaround as "well-documented." This shows the limits of agent-based review when the agents share the same incorrect assumption as the code they're reviewing.

## Other Notes

- **AwesomeAssertions** — used instead of raw xUnit assertions, works well with fluent syntax
- **EditorConfig CS1591 enforcement** — duplicate block crept in during initial setup, caught by review
- The `GetUninitializedObject` approach is architecturally sound but requires `{ get; private set; }` properties — documented prominently
- `IHydratable` has no consumers yet but was kept as the interface contract is cheap and removing it later would be a breaking change for consumers who depend on the split

## Communication Assessment

**What was clear:**
- Human's directives were short and specific ("split the interface into 2", "rename to *Core", "make them private")
- When the AI went in the wrong direction, the human redirected immediately and directly ("your finding seems somewhat wrong — please inherit from IMemento")

**What was ambiguous:**
- Early in the session, "it must be abstract as it will declared at the domain level" was ambiguous — it could have referred to the value object or the memento type. The AI guessed wrong (made MementoColor abstract), the human corrected quickly.

**What could improve:**
- **AI should try the simplest approach before concluding something is impossible.** The `static abstract` limitation could have been disproven in one compile attempt.
- **AI should distinguish between "I tried this and it failed" vs "I believe this won't work based on my understanding."** The first is evidence; the second is a hypothesis that should be tested.
- **Human was effective at catching direction errors** — the "just try it" approach was exactly right and saved potentially hours of workaround engineering.
