# ADR-001 — SDK package boundary: Distenka.Processor (job host) and Distenka.Client (platform client)

| Field | Value |
|---|---|
| Status | **Accepted** — records backlog decision AD-1 |
| Date | 2026-10-31 |
| Backlog story | DCS-001 (Phase 0 — Architecture & Contract Alignment) |
| Supersedes | — |
| Related | AD-2 (public contracts only), AD-4 (no tenant/HQ), AD-5 / DCS-005 (target frameworks), DCS-008 (serialization), OQ-7 (package id) |

## 1. Context

The repository `github.com/distenka/dotnet-core` (local `D:\Product\Distenka-sdk`) is described as the "Distenka Client SDK", but at HEAD `3eda536` ("release v1.0.0") it contains a single package:

- `src/Distenka/Distenka.csproj` — `PackageId` `Distenka`, `AssemblyName` `Distenka`, `TargetFramework` `net8.0`, hard-coded `Version` `1.0.0`.
- It is the Runly-derived **processor host**: `Processor<TConfig, …>`, `IProcessor`, `ProcessorHost`, `ServiceExtensions.AddDistenkaProcessor`, the `list` / `get` / `run` command line (`Hosting/*`), the execution pipeline (`Processing/*`) and the in-process test host (`Testing/TestHost.cs`, `Testing/TestRun.cs`).
- The platform executor (`Distenka.Core.Executor`) launches this assembly inside the job sandbox as `dotnet <package>.dll list|run <config>`; `Distenka.dll` ships in `tools/net8.0/any` of every job `.nupkg`.

The only code that talked to the platform — `src/Distenka/BroadCast/*` (SignalR `DistenkaClient`, `ApiKeyProvider`, `x-api-key` header, `Constants.ApiEndpointTemplate = https://{0}.{1}.distenka.ai`) — was removed in commit `856048c` (2025-07-08, *stry-37 "remove client api connection to be added to client package"*, 20 files, 1,483 deletions). The intended client package was never created. `test/Client.Tests/Client.Tests.csproj` (netcoreapp3.1, `Runly.Client.Tests`) still references the non-existent `..\..\src\Client\Client.csproj`.

Two different consumers are therefore conflated under one package identity, and the second one has no package at all:

1. **Job authors** — compile a processor against the host; their code runs inside the platform sandbox with no network credentials of its own.
2. **Applications** — call the Distenka platform's public API (submit / observe / cancel runs, invoke published plugins, read results, verify webhooks).

These consumers have opposite dependency profiles: the host must stay small and runtime-matched to the executor image; the client must be broadly consumable and must never drag job-runtime dependencies into a web application.

## 2. Decision

The SDK is split into **three packages** with a strict, one-directional dependency rule. There is **no project or package reference between the processor host and the client** in either direction.

| Package | Purpose | Target framework(s) | Root namespace |
|---|---|---|---|
| **Distenka.Processor** (NuGet `PackageId` stays `Distenka` — see §2.3) | Runtime linked into every job package; implements the executor contract (`list`, `get`, `run`). | `net8.0` (matches the executor runtime; AD-5) | `Distenka` (unchanged) |
| **Distenka.Client** | Typed HTTP client over the platform's public REST contracts and webhook verification. | `net8.0` + `netstandard2.0` (AD-5; ratified by DCS-005) | `Distenka.Client` |
| **Distenka.Processor.Testing** | `TestHost` / `TestRun` and (DCS-093) the executor-contract harness for processor authors' test projects. | `net8.0` | `Distenka.Testing` (unchanged) |

Existing projects that remain outside this decision's scope: `src/Diagnostics` (sample/diagnostic processors, depends on Distenka.Processor) and `src/Templates` (dotnet new template, depends on Distenka.Processor).

### 2.1 Allowed dependencies

| Package | May depend on | Must never depend on |
|---|---|---|
| Distenka.Processor | `Microsoft.Extensions.*` (Hosting, DependencyInjection, Logging), `System.CommandLine`, `Spectre.Console` (interactive mode only), JSON serializer per DCS-008, `Ben.Demystifier` | Distenka.Client; any HTTP client stack for platform calls; SignalR client |
| Distenka.Client | `System.Text.Json` (DCS-008), `Microsoft.Extensions.Http` / `Options` / `DependencyInjection.Abstractions`, `Microsoft.Bcl.AsyncInterfaces` (netstandard2.0 only), `System.Diagnostics.DiagnosticSource` | Distenka.Processor; `Newtonsoft.Json`; `Spectre.Console`; SignalR client in the core package (a push adapter, if ever approved, ships separately — DCS-072) |
| Distenka.Processor.Testing | Distenka.Processor; `Microsoft.Extensions.*` | Distenka.Client; any test framework (xUnit/NUnit/MSTest) — it must work under all of them |

