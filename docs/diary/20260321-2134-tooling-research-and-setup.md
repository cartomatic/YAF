# Session: Tooling Research and Setup

**Date:** 2026-03-21
**Branch:** `docs/claude-md-conventions`

## Goal

Research and configure Claude Code plugins, MCP servers, and skills for modern .NET development on the YAF project.

## What Was Achieved

- Researched available Claude Code plugins and MCP servers for .NET development
- Installed and configured:
  - **csharp-lsp plugin** — C# language server for real-time diagnostics and code navigation
  - **NuGet MCP Server** — package search, vulnerability detection, version management
  - **Project-level permissions** (`.claude/settings.json`) — pre-approved `dotnet`, `git`, `gh`, `powershell` commands
- Documented existing plugins already installed globally: `compound-engineering`, `dotnet` (Microsoft official), `dotnet-skills` (Aaronontheweb)
- Made all plugin/marketplace/MCP configuration project-level so contributors get it automatically
- Created `docs/general/dev-setup.md` — full contributor onboarding guide
- Updated README and CLAUDE.md with links to dev setup
- Produced research doc: `docs/research/20260321-2051-claude-code-tooling-for-dotnet.md`

## What Went Well

- Parallel research agents were effective — 5 agents launched simultaneously covering different areas, results came back within minutes
- The final tooling research (focused on Claude Code itself) was comprehensive, identifying plugins and MCP servers with concrete install commands
- Project-level config approach (`.claude/settings.json`, `.mcp.json`) means contributors don't need to manually configure anything beyond installing `csharp-ls`

## What Went Wrong

- **Significant drift on the initial research.** The first round of 5 research agents went deep into .NET architecture best practices (DDD patterns, library choices, CI/CD pipelines, Aspire strategy) instead of focusing on what the user actually asked: what tools/plugins to equip Claude Code with. Two research documents were produced on the wrong topic before the user corrected course.
- **Root cause:** The question "what plugins, skills should be useful for modern .NET development" was interpreted as "what .NET development patterns should we follow" rather than "what tooling should we install for the AI assistant." The distinction between _equipping the tool_ vs _planning the project architecture_ was missed.
- **Resolution:** User explicitly redirected ("I am not yet interested in .NET best practises... I would like to equip you with the proper tooling first"). The off-topic research files were deleted and a focused research agent was launched that delivered the right results.
- **Lesson:** When the user asks about "skills" and "plugins", default to interpreting this as Claude Code tooling configuration, not project-level technical decisions. Ask for clarification if ambiguous.

## Other Notes

- The off-topic research wasn't entirely wasted — it will be useful when the project moves to architecture planning. But it was premature and not what was asked for.
- Discovered that `dnx` on Windows is a PowerShell script (`C:\Program Files\dotnet\dnx.ps1`), so MCP server config needs `powershell -Command "dnx ..."` rather than calling `dnx` directly from bash.
- A custom NuGet source (`pkgs.dev.azure.com/emapasa`) in the global NuGet config caused `dotnet tool install` to fail; `--ignore-failed-sources` was needed as a workaround.
- Learned to use `powershell -Command "Get-Date -Format 'HH:mm'"` for local time instead of guessing (saved as a memory for future sessions).

## Communication Assessment

### User's communication style

- **Concise and direct.** Short instructions ("yes", "yup", "thnx commit") when things are on track. This works well — no noise, easy to act on.
- **Clear corrections.** When things drifted, the redirect was unambiguous: "dude you went a bit south... I am not yet interested in .NET best practises... I would like to equip you with the proper tooling first." No room for misinterpretation on the second pass.
- **Implicit context.** The initial request ("what plugins, skills should be useful for modern .NET development") carried an implied subject — "for you, the AI tool" — that wasn't explicitly stated. This is natural human communication but created the main misunderstanding of the session.

### Claude's communication gaps

- **Assumed instead of asking.** The ambiguous request had two valid interpretations: (1) tooling for Claude Code, (2) .NET patterns for the project. Instead of asking "do you mean tools for me to use, or .NET libraries and patterns for the project itself?", Claude assumed the broader interpretation and launched 5 expensive research agents. A single clarifying question would have saved significant time and tokens.
- **Over-eagerness to act.** The instinct to immediately spin up parallel agents before confirming understanding was the root problem. Ambiguity should trigger a question, not a shotgun approach.
- **Good recovery.** Once corrected, the focused research was on target and actionable. The course correction was quick.

### What to improve

Both sides should adopt a simple rule: **when something is ambiguous, ask before acting.**

- **Claude:** If a request has multiple valid interpretations and the cost of getting it wrong is high (e.g., launching multiple research agents), ask a short clarifying question first. A 10-second question beats 5 minutes of wasted work.
- **User:** When a request involves a distinction that matters (e.g., "tooling for you" vs "patterns for the project"), adding a word or two of context helps. Though this is a minor point — the user's correction was fast and clear.

The concise communication style works well for this workflow. The only gap is at the start of new topics where the intent isn't yet established. Once aligned, the back-and-forth is efficient.

## Files Changed

| File | Action |
|------|--------|
| `.claude/settings.json` | Created — plugins, marketplaces, permissions |
| `.mcp.json` | Created — NuGet MCP server config |
| `docs/general/dev-setup.md` | Created — contributor setup guide |
| `docs/research/20260321-2051-claude-code-tooling-for-dotnet.md` | Created — tooling research |
| `CLAUDE.md` | Updated — added tooling section |
| `README.md` | Updated — added dev setup link |
