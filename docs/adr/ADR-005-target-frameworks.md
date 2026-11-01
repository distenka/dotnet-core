# ADR-005 — Target frameworks: `net8.0` for everything that runs inside the executor; `net8.0` + `netstandard2.0` for the client

| Field | Value |
|---|---|
| Status | **Accepted** — ratifies backlog decision AD-5 |
| Date | 2026-11-01 |
| Backlog story | DCS-005 (Phase 0 — Architecture & Contract Alignment) |
| Related | ADR-001 (package boundary — TFM column), AD-5, ADR-004 (executor launch), DCS-008 (serialization — client dependencies), DCS-010 (csproj TFMs, `global.json`), DCS-014 (CI matrix), DCS-015 (template), DCS-020 (client project), DCS-051 (sandbox constraints doc), DCS-110 (orphan `test/Client.Tests`) |
| Mirrors | `D:\Product\Distenka` `master` @ **`4c3cf2c`**; `Distenka.Core.Executor/Dockerfile` last changed in **`6df4bd4`** (2026-09-30). Re-verify this ADR whenever that file changes. |

## 1. Context

### 1.1 What runs the processor (verified in `D:\Product\Distenka` @ `4c3cf2c`, read for this ADR)

- **Runtime image.** `Distenka.Core.Executor/Dockerfile` builds with `mcr.microsoft.com/dotnet/sdk:8.0` and runs on **`mcr.microsoft.com/dotnet/runtime:8.0`** — the *runtime-only* image: the `Microsoft.NETCore.App` 8.x shared framework and nothing else. There is **no ASP.NET Core shared framework** (`Microsoft.AspNetCore.App`) and no Windows Desktop framework in the pod. The executor itself targets `net8.0` (`Distenka.Core.Executor.csproj`). Every other platform service also builds on `sdk:8.0`.
- **Launch.** The executor starts the job with `dotnet` as the file name (`Program.cs:222` for `run`, `:597` for `list`) and the package's main DLL as the argument — i.e. framework-dependent execution on the image's .NET 8 runtime.
- **Which DLL.** `FindDll` (`Program.cs:400-424`, and `IPluginService.GetMainDllPath` `:1343` at upload) scans **every** `*.dll` under the extracted package regardless of folder, takes the first whose file name equals the package id, else the first that contains it, else prefers `lib` then `tools` folders. It does **not** look at target-framework folders: a package that ships the same assembly under several TFM folders (`tools/net8.0/any`, `tools/net9.0/any`, …) gets whichever the file system lists first.
- **Roll-forward.** A framework-dependent app's default `rollForward` is `Minor`: it runs on a newer *minor/patch* of its own major version only. A `net9.0` (or later) app does not start on the 8.0 runtime; a `net6.0`/`net7.0` app does not start on it either unless it opts into `RollForward=Major`.

### 1.2 What the SDK repository targets today (HEAD `2ae62fe`)

| Project | Target | Note |
|---|---|---|
| `src/Distenka` (processor host, PackageId `Distenka`) | `net8.0` | matches the executor |
| `src/Diagnostics`, `test/Distenka.Tests`, `test/Distenka.Tests.Cli` | `net8.0` | |
| `src/Templates/samples/SubscriptionServiceProcessor` | `net8.0` | references the published `Distenka 1.5.0-beta` |
| `src/Templates/templates/App/NewDistenkaApp.csproj` | **`netcoreapp3.1`** | end of life Dec 2022; would not start on the executor (§1.1 roll-forward) |
| `test/Client.Tests` (orphan, not in the solution) | **`netcoreapp3.1`** | references a non-existent `src/Client` project |
| `Distenka.Client`, `Distenka.Processor.Testing` | — | do not exist yet (DCS-020, DCS-093) |

No `global.json` pins the SDK.

## 2. Decision

### 2.1 Everything that runs inside the executor targets exactly `net8.0`

1. **`Distenka` (processor host)** targets `net8.0` — single target, no multi-targeting. It runs in the job's process on the executor's runtime, so its TFM is dictated by the executor image, not chosen for consumer reach.
2. **Job packages** — the template's generated app, the samples, `Distenka.Diagnostics`, the test CLI — target **`net8.0` with a single `<TargetFramework>`**. Multi-targeting a job package is not supported: the executor's `FindDll` ignores TFM folders (§1.1), so the assembly that runs would be chosen by file-system order. Authors pack with `PackAsTool=true`, which places the app under `tools/net8.0/any`.
3. **Forbidden in the processor host and in job packages:** a `FrameworkReference` to `Microsoft.AspNetCore.App` or `Microsoft.WindowsDesktop.App`, and any dependency that requires them — those frameworks are not in the runtime image and the job fails at start-up. (Packages that merely *reference* ASP.NET-related NuGet packages without the shared framework are unaffected.)
4. **No `RollForward` setting** is emitted by the SDK. A job built for `net8.0` runs on the image's 8.0.x runtime with the default `Minor` policy; patch updates of the image are picked up automatically.
5. **`Distenka.Processor.Testing`** targets `net8.0`: it is consumed by processor authors' test projects, which target what their job targets.

### 2.2 The client targets `net8.0` and `netstandard2.0`

`Distenka.Client` is consumed by *other* applications (web apps, services, scripts) that call the platform's REST API; its TFM is chosen for consumer reach, not dictated by the executor.