**Prohibited for every SDK package, without exception:** Orleans (`Microsoft.Orleans.*`), Redis (`StackExchange.Redis` or any Redis client), PostgreSQL / TimescaleDB (`Npgsql`, `Npgsql.*`, EF Core providers), Kubernetes client libraries (`KubernetesClient`), Azure SDKs, RabbitMQ / MassTransit, and any reference to platform source assemblies (`Distenka.Core.*`). These are platform implementation details (project instructions §3; AD-2). The SDK talks to the platform only through the published REST routes and webhooks, and the processor host talks to the executor only through the process contract (arguments, environment, stdout, exit code).

### 2.2 Responsibility matrix

| Concern | Platform | SDK | Consumer application / job author |
|---|---|---|---|
| Scheduling, device triggers, queueing, admission, quotas | Owns (dispatcher, Kueue, CAP-*) | Nothing — exposes no scheduling API | Uses console / public API as provided |
| Sandbox execution, Kubernetes Jobs, infra-failure retries | Owns (CliRunGrain, Executor) | **Processor** behaves per the executor contract: descriptor from `list`, meaningful exit codes from `run`, classifiable stdout (DCS-003/004/009) | Writes processor code |
| Item-level parallelism, per-item retry, failure threshold | Not implemented | **Processor** owns it in-process (`ExecutionConfig`) | Configures per run |
| Input schema publication & validation | Compiles the descriptor; validates invocations (ACC-36) | **Processor** generates the descriptor from the `Config` type | Annotates `Config` |
| Authentication & credential issuance | Issues DK-/DKI- keys, run capabilities; validates JWTs | **Client** sends `Authorization: Bearer`; shape checks; redaction. **Processor** holds no platform credential | Stores credentials securely; obtains JWTs if used |
| Run submission / status / cancel / rerun / results | REST routes, idempotency, ETag | **Client**: typed API, polling helpers, error model, idempotent-only retries (AD-6) | Business logic |
| Real-time events & logs | Console hubs today; public channel TBD (OQ-3) | **Client**: polling + webhook verification; push adapter later | Hosts the webhook endpoint |
| Secrets in job configuration | Owner bindings (ACC-37), scrubber | Never placed in `Config`; documented | Binds secrets server-side |
| Tenancy / environment / licensing | Single account per deployment; self-hosted / on-prem | No tenant, org or environment concept; no HQ or licence-server call; base URL is a required option (AD-4) | Supplies its deployment's base URL |

### 2.3 Package identity (DCS-001 task "Decide PackageId compatibility")

- The processor host project becomes `src/Distenka.Processor/Distenka.Processor.csproj` but **keeps `PackageId=Distenka` and `AssemblyName=Distenka`**. Rationale: every published job package carries `Distenka.dll`; existing processors reference the `Distenka` package; renaming would break them for no functional gain. This is the lower-risk path identified in the story's risk notes.
- The product owner's final word is tracked as **OQ-7**. If the owner chooses a rename to `Distenka.Processor`, DCS-010 renames the `PackageId` only (assembly name `Distenka` and namespaces stay), and the migration guide (DCS-095) documents the switch. Nothing else in this ADR changes.
- New package ids: `Distenka.Client`, `Distenka.Processor.Testing`.

### 2.4 Namespaces

