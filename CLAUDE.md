# CLAUDE.md - nopCommerce Architecture Guide

This document provides a high-level overview of the nopCommerce architecture for AI assistants and developers.

## What is nopCommerce?

nopCommerce is a free, open-source e-commerce platform built on ASP.NET Core. It supports multi-store, multi-language, and multi-vendor scenarios with a highly extensible plugin architecture.

## Solution Structure

```
src/
├── Libraries/
│   ├── Nop.Core          # Domain entities, interfaces, infrastructure
│   ├── Nop.Data          # Data access layer (LinqToDB ORM)
│   └── Nop.Services      # Business logic services
├── Presentation/
│   ├── Nop.Web           # Main web application (public store + admin)
│   └── Nop.Web.Framework # Common web infrastructure
└── Plugins/              # 28+ extensibility plugins
```

## Layered Architecture

```
┌─────────────────────────────────────────────────────┐
│  Presentation (Nop.Web, Nop.Web.Framework)          │
├─────────────────────────────────────────────────────┤
│  Services (Nop.Services)                            │
├─────────────────────────────────────────────────────┤
│  Data Access (Nop.Data)                             │
├─────────────────────────────────────────────────────┤
│  Core/Domain (Nop.Core)                             │
└─────────────────────────────────────────────────────┘
```

## Key Components

### Core Layer (Nop.Core)
- **BaseEntity** (`BaseEntity.cs`) - Base class for all entities
- **Domain Entities** (`Domain/`) - 100+ entities organized by feature (Catalog, Customers, Orders, etc.)
- **NopEngine** (`Infrastructure/NopEngine.cs`) - Central service resolution and configuration
- **INopStartup** (`Infrastructure/INopStartup.cs`) - Startup configuration contract
- **IWorkContext** - Current user, language, currency context
- **IEventPublisher** (`Events/`) - Publish/subscribe event system

### Data Layer (Nop.Data)
- **IRepository<T>** - Generic repository with caching and event publishing
- **EntityRepository<T>** - Implementation using LinqToDB
- **Entity Builders** (`Mapping/Builders/`) - FluentMigrator schema definitions

### Services Layer (Nop.Services)
- Interface-based services mirroring domain structure
- Examples: `IProductService`, `ICustomerService`, `IOrderService`
- Plugin management: `IPlugin`, `BasePlugin`, `PluginDescriptor`

### Presentation Layer
- **Program.cs** - Application entry point and bootstrap
- **Areas/Admin** - Administration interface
- **Factories** - ViewModel construction (Factory pattern)
- **Validators** - FluentValidation integration

## Architectural Patterns

| Pattern | Implementation |
|---------|----------------|
| Repository | `IRepository<T>` with caching & events |
| Dependency Injection | Autofac or built-in, via `INopStartup` |
| Event-Driven | `IEventPublisher` with auto-discovered consumers |
| Factory | Model factories for ViewModel construction |
| Plugin | `IPlugin` interface with dynamic loading |

## Plugin System

Plugins extend functionality without modifying core code:
- **Payments**: `IPaymentMethod` (PayPal, Manual, etc.)
- **Shipping**: `IShippingRateComputationMethod` (UPS, Fixed rates)
- **Tax**: `ITaxProvider` (Avalara, Fixed rates)
- **Widgets**: `IWidgetPlugin` (Google Analytics, Facebook Pixel)
- **Search**: `ISearchProvider` (Lucene)

Plugin structure: `plugin.json` metadata + main class inheriting `BasePlugin`

## Bootstrap Sequence

1. Load configuration (`appsettings.json`)
2. Configure DI container (Autofac or built-in)
3. Discover and execute `INopStartup.ConfigureServices()` by order
4. Build application
5. Execute `INopStartup.Configure()` for middleware
6. Publish `AppStartedEvent`

## Cross-Cutting Concerns

- **Caching**: `IStaticCacheManager`, `IShortTermCacheManager`
- **Localization**: `ILocalizationService`, `ILocalizedEntityService`
- **Security**: `IAclService`, `IPermissionService`
- **Logging**: `ILogger`, `ICustomerActivityService`

## Common Development Tasks

### Adding a new entity
1. Create entity class in `Nop.Core/Domain/{Feature}/`
2. Create entity builder in `Nop.Data/Mapping/Builders/{Feature}/`
3. Create service interface and implementation in `Nop.Services/{Feature}/`
4. Register service in an `INopStartup` implementation

### Creating a plugin
1. Create project in `src/Plugins/Nop.Plugin.{Group}.{Name}/`
2. Add `plugin.json` with metadata
3. Create main class inheriting `BasePlugin` and implementing feature interface
4. Implement `InstallAsync()` and `UninstallAsync()`

## Key File Locations