- **`netstandard2.0`** — reaches .NET Framework (supported minimum: **4.7.2**; 4.6.1–4.7.1 technically implement `netstandard2.0` but have known binding problems and are not supported), .NET Core 2.x/3.x, Mono/Xamarin and Unity. Dependencies on this target only: `System.Text.Json` and `Microsoft.Bcl.AsyncInterfaces` packages (DCS-008), `Microsoft.Extensions.Http` / `.Options` / `.DependencyInjection.Abstractions` 8.x (all ship `netstandard2.0` assets), `System.Diagnostics.DiagnosticSource`. No polyfill package beyond these without a recorded reason.
- **`net8.0`** — modern consumers (.NET 8 and later; a `net9.0`/`net10.0` app picks this asset) get the in-box `System.Text.Json`, `IAsyncEnumerable<T>`, `HttpClient` APIs and trimming/AOT annotations without shim packages.
- **Not chosen:** `netstandard2.1` (not implemented by .NET Framework — would lose the reason to multi-target); a separate `net9.0`/`net10.0` asset (no API need; adds build/test cost); dropping `netstandard2.0` (cuts off .NET Framework callers, which the client exists to serve as much as .NET 8 ones).
- Code shared across both targets must compile on `netstandard2.0`: no default interface members, no `init`/`required`/records without polyfill attributes, conditional compilation (`#if NET8_0_OR_GREATER`) only where an API is unavailable.

### 2.3 Build toolchain

- **`global.json`** (DCS-010) pins the **8.0 SDK feature band** with `rollForward: latestFeature`, so local and CI builds use the same compiler as the executor's build stage (`sdk:8.0`). `LangVersion` stays the SDK default (C# 12).
- **Tests:** all test projects target `net8.0`. The client's tests additionally run on **`net472`** on a Windows CI runner so the `netstandard2.0` asset is exercised, not just compiled (DCS-014).

### 2.4 Re-evaluation trigger

This ADR follows the executor image, not the newest .NET release. When `Distenka.Core.Executor/Dockerfile` moves to another runtime major version:

1. the processor host, `Distenka.Processor.Testing`, the template and the samples move to that TFM **in the same release** (a new major of the `Distenka` package);
2. existing job packages built for `net8.0` stop starting on the new image (§1.1 roll-forward) unless the platform keeps an 8.0 runtime available or the executor sets `DOTNET_ROLL_FORWARD=Major` — that is a **platform** decision with a migration plan for owners, to be raised before the image changes, not something the SDK can absorb;
3. the client's TFMs are re-evaluated independently (consumer reach), normally by adding the new LTS TFM.

## 3. Consequences

**Positive**
- A package built from the SDK template or samples starts on the executor; the `netcoreapp3.1` template defect has a defined fix.
- One TFM per job package removes the `FindDll` ambiguity.
- The client is usable from .NET Framework 4.7.2+ and .NET 8+ with a small, justified dependency set.

**Negative / limitations**
- Processor authors cannot use .NET 9/10 APIs inside jobs until the platform moves its image.
- Two client targets mean conditional code in a few places and a Windows leg in CI.
- ASP.NET Core shared-framework features (e.g. `Microsoft.AspNetCore.*` hosting inside a job) are unavailable in jobs; documented in DCS-051.

## 4. Implementation

| Rule | Story |
|---|---|
| §2.1.1, §2.1.5, §2.2 csproj `TargetFramework(s)`; §2.3 `global.json` | DCS-010 (projects), DCS-020 (client project), DCS-093 (testing package) |
| §2.1.2 template `netcoreapp3.1` → `net8.0`, single TFM, `PackAsTool` | DCS-015 |
| §2.3 CI: build both client TFMs; run client tests on `net8.0` and `net472` (Windows) | DCS-014 |
| §2.1.3 dependency guard (no `Microsoft.AspNetCore.App` / `WindowsDesktop` framework reference in host or template) | DCS-010 (nuspec/dependency test) |
| §2.1.2–2.1.4 author-facing rules (single TFM, no ASP.NET Core framework, no RollForward) | DCS-051, DCS-090 |
| Orphan `test/Client.Tests` (`netcoreapp3.1`) | DCS-110 (delete) |

## 5. Evidence

Platform (`D:\Product\Distenka` @ `4c3cf2c`, read 2026-11-01, folder access granted read-only):
- `Distenka.Core.Executor/Dockerfile` (last changed `6df4bd4`, 2026-09-30) — `FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build`, `FROM mcr.microsoft.com/dotnet/runtime:8.0`, `ENTRYPOINT ["dotnet", "Distenka.Core.Executor.dll"]`.
- `Distenka.Core.Executor/Distenka.Core.Executor.csproj` — `<TargetFramework>net8.0</TargetFramework>`.
- `Distenka.Core.Executor/Program.cs:222`, `:597` (`FileName = "dotnet"`), `:400-424` (`FindDll`).
- `Distenka.Core.API/Services/IPluginService.cs:1343` (`GetMainDllPath`, same name-based scan at upload).
- `Distenka.Callbacks`, `Distenka.Core.API`, `Distenka.Core.Host`, `Distenka.Core.Persistence`, `Distenka.Dispatcher`, `Distenka.Org.Web` Dockerfiles — all `sdk:8.0`.
- `Nuget Sample/src/Distenka-Sample-Echo/Distenka-Sample-Echo.csproj` — reference job: `net8.0`, single TFM, `PackAsTool=true`.

SDK (this repository, HEAD `2ae62fe`): the `TargetFramework` values listed in §1.2.
