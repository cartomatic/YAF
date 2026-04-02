# Specification Audit: Result\<T\> Pattern with IError Integration

**Plan:** `docs/plans/20260402-1808-feat-result-pattern-plan.md`
**ADR:** `docs/adr/domain/20260324-1140-result-and-error-pattern.md` (status: under review)
**Audit type:** Pre-implementation specification review
**Date:** 2026-04-02
**Compliance status:** Mostly Compliant -- specification is largely clear and implementable, with several items requiring clarification before coding begins.

---

## Summary

The specification is well-structured, with clear acceptance criteria, a thorough API surface definition, and comprehensive test requirements. The existing codebase supports the described integration points (`IError`, `IValidatable`, `Error` record). However, I identified several ambiguities and gaps that should be resolved before implementation to avoid rework.

The most significant issues are: (1) underspecified equality semantics for `Result` failure comparison, (2) missing specification for `operator ==`/`operator !=` overloads despite `IEquatable<T>` requirement, (3) ambiguous `default(Result)` accessor behavior, and (4) the ADR that underpins this spec is still "under review."

---

## Critical Issues

None.

---

## High Severity Findings

### H1: ADR Status Is "Under Review" -- Spec Builds on Unapproved Decision

**Spec reference:** Line 24 -- references ADR at `docs/adr/domain/20260324-1140-result-and-error-pattern.md`

**Evidence:** ADR file line 3: `- **Status:** under review`

**Category:** Ambiguous

**Severity:** High -- The spec explicitly derives its drivers and rationale from this ADR. If the ADR is revised or rejected, the spec's foundation changes. Implementing against an unapproved ADR risks building something that needs to be redesigned.

**Recommendation:** Confirm the ADR is approved (or explicitly accepted "as-is" for this phase) before implementation begins.

---

### H2: Equality Semantics for Failure Results Are Underspecified

**Spec reference:** Line 144 -- "Failure: sequence equality on the errors array"

**Evidence:** The spec says failure equality uses "sequence equality on the errors array." But `IError` is an interface. The spec does not state what equality comparison is used for individual `IError` elements in the sequence.

The concrete `Error` type is a `sealed record` (`src/Yaf.Domain/Error.cs:9`), which gets compiler-generated value equality. However, `IError` itself has no `IEquatable<IError>` constraint (`src/Yaf.Domain/Interfaces/IError.cs`). A third-party `IError` implementation without value equality would use reference equality by default.

The test plan (line 213) says "Two failure results with same errors -- equal" but does not specify whether "same errors" means same instances, or structurally identical `IError` objects.

**Category:** Ambiguous

**Severity:** High -- Gets the wrong answer silently if the implementer picks the wrong comparison strategy. Affects correctness of `Equals` and `GetHashCode`.

