# Development Setup

This guide covers everything needed to contribute to YAF using Claude Code as the AI-assisted development tool.

## Prerequisites

### .NET SDK

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (or later). Verify with:

```sh
dotnet --version
```

### GitHub CLI

Install the [GitHub CLI](https://cli.github.com/) and authenticate:

```sh
gh auth login
```

### Claude Code

Install [Claude Code](https://claude.com/product/claude-code) and ensure it is available on your PATH:

```sh
claude --version
```

## Claude Code Tooling

The project ships checked-in configuration that sets up plugins, MCP servers, and permissions automatically. Contributors only need to install a few prerequisites — the rest loads from repo config.

### C# Language Server

The `csharp-lsp` plugin requires `csharp-ls` installed as a global .NET tool:

```sh
dotnet tool install -g csharp-ls
```

This gives Claude Code real-time C# diagnostics (type errors, missing references) and code navigation (jump-to-definition, find-references) after every edit.

### Plugins

Plugins and their marketplaces are configured in `.claude/settings.json`. They should activate automatically when Claude Code opens the project. If any are missing, install manually:

```
/plugin marketplace add dotnet/skills
/plugin marketplace add Aaronontheweb/dotnet-skills
/plugin install csharp-lsp@claude-plugins-official
/plugin install compound-engineering@every-marketplace
/plugin install dotnet@dotnet-agent-skills
/plugin install dotnet-skills@dotnet-skills
```

| Plugin | Marketplace | Purpose |
|--------|-------------|---------|
| `csharp-lsp` | `claude-plugins-official` | C# language server — real-time diagnostics, code navigation |
| `compound-engineering` | `every-marketplace` | Workflow orchestration, Context7 docs lookup, Playwright |
| `dotnet` | `dotnet-agent-skills` | Official Microsoft .NET skills (core, EF, diagnostics, NuGet, testing) |
| `dotnet-skills` | `dotnet-skills` | Community .NET skills and agents (C# patterns, Aspire, performance) |

### MCP Servers

MCP servers are configured in `.mcp.json` at the repo root and load automatically.

| Server | Purpose |
|--------|---------|
| `nuget` | NuGet package search, vulnerability detection, version management |

The NuGet MCP server requires the .NET 10+ SDK (`dnx` command).

### Permissions

Project-level permissions in `.claude/settings.json` pre-approve common CLI commands (`dotnet`, `git`, `gh`, `powershell`) to reduce interactive approval prompts during development.

Personal permission overrides go in `.claude/settings.local.json` (git-ignored).

## Quick Start

```sh
git clone https://github.com/Cartomatic/YAF.git
cd YAF
dotnet tool install -g csharp-ls
claude
```

Claude Code will automatically pick up the project plugins, MCP servers, and permissions from the checked-in configuration files.
