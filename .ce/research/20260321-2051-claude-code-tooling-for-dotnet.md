# Claude Code Tooling for .NET Development

Research into plugins, MCP servers, and configuration to make Claude Code more effective for .NET development.

## Current Setup (YAF Project)

Already installed (from `~/.claude/settings.json`):

| Component | Type | Source |
|-----------|------|--------|
| compound-engineering | Plugin | `EveryInc/compound-engineering-plugin` (every-marketplace) |
| dotnet | Plugin | `dotnet/skills` (dotnet-agent-skills) |
| dotnet-skills | Plugin | `Aaronontheweb/dotnet-skills` (dotnet-skills) |

No project-level `.claude/settings.json` exists yet. The `.claude/settings.local.json` has minimal permission allowances (gh, WebSearch, WebFetch).

---

## 1. Plugins Already Installed

### compound-engineering (EveryInc)

- **What**: Workflow orchestration plugin from Every, Inc. Implements "80% planning/review, 20% execution" methodology.
- **Maintainer**: Kieran Klaassen and Dan Shipper (Every, Inc.)
- **Key commands**: `/ce:ideate`, `/ce:brainstorm`, `/ce:plan`, `/ce:work`, `/ce:review`, `/ce:compound`
- **Includes**: Context7 MCP server (real-time framework docs for 100+ libraries) and Playwright MCP server
- **Note**: Context7 may not auto-load. May need manual MCP config. Set `CONTEXT7_API_KEY` env var to avoid rate limits.
- **Repo**: https://github.com/EveryInc/compound-engineering-plugin

### dotnet (Microsoft Official)

- **What**: Official .NET team skills for coding agents.
- **Maintainer**: Microsoft (.NET team)
- **10 plugin categories**: dotnet (core), dotnet-data (EF), dotnet-diag (perf debugging), dotnet-msbuild (build diagnostics), dotnet-nuget (package management), dotnet-upgrade (version migrations), dotnet-maui (mobile), dotnet-ai (LLM/ML.NET), dotnet-template-engine (scaffolding), dotnet-test (test execution)
- **Repo**: https://github.com/dotnet/skills

### dotnet-skills (Aaronontheweb)

- **What**: Community plugin with 30 skills and 5 specialized agents.
- **Maintainer**: Aaron Stannard (Akka.NET creator)
- **Version**: v1.3.0 (Feb 19, 2026)
- **Skills**: C# patterns, EF Core, .NET Aspire, ASP.NET Core, testing (Testcontainers, Playwright, snapshot testing), Microsoft.Extensions, serialization, project structure
- **Agents**: akka-net-specialist, dotnet-concurrency-specialist, dotnet-benchmark-designer, dotnet-performance-analyst, docfx-specialist
- **Repo**: https://github.com/Aaronontheweb/dotnet-skills

---

## 2. Plugins to Consider Adding

### dotnet-claude-kit (codewithmukesh)

- **What**: 47 skills, 10 agents, 16 slash commands, 15 MCP tools (Roslyn-powered), 10 rules, 5 project templates
- **Target**: .NET 10 / C# 14
- **Unique features**: Roslyn semantic analysis (dependency graphs, circular dependency detection, dead code finder, test coverage mapping), `/dotnet-init` interactive setup, `/scaffold` feature generation, `/verify` 7-phase verification, `/tdd` red-green-refactor workflow
- **Requires**: `dotnet tool install -g CWM.RoslynNavigator`
- **Install**:
  ```
  /plugin marketplace add codewithmukesh/dotnet-claude-kit
  /plugin install dotnet-claude-kit
  ```
- **Repo**: https://github.com/codewithmukesh/dotnet-claude-kit
- **Assessment**: Strong candidate. The Roslyn-powered MCP tools for semantic code analysis are unique and would complement the other two plugins well. The `/dotnet-init` and `/scaffold` commands align with YAF's greenfield status.

### csharp-lsp (Official Anthropic Plugin)

- **What**: C# Language Server Protocol integration giving Claude jump-to-definition, find-references, and automatic diagnostics after edits.
- **Requires**: `csharp-ls` binary in PATH (`dotnet tool install -g csharp-ls`)
- **Install**:
  ```
  /plugin install csharp-lsp@claude-plugins-official
  ```
- **Assessment**: HIGH PRIORITY. This gives Claude Code real-time type error detection and code navigation for C#. After every edit, Claude sees diagnostics without running the compiler. This is probably the single highest-impact addition.

### wshaddix/dotnet-skills

- **What**: 167 skills and 16 agents. Broader .NET ecosystem coverage (Blazor, MAUI, Native AOT, security, CI/CD, cloud-native).
- **Install**:
  ```
  /plugin marketplace add wshaddix/dotnet-skills
  /plugin install dotnet-skills@wshaddix-dotnet-skills
  ```
- **Repo**: https://github.com/wshaddix/dotnet-skills
- **Assessment**: Overlaps significantly with existing plugins. May cause skill conflicts. Evaluate after initial development starts.

### Official Anthropic Plugins Worth Installing