- **Processor host keeps `Distenka`, `Distenka.Hosting`, `Distenka.Processing`.** A new `Distenka.Processor` *namespace* is deliberately **not** introduced: the host's main public types are `Distenka.Processor<TConfig>` … `Distenka.Processor<TConfig, TItem, T1…T16>`; a sibling namespace of the same name makes `Processor<…>` references ambiguous or forces qualification in every existing processor and template. Keeping the namespace makes the split source-compatible for job authors.
- **Testing package keeps namespace `Distenka.Testing`** for `TestHost` and `TestRun` (type-forwarding is not needed because the package is new; source using `using Distenka.Testing;` keeps compiling after adding the package). `Average`, `DataPoint` and `Metric` currently live in `Distenka.Testing` but are used by the runtime (`Execution.ItemProcessingTime`), so they **stay in the processor assembly**; DCS-010 may move them to `Distenka.Processing` with type-forwards.
- **Client uses `Distenka.Client`** with feature sub-namespaces: `Distenka.Client.Runs`, `Distenka.Client.Invocations`, `Distenka.Client.Results`, `Distenka.Client.Account`, `Distenka.Client.Webhooks`, `Distenka.Client.Authentication`, `Distenka.Client.Events`. The `Distenka.Client` namespace never contains host types, so no ambiguity with `Distenka.Processor<…>` arises.
- Leftover `Runly.*` identifiers (e.g. `Runly.Client.Tests`) are not carried forward.

### 2.5 Fate of existing projects

| Item | Action | Story |
|---|---|---|
| `src/Distenka` | Move to `src/Distenka.Processor` (identity per §2.3) | DCS-010 |
| `src/Distenka/Testing/TestHost.cs`, `TestRun.cs` | Move to `src/Distenka.Processor.Testing` | DCS-010 / DCS-093 |
| `src/Distenka/DistenkaApiConfig.cs`, `ExecutionConfig.OrgName/APIKey/Env/RunId/…` | Remove (host never calls the platform) | DCS-045 |
| `test/Client.Tests` (orphan, netcoreapp3.1, references missing `src/Client`) | Delete; real client tests are created with Distenka.Client | DCS-110 |
| `src/Distenka.Client` | New | DCS-020 |
| `InternalsVisibleTo("Distenka.Api.Tests")`, `("Distenka.Cli")` (no such projects) | Review / remove | DCS-010 |

## 3. Consequences

**Positive**
- Job packages stay small and runtime-matched; no HTTP/DI-HTTP stack or credentials enter the sandbox.
- Applications get a dependency-light client that can be used from .NET Framework / older runtimes via netstandard2.0, without pulling Spectre.Console, System.CommandLine or Newtonsoft.
- Each package can be versioned and released independently, while the 2.0.0 release ships them together (DCS-103).
- Existing processor source code compiles unchanged after the split (same package id, assembly name and namespaces).

**Negative / costs**
- Three packages to version, document and test instead of one; CI must pack all of them (DCS-011, DCS-014).
- Concepts shared conceptually by both sides (e.g. run state, disposition) are **not** shared as a common assembly. Duplication is accepted on purpose: the host's disposition is an executor-process concern; the client's `RunState` mirrors the REST contract. A shared contracts package would recouple the two consumers.
- Real-time observation is limited to polling + webhooks until the platform publishes an API-authenticated channel (DCS-007, OQ-3).

**Risks**
- OQ-7 (package id) is still open with the product owner; mitigated by §2.3 making the rename a one-property change.
- OQ-6 (licence of the Runly-derived code) affects the processor and testing packages' metadata (DCS-011), not this boundary.

## 4. Enforcement

- DCS-010 creates the projects according to this ADR.
- DCS-014 (CI dependency scanning) adds an automated guard that fails the build if any SDK `src/**` project references a prohibited package from §2.1 or if Distenka.Client and Distenka.Processor reference each other.
- Pull-request review checks new `PackageReference`s against §2.1; any exception requires a new ADR.

## 5. Evidence

- `git show --stat 856048c` — deletion of `src/Distenka/BroadCast/*` (20 files, 1,483 deletions).
- `test/Client.Tests/Client.Tests.csproj` — `ProjectReference ..\..\src\Client\Client.csproj` (missing).
- `src/Distenka/Distenka.csproj` — `PackageId Distenka`, `AssemblyName Distenka`, `net8.0`, `Version 1.0.0`.
- `src/Distenka/Processor.cs` — public `Processor<TConfig>` … `Processor<TConfig, TItem, T1…T16>` in namespace `Distenka`.
- `src/Distenka/Execution.cs` — uses `Distenka.Testing.Average`.
- Platform: `D:\Product\Distenka\docs\e2e-audit-and-security-assessment.md` §3 (`Distenka.dll` in `tools/net8.0/any` is the Runly-derived SDK); `Distenka.Core.Executor/Program.cs` (verbs and DLL discovery).
- Backlog: `Claude outputs/DISTENKA_CLIENT_SDK_BACKLOG.html` — AD-1, AD-2, AD-4, AD-5; OQ-6, OQ-7.