| Purpose | Location |
|---------|----------|
| Entry point | `src/Presentation/Nop.Web/Program.cs` |
| Service registration | `src/Presentation/Nop.Web.Framework/Infrastructure/NopStartup.cs` |
| Domain entities | `src/Libraries/Nop.Core/Domain/` |
| Repository interface | `src/Libraries/Nop.Data/IRepository.cs` |
| Business services | `src/Libraries/Nop.Services/` |
| Admin controllers | `src/Presentation/Nop.Web/Areas/Admin/Controllers/` |
| Plugin examples | `src/Plugins/` |

# Running .NET Tests

## Solution Info
- **Path:** `/Users/asotov/Workspace/nopCommerce/`
- **Framework:** NUnit with .NET 9.0
- **Build output:** `src/Tests/Nop.Tests/bin/Debug/net9.0/Nop.Tests.dll`

## The Problem & Solution

❌ **Don't:** `dotnet test src/NopCommerce.sln` → Slow (rebuilds 34 projects)

✅ **Do:** Build test project + Test the DLL directly

## Core Pattern

```bash
# From repo root (/Users/asotov/Workspace/nopCommerce/)
dotnet build src/Tests/Nop.Tests/Nop.Tests.csproj
dotnet test src/Tests/Nop.Tests/bin/Debug/net9.0/Nop.Tests.dll --filter "[OPTIONAL_FILTER]"
```

**Quick single command:**
```bash
dotnet build src/Tests/Nop.Tests/Nop.Tests.csproj && dotnet test src/Tests/Nop.Tests/bin/Debug/net9.0/Nop.Tests.dll
```

## Test Filtering

| Filter Type | Pattern | Example |
|-------------|---------|---------|
| **Namespace** | `FullyQualifiedName~[Namespace]` | `--filter "FullyQualifiedName~Nop.Tests.Nop.Core.Tests"` |
| **Class** | `FullyQualifiedName~[ClassName]` | `--filter "FullyQualifiedName~ProductServiceTests"` |
| **Method** | `FullyQualifiedName~[MethodName]` | `--filter "FullyQualifiedName~CanCalculatePrice"` |
| **Category** | `Category=[Name]` | `--filter "Category=Integration"` |
| **Exclude** | `Category!=[Name]` | `--filter "Category!=Integration"` |
| **Multiple (OR)** | `(Filter1)\|(Filter2)` | `--filter "(FullyQualifiedName~Product)\|(FullyQualifiedName~Order)"` |
| **Multiple (AND)** | `Filter1&Filter2` | `--filter "FullyQualifiedName~Catalog&Category!=Integration"` |

## Test Namespaces Structure

All tests are in a single project (`Nop.Tests`) organized by layer:

| Test Area | Namespace | Focus |
|-----------|-----------|-------|
| Core Tests | `Nop.Tests.Nop.Core.Tests` | Core business logic, caching, helpers |
| Data Tests | `Nop.Tests.Nop.Data.Tests` | Repository, data provider |
| Services Tests | `Nop.Tests.Nop.Services.Tests` | Business services |
| Web Tests | `Nop.Tests.Nop.Web.Tests` | Controllers, validators, model factories |

### Common Test Namespaces

| Test Area | Full Namespace | Filter Example |
|-----------|----------------|----------------|
| Catalog Services | `Nop.Tests.Nop.Services.Tests.Catalog` | `--filter "FullyQualifiedName~Catalog"` |
| Order Services | `Nop.Tests.Nop.Services.Tests.Orders` | `--filter "FullyQualifiedName~Orders"` |
| Customer Services | `Nop.Tests.Nop.Services.Tests.Customers` | `--filter "FullyQualifiedName~Customers"` |
| Shipping Services | `Nop.Tests.Nop.Services.Tests.Shipping` | `--filter "FullyQualifiedName~Shipping"` |
| Tax Services | `Nop.Tests.Nop.Services.Tests.Tax` | `--filter "FullyQualifiedName~Tax"` |
| Caching Tests | `Nop.Tests.Nop.Core.Tests.Caching` | `--filter "FullyQualifiedName~Caching"` |
| Admin Validators | `Nop.Tests.Nop.Web.Tests.Admin.Validators` | `--filter "FullyQualifiedName~Admin.Validators"` |
| Model Factories | `Nop.Tests.Nop.Web.Tests.Public.Factories` | `--filter "FullyQualifiedName~Public.Factories"` |

## Output Directory

- **Default:** `src/Tests/Nop.Tests/bin/Debug/net9.0/Nop.Tests.dll`
- **Release:** `src/Tests/Nop.Tests/bin/Release/net9.0/Nop.Tests.dll`

## Why This Works

> **The Issue:** Running `dotnet test` on the solution file rebuilds all 34 projects even when only tests changed.
>
> **The Fix:** Build the test project directly, then run tests against the compiled DLL.
>
> **Why Test DLL?** Skips MSBuild overhead, skips solution-wide discovery, runs filtered tests immediately.

## Troubleshooting