From the official marketplace (`claude-plugins-official`):

| Plugin | Purpose | Install |
|--------|---------|---------|
| `github` | GitHub integration (issues, PRs, actions) | `/plugin install github@claude-plugins-official` |
| `commit-commands` | Git commit workflows | `/plugin install commit-commands@claude-plugins-official` |
| `pr-review-toolkit` | PR review agents | `/plugin install pr-review-toolkit@claude-plugins-official` |

---

## 3. MCP Servers to Add

### NuGet MCP Server (Microsoft Official)

- **What**: Package search, vulnerability detection, package updates, version management
- **Requires**: .NET 10 SDK (`dnx` command)
- **Claude Code config** (add to settings or use `claude mcp add`):
  ```json
  {
    "mcpServers": {
      "nuget": {
        "type": "stdio",
        "command": "dnx",
        "args": ["NuGet.Mcp.Server", "--source", "https://api.nuget.org/v3/index.json", "--yes"]
      }
    }
  }
  ```
- **Or CLI**: `claude mcp add nuget -- dnx NuGet.Mcp.Server --source https://api.nuget.org/v3/index.json --yes`
- **Docs**: https://learn.microsoft.com/en-us/nuget/concepts/nuget-mcp-server
- **Assessment**: RECOMMENDED. Direct NuGet integration for package management.

### Playwright MCP Server (Microsoft Official)

- **What**: Browser automation, exploratory testing, test code generation via accessibility tree
- **Install**: `claude mcp add playwright -- npx @playwright/mcp@latest`
- **Note**: May already be included via compound-engineering plugin. Verify with `claude mcp list`.
- **Repo**: https://github.com/microsoft/playwright-mcp
- **Assessment**: Useful when web API testing with browser begins.

### GitHub MCP Server (GitHub Official)

- **What**: Read issues, review PRs, search repos, manage actions directly from Claude Code
- **Install**:
  ```
  claude mcp add github -e GITHUB_PERSONAL_ACCESS_TOKEN=<token> -- docker run -i --rm -e GITHUB_PERSONAL_ACCESS_TOKEN ghcr.io/github/github-mcp-server
  ```
  Or without Docker (if using the `gh` CLI, Claude already has access via Bash):
  ```
  claude mcp add github -e GITHUB_PERSONAL_ACCESS_TOKEN=<token> -e GITHUB_TOOLSETS="repos,issues,pull_requests,actions,code_security" -- npx @anthropic-ai/github-mcp-server
  ```
- **Repo**: https://github.com/github/github-mcp-server
- **Assessment**: Claude already has `gh` CLI access. MCP server adds structured tool access but may be redundant. Lower priority.

### Context7 MCP Server

- **What**: Real-time documentation lookup for 100+ frameworks. Prevents hallucination from outdated training data.
- **Note**: Bundled with compound-engineering plugin but may not auto-load.
- **Manual config if needed**:
  ```json
  {
    "mcpServers": {
      "context7": {
        "type": "stdio",
        "command": "npx",
        "args": ["-y", "@upstash/context7-mcp@latest"]
      }
    }
  }
  ```
- **Env var**: Set `CONTEXT7_API_KEY` to avoid anonymous rate limits.
- **Assessment**: HIGH PRIORITY. Ensures Claude uses current .NET 10 docs instead of outdated training data.

### Docker MCP Server

- **What**: Container management from Claude Code
- **Install**: `claude mcp add docker -- npx @anthropic-ai/docker-mcp-server`
- **Repo**: https://github.com/QuantGeekDev/docker-mcp
- **Assessment**: Useful later for containerized deployment. Not needed yet.

### SQL Server MCP Server

- **What**: Database exploration, query execution, schema inspection
- **Config**:
  ```json
  {
    "mcpServers": {
      "mssql": {
        "type": "stdio",
        "command": "npx",
        "args": ["-y", "mcp-server-mssql"],
        "env": {
          "MSSQL_SERVER": "localhost",
          "MSSQL_DATABASE": "yourdb",
          "MSSQL_USER": "sa",
          "MSSQL_PASSWORD": "yourpass"
        }
      }
    }
  }
  ```
- **Assessment**: Useful when database work begins. Not needed yet.

---

## 4. Claude Code Configuration Recommendations

### Permission Settings (`.claude/settings.json` for project)

Create a project-level settings file to pre-approve common .NET CLI operations:

```json
{
  "permissions": {
    "allow": [
      "Bash(dotnet build *)",
      "Bash(dotnet test *)",
      "Bash(dotnet run *)",
      "Bash(dotnet restore *)",
      "Bash(dotnet new *)",
      "Bash(dotnet add *)",
      "Bash(dotnet remove *)",
      "Bash(dotnet list *)",
      "Bash(dotnet tool *)",
      "Bash(dotnet format *)",
      "Bash(dotnet clean *)",
      "Bash(dotnet publish *)",
      "Bash(dotnet sln *)",
      "Bash(dotnet nuget *)",
      "Bash(git *)",
      "Bash(gh *)",
      "WebSearch",
      "WebFetch"
    ]
  }
}
```

