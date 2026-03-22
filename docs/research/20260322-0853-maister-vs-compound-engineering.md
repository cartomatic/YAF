# Maister vs Compound Engineering Plugin — Comparison & Combined Workflow

## Context

Both [Maister](https://github.com/SkillPanel/maister) (SkillPanel) and [Compound Engineering](https://github.com/EveryInc/compound-engineering-plugin) (EveryInc) are Claude Code plugins that structure AI-assisted software development. This research compares their approaches and defines a combined workflow for YAF.

## What They Are

| | **Maister** | **Compound Engineering** |
|---|---|---|
| Core idea | Structured, phase-gated workflows with standards enforcement | Cyclical knowledge-compounding where each task makes the next easier |
| Philosophy | AI needs guardrails and human checkpoints | 80% planning/review, 20% execution; knowledge is an asset |
| Scale | 14 skills, 26 agents, 8 commands | 19 skills, 29 agents, 22 commands |
| Maturity | 46 stars, v2.0.6 | 10.8k stars, v2.48.0, 468 commits |
| Origin | Independent/community | Production-tested at Every (5 products, thousands of daily users) |
| License | MIT | MIT |

## Approach Differences

### Maister — Control-oriented

- Multi-phase development workflows with mandatory pause gates between phases
- Standards discovery system that scans the codebase and scores conventions (0–100 confidence)
- Explicit "no YOLO mode" — removed continuous execution in favor of human gates
- Non-automatic rollback philosophy: stop, analyze, ask user — never silently undo work
- State persistence via `orchestrator-state.yml` for pause/resume across sessions
- Evidence-cited findings — every discovered standard cites specific files/configs

### Compound Engineering — Compounding-oriented

- Brainstorm → Plan → Work → Review → Compound loop
- Multi-agent review from 12+ distinct perspectives (security, performance, simplicity, etc.)
- Knowledge capture phase ("Compound") stores learnings as indexed markdown with YAML frontmatter
- Persistent context files automatically loaded into future sessions
- Flexible autonomy levels — from fully manual to fully autonomous pipelines

## Pros & Cons

### Maister

**Pros:**
- Stronger guardrails — mandatory human gates prevent runaway AI decisions
- Standards discovery — automatically extracts and scores existing project conventions with evidence
- Resume from any phase — robust state management for long-running tasks
- Simpler mental model — linear phase progression

**Cons:**
- Slower velocity — mandatory gates on every phase add friction for routine tasks
- Smaller ecosystem — fewer contributors, less battle-testing
- No explicit knowledge compounding — institutional knowledge doesn't systematically build up
- Limited platform support — Claude Code only (Copilot CLI secondary, with known issues)

### Compound Engineering

**Pros:**
- Knowledge compounds over time — documented learnings become automatic context for future sessions
- Battle-tested — production use at Every across 5 products
- Rich review — 12+ specialized reviewer agents catch issues from many angles
- Multi-platform — syncs to Cursor, Copilot, Windsurf, and more
- Active development with frequent releases

**Cons:**
- Context budget pressure — heavy agent spawning can hit context limits
- Complexity — large surface area (22 commands, 29 agents, 19 skills) with steep onboarding curve
- Parallel coordination risk — many agents can do redundant work if not carefully orchestrated
- Opinionated toward Every's stack — some skills are Ruby/Rails-specific

## Combined Approach

The two plugins optimize for different moments. Combining them means adaptive workflow selection based on risk and context, not merging everything into one mega-plugin.

### Principles

1. **Maister's standards discovery** is most valuable at project setup and onboarding
2. **CE's compounding loop** is most valuable during ongoing development
3. **Maister's phase gates and reviews** are most valuable for high-risk changes
4. **CE's flexible autonomy** is most valuable for routine work
5. **CE's compound phase** should capture Maister's discoveries too — single knowledge store

## Combined Workflow Cheatsheet

### Step 0 — Classify Risk

| Risk | Examples | Mode |
|---|---|---|
| **Trivial** | Typo, config tweak, one-liner | Just do it |
| **Low** | Simple feature, internal refactor | Flow |
| **Medium** | New endpoint, new domain entity | Flow + selective review |
| **High** | Schema migration, auth, public API | Guarded |
| **Critical** | Data migration, breaking change, security | Guarded + mandatory approval |

### Project Setup & Standards Discovery

| What | Command |
|---|---|
| Initialize project standards | `/maister:init` |
| Discover conventions (scored, evidence-cited) | `/maister:standards-discover` |
| Update/create a standard | `/maister:standards-update` |
| Persist findings for future sessions | `/ce:compound` |

### Flow Mode (Low/Medium Risk)

| Phase | Command | Notes |
|---|---|---|
| **Explore** | `/ce:brainstorm` | Requirements, edge cases, approaches |
| **Refine** | `/document-review` | Polish brainstorm/plan before moving on |
| **Plan** | `/ce:plan` | Research-backed implementation strategy |
| **Deepen** | `/deepen-plan` | Optional — parallel research per section |
| **Execute** | `/ce:work` | Task-tracked implementation |
| **Review** | `/ce:review` | Multi-agent, 12+ perspectives |
| **Compound** | `/ce:compound` | Document learnings |

**Shortcut for trivial/obvious tasks:** `/maister:quick-dev` (skips planning, goes straight to implementation with standards awareness)

### Guarded Mode (High/Critical Risk)

Same phases as Flow, with gates and extra verification inserted:

| Phase | Command | Gate? |
|---|---|---|
| **Explore** | `/ce:brainstorm` | |
| **Plan** | `/ce:plan` + `/deepen-plan` | **GATE — human approves plan** |
| **Audit spec** | `/maister:reviews-spec-audit` | **GATE — verify completeness** |
| **Execute** | `/ce:work` | |
| **Review** | `/ce:review` | **GATE — all perspectives** |
| **Targeted review** | `/maister:reviews-code` | **GATE — security/quality/perf** |
| **Reality check** | `/maister:reviews-reality-check` | **GATE — does it actually work?** |
| **Deploy readiness** | `/maister:reviews-production-readiness` | **GATE — go/no-go** |
| **Browser tests** | `/test-browser` | |
| **Compound** | `/ce:compound` | |

### Specialized Workflows

| Situation | Command |
|---|---|
| Technology/platform migration | `/maister:migration` |
| Performance optimization | `/maister:performance` |
| Investigation / technical research | `/maister:research` |
| Full adaptive dev (Maister's phase-gated flow) | `/maister:development` |
| Unified entry (auto-classifies to above) | `/maister:work` |

### Review — Pick What You Need

| Need | Command |
|---|---|
| Full multi-agent review (12+ lenses) | `/ce:review` |
| Code quality + security + performance | `/maister:reviews-code` |
| Over-engineering detection | `/maister:reviews-pragmatic` |
| Spec completeness before implementation | `/maister:reviews-spec-audit` |
| Does the implementation actually work? | `/maister:reviews-reality-check` |
| Production deployment go/no-go | `/maister:reviews-production-readiness` |

### Quick Reference

```
Task arrives
│
├─ Trivial → just do it
│
├─ Small & clear → /ce:plan → /ce:work → /ce:review → /ce:compound
│     or shortcut: /maister:quick-dev
│
├─ Medium → /ce:brainstorm → /ce:plan → /ce:work → /ce:review → /ce:compound
│
├─ High risk → Flow + gates:
│     /ce:brainstorm → /ce:plan [GATE] → /maister:reviews-spec-audit [GATE]
│     → /ce:work → /ce:review [GATE] → /maister:reviews-code [GATE]
│     → /maister:reviews-reality-check [GATE] → /ce:compound
│
├─ Migration → /maister:migration
├─ Performance → /maister:performance
├─ Research → /maister:research
│
└─ New project → /maister:init → /maister:standards-discover → /ce:compound
```

## Consequences for YAF

Given YAF's constraint that all code is AI-generated:

- **Standards discovery at init** — let framework conventions emerge from code, then crystallize them
- **Mandatory gates for architectural decisions** — this is a framework; public API surface matters enormously
- **Compound everything** — no tribal knowledge from human developers, so compounded learnings are the institutional memory
- **Risk-adaptive autonomy** — routine implementation in flow mode, anything touching the public API in guarded mode

## Summary

CE carries the daily workflow (brainstorm → plan → work → review → compound). Maister fills two gaps CE doesn't cover: standards discovery with evidence scoring, and targeted reviews with rigid phase gates for critical paths. The combination avoids the weaknesses of each — Maister's friction on routine tasks and CE's lack of standards enforcement — while preserving both plugins' strengths.