**Q:** "Could not find file bin/Debug/net9.0/Nop.Tests.dll"
**A:** Build failed or not run yet - check build output, rebuild project

**Q:** Tests don't reflect recent changes
**A:** DLL outdated - rebuild: `dotnet build src/Tests/Nop.Tests/Nop.Tests.csproj`

**Q:** "No test is available"
**A:** Wrong filter or path - verify DLL exists, check filter syntax, list tests: `dotnet test [dll] --list-tests`

**Q:** Tests fail with database/configuration errors
**A:** Ensure `appsettings.json` is properly configured in the test project directory

## Additional Options

- **Verbose output:** `--logger "console;verbosity=detailed"`
- **List without running:** `--list-tests`
- **Stop on failure:** `-- NUnit.StopOnError=true`
- **Generate TRX:** `--logger "trx;LogFileName=test-results.trx"`
- **Release build:** `dotnet build src/Tests/Nop.Tests/Nop.Tests.csproj -c Release`

## Key Reminders

1. Build test project directly for faster iteration
2. Test DLL directly from `bin/Debug/net9.0/` for speed
3. Use filters for targeted test execution
4. Incremental builds only rebuild changed files
5. nopCommerce has 34 projects - testing the DLL directly avoids rebuilding them all

# TOOLS.md - Tool Selection Guide

## Overview

| Tool | Scope | Speed | IDE Integration | Use Case |
|------|-------|-------|----------------|----------|
| **JetBrains MCP** | IDE-opened projects | ⚡⚡⚡ Fastest (indexed) | ✅ Semantic awareness | Primary choice for all operations |
| **Standard Grep** | Any directory | ⚡ Fast | ❌ No IDE context | Fallback for non-indexed repos |
| **Standard Glob** | Any directory | ⚡ Fast | ❌ No IDE context | File pattern matching |
| **Task (Explore)** | Any directory | ⚡⚡ | ❌ No IDE context | Open-ended exploration |

---

## 1️⃣ JetBrains MCP (Highest Priority)

**Scope:** IDE-opened projects only

**Use when:**
- Any search/exploration in IDE-opened projects
- Need semantic code understanding
- Refactoring (rename symbols, replace text)
- Code quality checks (errors/warnings)

**Key features:**
- Fastest (uses IDE indexes)
- Semantic awareness (understands code structure)
- Language-aware search (precise results)
- Refactoring support (intelligent rename)

**Available tools:**

| Tool | Purpose |
|------|---------|
| `mcp__jetbrains__search_in_files_by_text` | Text/substring search |
| `mcp__jetbrains__search_in_files_by_regex` | Pattern search |
| `mcp__jetbrains__find_files_by_name_keyword` | File search by name (fastest) |
| `mcp__jetbrains__find_files_by_glob` | File search by glob pattern |
| `mcp__jetbrains__get_file_text_by_path` | Read file contents |
| `mcp__jetbrains__replace_text_in_file` | Text replacement with auto-save |
| `mcp__jetbrains__create_new_file` | Create new files |
| `mcp__jetbrains__rename_refactoring` | Semantic symbol renaming (all references) |
| `mcp__jetbrains__get_symbol_info` | Quick Documentation (hover info) |
| `mcp__jetbrains__get_file_problems` | Find errors/warnings |
| `mcp__jetbrains__reformat_file` | Apply IDE formatting |
| `mcp__jetbrains__list_directory_tree` | Explore directory structure |
| `mcp__jetbrains__get_project_modules` | List project modules |
| `mcp__jetbrains__get_project_dependencies` | List libraries/packages |
| `mcp__jetbrains__execute_run_configuration` | Run/debug configurations |
| `mcp__jetbrains__execute_terminal_command` | Run shell commands |

**Example:**
```python
mcp__jetbrains__search_in_files_by_text(
    searchText="IProductService",
    projectPath="/Users/asotov/Workspace/nopCommerce/src",
    fileMask="*.cs",
    directoryToSearch="Libraries"
)
```

**Constraint:** Only works in projects open in IDE

---

## 2️⃣ Standard Grep

**Scope:** Any directory

**Use when:**
- Search in directories not open in IDE
- Fallback when JetBrains MCP unavailable

**When NOT to use:**
- In IDE-opened projects → Use JetBrains instead

**Example:**
```python
Grep("IProductService", path="/Users/asotov/Workspace/external-repo", glob="*.cs")
```

---

## 3️⃣ Standard Glob

**Scope:** Any directory

**Use when:**
- Finding files by pattern (not content)
- Directory structure exploration
- File existence checks

**Example:**
```python
Glob("**/*Tests.cs")
```

---

## 4️⃣ Task Tool (Explore Agent)

**Scope:** Any directory

**Use when:**
- Open-ended exploration requiring multiple searches
- Understanding how a feature works
- Answering architectural questions

**Example:**
```python
Task(subagent_type="Explore", prompt="How is customer authentication implemented?")
```