### Hooks Configuration

Add to `.claude/settings.json` for automatic quality checks:

```json
{
  "hooks": {
    "PostToolUse": [
      {
        "matcher": "Edit|Write",
        "hooks": [
          {
            "type": "command",
            "command": "dotnet build --no-restore --verbosity quiet 2>&1 | tail -5"
          }
        ]
      }
    ]
  }
}
```

This runs a quick build check after every file edit, so Claude sees compilation errors immediately. Consider also:
- A hook to run `dotnet format --verify-no-changes` after edits (style enforcement)
- A hook to run affected tests after edits (requires project structure first)

**Note**: Hooks should be added after the solution structure exists, since they will fail on an empty project.

---

## 5. Priority Shopping List

### Install Now (High Impact)

| Item | Type | Action |
|------|------|--------|
| C# LSP plugin | Plugin | `/plugin install csharp-lsp@claude-plugins-official` then `dotnet tool install -g csharp-ls` |
| Context7 MCP | MCP | Verify it loads from compound-engineering; if not, add manually. Set `CONTEXT7_API_KEY`. |
| NuGet MCP Server | MCP | `claude mcp add nuget -- dnx NuGet.Mcp.Server --source https://api.nuget.org/v3/index.json --yes` |
| Project permissions | Config | Create `.claude/settings.json` with dotnet CLI allow-list |

### Install When Code Exists

| Item | Type | Action |
|------|------|--------|
| dotnet-claude-kit | Plugin | `/plugin marketplace add codewithmukesh/dotnet-claude-kit` (Roslyn tools need a solution to analyze) |
| Build hook | Config | Add PostToolUse hook for `dotnet build` after edits |
| Test hook | Config | Add PostToolUse hook for `dotnet test` on affected projects |

### Install When Needed

| Item | Type | Action |
|------|------|--------|
| Playwright MCP | MCP | When browser/API testing starts |
| Docker MCP | MCP | When containerization work starts |
| SQL Server MCP | MCP | When database work starts |
| GitHub MCP Server | MCP | If `gh` CLI proves insufficient |

### Evaluate Later

| Item | Type | Reason |
|------|------|--------|
| wshaddix/dotnet-skills | Plugin | 167 skills, may overlap with existing plugins |
| managedcode/dotnet-skills | Plugin | Cross-agent catalog, different approach |
| github plugin (official) | Plugin | `/plugin install github@claude-plugins-official` |

---

## 6. Plugin Ecosystem Notes

### Plugin Management Commands

```
/plugin                              # Interactive UI (Discover, Installed, Marketplaces, Errors tabs)
/plugin marketplace add owner/repo   # Add a marketplace
/plugin marketplace list             # List configured marketplaces
/plugin marketplace update name      # Refresh marketplace
/plugin marketplace remove name      # Remove marketplace
/plugin install name@marketplace     # Install a plugin
/plugin uninstall name@marketplace   # Remove a plugin
/plugin disable name@marketplace     # Disable without removing
/plugin enable name@marketplace      # Re-enable
/reload-plugins                      # Apply changes without restart
```

### MCP Management Commands

```
claude mcp add name -- command args          # Add stdio MCP server
claude mcp add --transport http name url     # Add HTTP MCP server
claude mcp add name --scope project -- cmd   # Project-scoped (creates .mcp.json)
claude mcp list                              # List configured servers
claude mcp remove name                       # Remove a server
claude mcp get name                          # Test a server
```

### Key Marketplaces

| Marketplace | ID | Source |
|------------|-----|--------|
| Anthropic Official | `claude-plugins-official` | Auto-available |
| Anthropic Demo | `anthropics-claude-code` | `/plugin marketplace add anthropics/claude-code` |
| Microsoft .NET | `dotnet-agent-skills` | `/plugin marketplace add dotnet/skills` |
| Aaronontheweb | `dotnet-skills` | `/plugin marketplace add Aaronontheweb/dotnet-skills` |
| Every Inc | `every-marketplace` | `/plugin marketplace add EveryInc/compound-engineering-plugin` |

---

## Sources

- https://github.com/Aaronontheweb/dotnet-skills
- https://github.com/dotnet/skills
- https://github.com/codewithmukesh/dotnet-claude-kit
- https://github.com/EveryInc/compound-engineering-plugin
- https://github.com/github/github-mcp-server
- https://github.com/microsoft/playwright-mcp
- https://learn.microsoft.com/en-us/nuget/concepts/nuget-mcp-server
- https://code.claude.com/docs/en/discover-plugins
- https://code.claude.com/docs/en/mcp
- https://code.claude.com/docs/en/permissions
- https://code.claude.com/docs/en/hooks-guide
- https://devblogs.microsoft.com/dotnet/extend-your-coding-agent-with-dotnet-skills/
- https://devblogs.microsoft.com/dotnet/nuget-mcp-server-preview/
- https://platform.uno/blog/configuring-claude-code-for-real-net-projects/
