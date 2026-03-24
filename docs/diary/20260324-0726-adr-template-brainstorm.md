# Session Diary: ADR Template Brainstorm

**Date:** 2026-03-24
**Duration:** ~20 minutes
**Participants:** @cartomatic (decision-maker), Claude Code (drafter)

---

## Goal

Define the ADR (Architecture Decision Record) template structure and folder organization for the YAF project, so that architectural decisions from prior brainstorms can be formalized.

## What Was Achieved

- **ADR template defined** with 14 sections: title, timestamp, status, scope, stakeholders, summary, drivers, options with analysis, recommendation, consequences, conclusion, more information, supersedes/superseded-by, review date
- **Folder structure decided**: `docs/adr/` with subfolders by scope (`domain/`, `api/`, `infrastructure/`, `architecture/`) plus a root `INDEX.md`
- **Key conventions established**:
  - Stakeholders track both human decision-maker and AI tool
  - Superseded status must include a link to the replacement ADR
  - No separate decision date — non-superseded/deprecated ADRs are "in force"
  - Standard `yyyymmdd-hhmm-short-description.md` filename convention
- **Brainstorm document written**: `docs/brainstorms/20260324-0714-adr-template-and-organization.md`
- **PR created**: #8

## What Went Well

- The brainstorming workflow worked smoothly — questions were focused and one-at-a-time
- The user had a clear vision of the template sections; the conversation added four valuable extras (scope, stakeholders, supersedes links, review date)
- Preview-based multiple choice for folder structure made the decision concrete and visual
- Quick session with clear output

## What Went Wrong

- **Missed the CHANGELOG update before creating the PR.** This is the second time this has happened. The changelog convention is in CLAUDE.md and in memory, yet it was skipped. Root cause: the commit-and-PR flow didn't include a changelog check as a habit. Memory feedback updated to be more emphatic about this.

## Communication Assessment

- **Clear from the user**: The initial request was well-structured — a bullet list of desired sections plus the storage location. This made the brainstorm focused from the start.
- **Good back-and-forth**: The multi-select questions for additional sections and scope folders worked well. The user's inline notes on answers (e.g., "subfolder by scope + index file") were helpful refinements.
- **Correction was direct and constructive**: The changelog miss was flagged immediately with clear reasoning ("I would like to document all the changes, including the docs"). This is the ideal way to redirect.

## Other Notes

- The existing brainstorms (`20260322-1755-yaf-library-design-brainstorm.md` and `20260322-2036-yaf-ddd-concepts-brainstorm.md`) contain many architectural decisions that are candidates for formalization as ADRs once the template and structure are in place.
- Next logical step: `/workflows:plan` to create the ADR folder structure, template file, and potentially convert existing brainstorm decisions into ADRs.