**Questions for stakeholder:**
1. Should failure equality compare `IError` elements using `object.Equals` (which delegates to the concrete type's equality)?
2. Or should it compare by `Code` + `Message` string values (structural equality regardless of implementation type)?
3. Should the spec mandate `EqualityComparer<IError>.Default` and document that consumers with custom `IError` implementations need proper `Equals`/`GetHashCode`?

---

### H3: Missing Specification for `operator ==` and `operator !=`

**Spec reference:** Lines 45-63 -- API surface for `Result<T>` shows `IEquatable<Result<T>>` but no `==`/`!=` operators.

**Evidence:** The project's existing `TypedId` is a record (`src/Yaf.Domain/TypedId.cs:20`) which gets `==`/`!=` from the compiler. But `Result<T>` is a `readonly struct`, not a record struct. For a regular struct implementing `IEquatable<T>`, the compiler does NOT generate `==`/`!=` operators. Without explicit overloads, `result1 == result2` will not compile.

The equality test plan (lines 211-218) tests equality but does not specify whether `==`/`!=` operators should be testable.

**Category:** Incomplete

**Severity:** High -- Implementing `IEquatable<Result<T>>` without `==`/`!=` operator overloads would make the equality API inconsistent and surprising. Users would have to call `.Equals()` instead of using `==`.

**Recommendation:** Add `operator ==` and `operator !=` to the API surface for both `Result<T>` and `Result`, and add tests for them.

---

### H4: `default(Result)` Non-Generic Accessor Behavior Is Ambiguous

**Spec reference:** Line 157-158 -- acceptance criteria say `default(Result<T>)` has `IsFailure=true`, all accessors throw. Line 203-204 -- test says `default(Result)` has `IsFailure=true`, `Error` throws, `Errors` throws.

**Evidence:** The spec says `default(Result<T>)` treats a null internal array as "uninitialized = failure" (line 127). For the generic case, `.Value` throwing makes sense (there is no value). For `.Error`/`.Errors` throwing, this means a `default` failure result has no errors accessible at all -- it is a "failure without error information."

The test spec (line 203) says `.Error` throws and `.Errors` throws on `default(Result)`. But the acceptance criteria on line 157 say "all accessors throw" which is consistent.

The ambiguity: is `.IsFailure` returning `true` while `.Error`/`.Errors` throw `InvalidOperationException` the intended behavior? This creates a paradoxical state: "I am a failure, but you cannot know why." The spec acknowledges this in line 249 ("uninitialized struct fields silently become failures") but does not address what exception message to show, or whether this is truly "failure" vs a distinct "uninitialized" state.

**Category:** Ambiguous

**Severity:** High -- Consumers checking `IsFailure` and then accessing `.Errors` will get an exception on default-initialized results, which is a trap. The spec should be explicit about this being intentional and what the exception message should communicate.

**Questions for stakeholder:**
1. Is the intent that `default(Result<T>)` is a "poisoned" value that throws on ALL property access except `IsSuccess`/`IsFailure`? If so, this should be stated more explicitly.
2. Should there be a separate `IsDefault` or `IsInitialized` property so consumers can distinguish "failed with errors" from "uninitialized"?
3. What should the `InvalidOperationException` message say for default-value error access?

---

## Medium Severity Findings

### M1: `Array.AsReadOnly()` vs Direct Array Cast -- Spec Is Contradictory

**Spec reference:** Line 125 -- "wrapped in `IReadOnlyList<IError>` via `Array.AsReadOnly()` or direct array cast"

**Evidence:** These two approaches have different runtime behaviors:
- `Array.AsReadOnly()` returns a `ReadOnlyCollection<T>` wrapper (extra allocation, truly read-only at runtime).
- Direct array cast (`(IReadOnlyList<IError>)array`) costs nothing but the array is still mutable via a cast back to `IError[]`.

The spec says "or" without picking one. The key design decisions table (line 101) says "Immutable, no external mutation" which favors `Array.AsReadOnly()`, but the performance section (line 136) implies minimal allocations.

**Category:** Ambiguous

**Severity:** Medium -- For a `readonly struct` that never exposes the array reference externally (only through `IReadOnlyList<IError>` property), the direct cast is likely sufficient since the struct owns the only reference. But the spec should pick one approach.

**Recommendation:** Specify the direct `IError[]` to `IReadOnlyList<IError>` cast (the array IS the `IReadOnlyList<IError>` -- `T[]` implements `IReadOnlyList<T>` in .NET). No `Array.AsReadOnly()` wrapper needed. Document the rationale: the struct is the sole owner of the array, and it is never mutated after construction.

---

### M2: No Specification for `Result<T>` with Value Types and `default(T)` Ambiguity

**Spec reference:** Line 102 -- "Null value on success: `ArgumentNullException` for reference types"

**Evidence:** The spec says null is rejected for reference types on success. But it says nothing about value types. For `Result<int>`, `Result.Success(0)` and `Result.Success(default(int))` should both be valid successes. This is likely the intended behavior (since value types cannot be null), but the spec should be explicit.

More importantly: how does the implementation distinguish "success with `default(T)`" from "uninitialized struct" internally? If the struct stores `T _value` and `IError[] _errors`, then for `default(Result<int>)`: `_value` is `0` and `_errors` is `null`. The spec says null `_errors` means failure (line 127). So `default(Result<int>)` correctly becomes failure even though `_value` happens to be `0`. This works, but only if the success/failure state is NOT determined by examining `_value`.

**Category:** Incomplete

**Severity:** Medium -- The design likely works correctly, but the spec should explicitly state that state discrimination is based solely on the errors array (null = default/failure, non-null could mean... what for success?). See M3 below.

---

### M3: Internal State Representation Not Fully Specified

**Spec reference:** Line 101 -- "Internal error storage: `IError[]` array"
Line 127 -- "null array = uninitialized = treated as failure"

**Evidence:** The spec implies the internal state is determined by whether the `IError[]` is null or not. But it does not explicitly state the full internal field layout. A reasonable implementation would be:

```
private readonly T _value;
private readonly IError[]? _errors;  // null = default (failure), non-null = explicit state
```

But this raises a question: for success, is `_errors` set to an empty array, or to null? If null means failure, then success must set `_errors` to something non-null (e.g., `Array.Empty<IError>()`). This is an implementation detail, but since the spec discusses internal storage explicitly (line 101, 127), it should be complete.

**Category:** Incomplete

**Severity:** Medium -- An implementer could reasonably choose different internal representations. The spec should clarify: success sets `_errors` to `Array.Empty<IError>()` (or some sentinel), failure sets it to a non-empty array, and `null` is the default/uninitialized state.

**Recommendation:** Add a brief internal state table:

| State | `_errors` value | `_value` value |
|-------|----------------|---------------|
| Success | `Array.Empty<IError>()` | The value |
| Failure | Non-empty `IError[]` | `default(T)` |
| Default (uninitialized) | `null` | `default(T)` |

---

### M4: `ToString()` Behavior for `default(Result<T>)` Is Not Specified

**Spec reference:** Line 224 -- test says "Default -- reasonable representation" with no concrete format.

**Evidence:** The spec defines `ToString()` formats for success (`"Success(value)"`) and failure (`"Failure(code1, code2, ...)"`) but for the default/uninitialized state it only says "reasonable representation." Since `default` is treated as failure but has no errors, neither the success nor failure format applies cleanly.

**Category:** Incomplete

**Severity:** Medium -- Without a defined format, different implementations could produce different strings. Suggest specifying `"Result<T>(Uninitialized)"` or `"Failure(Uninitialized)"` or similar.

**Recommendation:** Pick a concrete `ToString()` format for default values. For example: `"Failure(Uninitialized)"`.

---

### M5: Null Checking in Multi-Error Collection -- Individual Null Elements Not Addressed

**Spec reference:** Line 161 -- "Empty error collection rejected"
Line 162 -- "Null error rejected on `Result.Failure<T>()`"
Line 194 -- "Failure rejects null collection"

**Evidence:** The spec covers: null single error (rejected), null collection (rejected), empty collection (rejected). But it does not specify what happens when the collection contains null elements, e.g., `Result.Failure<T>(new IError[] { validError, null! })`.

**Category:** Incomplete

**Severity:** Medium -- A defensive implementation should reject collections containing null elements. This is a common edge case that should be documented.

**Recommendation:** Add acceptance criterion: "Failure rejects collection containing null elements -- throws `ArgumentException`." Add corresponding test case.

---

### M6: No Explicit Constraint on `T` in `Result<T>`

**Spec reference:** Lines 45-63 -- `Result<T>` API surface.

**Evidence:** The spec does not state whether `T` has any constraints (e.g., `where T : notnull`). Given line 102 says null values are rejected for reference types, a `notnull` constraint would give compile-time enforcement rather than relying solely on a runtime `ArgumentNullException`. Without it, `Result<string?>` would be a valid type but `Success(null)` would throw at runtime.

**Category:** Ambiguous

**Severity:** Medium -- A `notnull` constraint would provide better compile-time safety. However, it may be intentionally omitted to allow `Result<string?>` where the caller explicitly wants nullable-value-success semantics. The spec should state the decision either way.

**Questions for stakeholder:**
1. Should `Result<T>` have `where T : notnull`?
2. Or should it remain unconstrained with runtime null checks only?

---

## Low Severity Findings

### L1: `Result` Non-Generic Has No `ToString()` Specification

**Spec reference:** Lines 69-89 -- `Result` API surface. Lines 219-224 -- `ResultToStringTests`.

**Evidence:** The `ToString()` tests only cover `Result<T>` (success with value, failure with codes). The non-generic `Result` has no `ToString()` specification. What does `Result.Success().ToString()` return? `"Success()"` with no value? `"Success"`?

**Category:** Incomplete

**Severity:** Low -- Minor omission. Suggest specifying `"Success"` for non-generic success and same failure format as generic.

---

### L2: Implicit Conversion from `T` to `Result<T>` -- No Implicit Conversion from Error(s) to `Result<T>`

**Spec reference:** Line 59 -- implicit conversion from `T` to `Result<T>` for success.

**Evidence:** The spec includes implicit `T -> Result<T>` for ergonomic success returns (`return order;`). It does not include any implicit conversion from `IError` or `IError[]` to `Result<T>` for failure. This means failure always requires `Result.Failure<T>(error)` which is more verbose than the success path.

**Category:** Extra observation (not a gap)

**Severity:** Low -- This is a design choice, not a deficiency. The asymmetry is likely intentional (explicit failure, ergonomic success). Noting it for completeness.

---

### L3: Test File Structure -- Single File May Become Large

**Spec reference:** Lines 230-233 -- all tests in `tests/Yaf.Domain.Tests/ResultTests.cs`.

**Evidence:** The spec defines 8 test classes with approximately 30+ test methods. Placing all in a single file follows the existing `ErrorTests.cs` pattern (which has 5 classes, ~20 tests). This is consistent with project conventions.

**Category:** Extra observation

**Severity:** Low -- The single file approach is consistent with existing patterns. No action needed, just noting the file will be substantial.

---

### L4: No Specification for `Failure` Factory Accepting `params IError[]`

**Spec reference:** Lines 85-88 -- Failure factories accept `IError` (single) or `IReadOnlyCollection<IError>` (multi).

**Evidence:** There is no `params IError[]` overload for creating failures with multiple inline errors without creating a collection first. For example:
```csharp
// Current spec requires:
Result.Failure<T>(new IError[] { error1, error2 });
// A params overload would allow:
Result.Failure<T>(error1, error2);
```

**Category:** Extra observation

**Severity:** Low -- The spec explicitly chose `IReadOnlyCollection<IError>` to match `IValidatable.GetValidationErrors()` return type (line 100). A `params` overload could be added later. Not a gap, just an ergonomic consideration.

---

### L5: Spec References Nonexistent Solution Document

**Spec reference:** Line 259 -- references `docs/solutions/design-patterns/auto-generated-error-codes-with-callermembername.md`.

**Evidence:**
This is a reference document only, not an implementation dependency. If the file does not exist, it is a broken documentation link but does not affect implementability.

**Category:** Incomplete (documentation)

**Severity:** Low -- Verify the referenced solution document exists. If not, either create it or remove the reference.

---

## Clarification Questions

1. **H1 -- ADR Status:** Is the Result and Error Pattern ADR approved for implementation, or is it still under active review? Should implementation proceed regardless?

2. **H2 -- Failure Equality:** What equality comparison should be used for `IError` elements during failure sequence comparison? `object.Equals` (delegates to concrete type), or structural comparison by `Code`+`Message`?

3. **H3 -- Operators:** Should `operator ==` and `operator !=` be included in the API surface for both `Result<T>` and `Result`?

4. **H4 -- Default Behavior:** Is the intent that `default(Result<T>).Errors` throws `InvalidOperationException`? If so, what message? Should there be a way to distinguish "uninitialized" from "real failure"?

5. **M3 -- Internal State:** For success results, should the internal `_errors` field be `Array.Empty<IError>()` (to distinguish from `null` = default)?

6. **M5 -- Null Elements:** Should `Result.Failure<T>(collection)` reject collections containing null `IError` elements?

7. **M6 -- Generic Constraint:** Should `Result<T>` have a `where T : notnull` constraint, or remain unconstrained with runtime-only null rejection?

---

## Extra Features (Not in Spec, Potentially Expected)

None identified. The spec is appropriately scoped and explicitly lists what is deferred (monadic methods, `IDomainError`/`IApplicationError` hierarchy, `ToResult<T>()` extension).

---

## Codebase Compatibility Verification

| Spec Claim | Verified | Evidence |
|------------|----------|----------|
| `IError` has `Code` and `Message` | Yes | `src/Yaf.Domain/Interfaces/IError.cs:9-17` |
| `Error` is a sealed record implementing `IError` | Yes | `src/Yaf.Domain/Error.cs:9` |
| `IValidatable.GetValidationErrors()` returns `IReadOnlyCollection<IError>` | Yes | `src/Yaf.Domain/Interfaces/IValidatable.cs:17` |
| `ValidationException` exists for memento hydration | Yes | `src/Yaf.Domain/ValidationException.cs:8` |
| `IErrorSource` marker interface exists | Yes | `src/Yaf.Domain/Interfaces/IErrorSource.cs:7` |
| `Error.Create<T>()` and `Error.Unspecified<T>()` factories exist | Yes | `src/Yaf.Domain/Error.cs:54,73` |
| No existing `Result` types in the codebase | Yes | Glob for `**/Result*.cs` in `src/` returned no results |
| `TreatWarningsAsErrors` is enabled | Yes | `Directory.Build.props:4` |
| CS1591 enforced for source projects | Yes | `.editorconfig:16` (warning) + `TreatWarningsAsErrors` = error |
| Test naming convention: `{Type}{Concern}Tests` | Yes | `tests/Yaf.Domain.Tests/ErrorTests.cs` uses this pattern |

---

## Recommendations

1. **Before implementation:** Resolve all High severity items (H1-H4) and the clarification questions. These affect API design decisions that are hard to change after tests are written.

2. **Add to spec:** `operator ==`/`operator !=` overloads for both structs (H3).

3. **Add to spec:** Null-element-in-collection rejection (M5).

4. **Add to spec:** Concrete `ToString()` format for default values (M4) and non-generic `Result.Success()` (L1).

5. **Add to spec:** Internal state representation table (M3) to guide the implementer unambiguously.

6. **Decide:** `where T : notnull` constraint question (M6).

7. **Decide:** Equality comparison strategy for `IError` elements in failure results (H2).
