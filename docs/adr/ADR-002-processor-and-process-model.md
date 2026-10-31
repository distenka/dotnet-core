# ADR-002 — Processor and process model: one package = one processor exposing N processes; process discovery and selection through the ACC-36 descriptor

| Field | Value |
|---|---|
| Status | **Accepted** — records backlog decision AD-7 |
| Date | 2026-10-31 |
| Backlog story | DCS-002 (Phase 0 — Architecture & Contract Alignment) |
| Supersedes | — |
| Related | ADR-001 (package boundary), AD-3 (ACC-36 descriptor dialect, executor contract), AD-7 (processor/process identity), OQ-1 (resolved by AD-7), **OQ-9 (per-process schema — open)**, DCS-003 (descriptor mapping), DCS-043 (`list` implementation), DCS-044 (`run` resolution) |

## 1. Context

### 1.1 What the SDK does today (verified in this repository, HEAD `3eda536`)

- **Discovery.** `Hosting/ProcessCache.FindProcessTypes` collects *every* non-interface type implementing `IProcessor` in the scanned assemblies (by default the calling assembly — `ServiceExtensions.AddDistenkaProcessor(args)` → `Assembly.GetCallingAssembly()`). Abstract types and open generic definitions are included too; they are only flagged through `ProcessInfo.Errors` / `IsValid`.
- **Selection.** `ConfigReader.FromJson` reads the configuration's `process` member (case-insensitive) in two forms:
  - *reduced form* — `"process": "Namespace.Type"` (a string), and
  - *full form* — `"process": { "type": "…", "package": "…", "version": "…" }` (the serialized `ProcessorConfig`).
  It then calls `ProcessCache.Get(string)` → `ResolveProcessInfo`, which matches `Type.Name` or `Type.FullName`, first exactly, then case-insensitively, and throws `TypeNotFoundException` unless exactly one type matches.
- **Defects this ADR must account for (fixed by DCS-044, not here):**
  - A configuration without `process` throws `NullReferenceException` in `IsReducedForm` (`GetProcess(...)` returns `null`, `.Type` is dereferenced) — before the full-form path's `.ToObject<ProcessorConfig>()` NRE is even reached.
  - An *ambiguous* short name (two process types with the same `Name` in different namespaces) is reported as `Type X not found` — indistinguishable from an unknown name.
  - `ResolveProcessInfo` can resolve an abstract or generic-definition type; the failure then surfaces later at activation.
  - The `ProcessInfo == null` check in `FromJson` is dead code: `Get(string)` throws rather than returning `null`.
- **Command line.** `list` (`--json`, `--verbose`), `get <type> [filePath]`, `run <configPath> [resultsPath]` and one sub-command per process type (lower-cased `Type.Name`, aliased to `Type.Name`) — inherited from Runly. Only `list` and `run` are invoked by the platform.
- **The repository's own sample is already multi-process.** `src/Templates/samples/SubscriptionServiceProcessor` is one package with three processes — `ActivateSubscriptionProcess` (`SubscriptionId: int`), `AddBankAccountProcess` (`UserId: int`, `AccountNumber`, `RoutingNumber: string`), `CreateSubscriptionProcess` (`UserId: int`, `PlanId: int`) — and its sample configuration uses the reduced form `"process": "SubscriptionServiceProcessor.CreateSubscriptionProcess"`.

### 1.2 What the platform does (recorded during the backlog assessment of `D:\Product\Distenka` @ `4c3cf2c`; the platform repository is not connected to this session and was not re-read)

- One uploaded package = one plugin = one `processorId` (`PluginService.Create`: `PluginName = packageId`); `POST /api/runs` takes `{ processorId, configurationJson }`; the executor picks the DLL by package name (`Distenka.Core.Executor/Program.cs FindDll`).
- The executor runs `dotnet exec <dll> list` once at validation time and keeps the first balanced JSON object on stdout; that object is stored as the plugin's single `published_schema`.
- `PluginInputSchema.TryParse` accepts only a root map *property name → descriptor* with the keywords `type` (string | number | integer | boolean | object | array), `required`, `description`, `nullable`, `minimum`, `maximum`, `minLength`, `maxLength`, `minItems`, `maxItems`, `enum` (strings), `items`, `properties`. `PluginInputRules.Validate` rejects properties that are not declared (when `HasPublishedSchema`, on `POST /api/runs` and `POST /v1/invoke`).
- The platform already treats the configuration's `process` **string** as the run's process name: `RunProcessName.From(configJson)` (`Distenka.Core.Kernel/Execution/RunAcceptedNotice.cs`), surfaced by `RunsQueryController` list/details and the accepted-run notice.

### 1.3 Decision being recorded

Product-owner Decision 1 (2026-10-31) → **AD-7**: *one package = one processor; a processor exposes multiple processes; `processorId` identifies the processor/package and a separate process identifier identifies the process; the two are never conflated; the SDK must not assume one package = one process.*

This ADR turns AD-7 into rules the host can implement **using only existing contracts**: the `process` field the platform already reads, and keywords the ACC-36 dialect already accepts. It invents no descriptor format.

## 2. Decision

### 2.1 Definitions

| Concept | Definition | Identified by | Owned by |
|---|---|---|---|
| **Processor** | The uploaded job package (`.nupkg`) and the host assembly built from it. | `processorId` = the package id (platform `PluginName`). | Platform. The SDK host never reads, receives or emits `processorId`. |
| **Process** | One concrete, valid `IProcessor` implementation inside the processor (non-abstract, not an open generic definition — `ProcessInfo.IsValid`). | The configuration's `process` string (§2.2). | SDK host (discovery and resolution). |
| **Process set** | All valid processes discovered in the scanned assemblies. Invalid types are diagnostics, never members. | — | SDK host. |

`processorId` is never used to name a process, and a process name is never used to address a package. A package with one process is the degenerate case of the same model, not a different model.

### 2.2 Process identifier contract

1. **Field.** The process is named by the configuration's top-level `process` member, a **string**. The member name is matched case-insensitively (existing behaviour, kept).
2. **Canonical name.** The canonical identifier of a process is its CLR `Type.FullName` (e.g. `SubscriptionServiceProcessor.CreateSubscriptionProcess`). It is unique within a process set by construction for a single assembly; if several scanned assemblies contribute the same `FullName`, the package is invalid and `list` fails (§2.3 rule 6).
3. **Accepted forms at `run` / `get`.** The host resolves, in order:
   1. exact `FullName`;
   2. exact short name (`Type.Name`);
   3. case-insensitive `FullName` or short name.
   A step that yields exactly one valid process wins; a step that yields several is an **ambiguity error** naming the candidates' canonical names; a step that yields none falls through. No match after step 3 is an **unknown-process error** listing the canonical names.
4. **Platform-facing form.** Because the published descriptor enumerates canonical names (§2.3), a configuration submitted through `POST /api/runs` or `POST /v1/invoke` against a package with a published schema must use the **canonical** name; short and case-variant forms are a host convenience for local runs, `get`, tests and packages without a published schema. (`PluginInputRules.ValidateString` compares `enum` values ordinally — case-sensitive — verified in `D:\Product\Distenka` @ `4c3cf2c` during DCS-003.)
5. **Legacy full form.** The object form `"process": { "type", "package", "version" }` stays readable by the host for 1.x configuration files, resolving on `type` only (`package` and `version` are ignored — package identity belongs to the platform). It is **not** part of the platform contract: the descriptor declares `process` as `string`, and the platform reads the process name only from a string. New tooling (`get`, templates, samples) emits the reduced string form only.

### 2.3 `list`: describing the process set in the ACC-36 dialect

The descriptor printed by `list` (shape and CLR mapping owned by ADR-003 / DCS-003; implementation DCS-043) describes the process set as follows:

1. **`process` descriptor — always emitted**:
   ```json
   "process": {
     "type": "string",
     "required": <true when the process set has more than one member, otherwise false>,
     "enum": [<canonical names of all processes, ordinal order>],
     "description": "Process to run. One of: <canonical names, comma-separated>."
   }
   ```
   It is emitted for single-process packages too (with `required: false` and a one-element `enum`) because `PluginInputRules` rejects undeclared properties: omitting it would make every existing configuration that carries `"process": "…"` — the SDK's own samples and `get` output included — fail platform validation. This satisfies the "absent or optional" wording of DCS-043's acceptance criteria by choosing *optional*.
2. **Process properties — the union.** The remaining root properties are the union of the process-specific configuration properties of every process in the set. Which members of a `Config` subclass count as process properties (versus host-owned members such as `Execution`, `Process` and the removed infrastructure fields) is ADR-003's mapping, not this ADR's.
3. **Merging identical properties.** A property declared by several processes appears once. Two declarations are *compatible* when their descriptors are equal after ignoring `description` and `required`; `description` is taken from the first declaring process in ordinal canonical-name order that has one.
4. **`required` in the union.** A union property is `required: true` only when **every** process in the set declares it and requires it. Otherwise it is optional in the descriptor; the per-process requirement is enforced by the host at `run` against the resolved process's configuration type (DCS-044), failing with exit code 1 and an `ERROR:` line.
5. **Conflicts.** Two incompatible declarations of the same property name (different `type`, `enum`, bounds, `items` or `properties`) make the package **undescribable**: `list` writes nothing to stdout, writes an `ERROR:` line naming the property and the conflicting processes, and exits 1 (the platform treats any non-zero `list` exit as a rejection). The SDK does not rename, prefix or nest properties to dodge the conflict — that would be a new descriptor convention (see OQ-9).
6. **Empty or invalid sets.** No valid process, or two valid processes with the same canonical name, → `list` fails the same way. Invalid `IProcessor` types (abstract, open generic) are reported as diagnostics on stderr and never appear in `enum`.
7. **Processor identity never appears** in the descriptor — no `processorId`, package id or version.
8. **Only dialect keywords.** Every key written is one of the ACC-36 keywords listed in §1.2.

Worked example — the repository's `SubscriptionServiceProcessor` sample (three processes, no conflicts) produces `process` (`required: true`, three-element `enum`) plus `SubscriptionId: integer`, `UserId: integer`, `AccountNumber: string`, `RoutingNumber: string`, `PlanId: integer`, all optional at the descriptor level (`UserId` is declared by two of the three processes only). Property-name casing follows ADR-003.

### 2.4 `run`: resolving the process

| Configuration | Process set | Result |
|---|---|---|
| `process` absent or `null` | exactly one process | Run that process. |
| `process` absent or `null` | two or more | Exit 1: `ERROR: configuration does not name a process and the package contains N processes (<canonical names>)`. |
| `process` present | any | Resolve per §2.2 rule 3; ambiguity or unknown name → exit 1 with an `ERROR:` line listing the canonical names. |
| `process` is neither string, object (legacy form) nor null | any | Exit 1: `ERROR:` naming the invalid member. |

Additional rules:

- Resolution never consults `processorId` (the host does not receive it) and never falls back to "the first process".
- Properties in the document that belong to other processes of the union are not an error at `run` (the platform has already accepted the document against the union); they are ignored by deserialization into the resolved configuration type. DCS-044 may log them as a warning on the diagnostic channel.
- No stack trace reaches stdout for any of these errors; the error classes and exit codes follow the executor contract (ADR-004 / DCS-004).

### 2.5 Other verbs

- `get <type>` and the per-process sub-commands stay as **local developer conveniences**. They are not part of the executor contract, use the resolution rules of §2.2, and emit the reduced string form.
- The Runly `list --json` / `--verbose` listing of default configurations is not the platform contract; its fate (kept behind a flag or removed) is decided by ADR-003 / DCS-003.

## 3. Consequences

**Positive**

- AD-7 is implementable now with no platform change: every multi-process package whose processes do not conflict becomes describable and invocable.
- Every existing 1.x reduced-form configuration (`"process": "Namespace.Type"`) stays valid at the host and passes the published descriptor.
- The NRE on configurations without `process` is replaced by a defined rule for both the single- and multi-process case.
- Processor identity and process identity are separate in the ADR, in the descriptor and in the resolution rules — the condition DCS-043 and DCS-044 are tested against.

**Negative / limitations (recorded, not bridged)**

- **OQ-9 — per-process schema is a platform-contract gap.** ACC-36 stores one descriptor map per plugin. With the union rule:
  - per-process `required` cannot be expressed; the platform accepts a document that lacks a property the selected process requires, and the failure surfaces only at `run` (exit 1) instead of at submission (HTTP 400);
  - the platform accepts properties that the selected process does not have;
  - packages whose processes declare the same property name with incompatible descriptors cannot be published at all.
  Single-process packages are unaffected. Resolving OQ-9 needs a platform decision — (a) keep the union convention as the documented contract, (b) extend ACC-36 with per-process schemas, or (c) restrict publication to one process per package (contradicts AD-7). This ADR implements (a) as the interim and must be revisited when OQ-9 is answered.
- A single-process run submitted without `process` is recorded by the platform without a process name (`RunProcessName.From` finds nothing). Callers who want the name shown in run lists should send the canonical name; the host cannot rewrite the stored configuration.
- Platform-validated submissions must use canonical (`FullName`) names; short names are a host-side convenience only (§2.2 rule 4).
- Renaming or moving a process class changes its canonical name and therefore the published `enum` — a breaking change for that package's callers, to be called out in the template and README (DCS-090).

## 4. Implementation and verification

| Rule | Story | Verified by |
|---|---|---|
| §2.2 resolution order, ambiguity vs unknown errors, valid-only resolution, legacy object form | DCS-044 | DCS-044 unit tests; DCS-100 executor-contract suite |
| §2.3 `process` descriptor, union, `required` intersection, conflict failure, invalid-type diagnostics | DCS-043 (mapping per ADR-003 / DCS-003) | DCS-043 golden files for one- and two-process assemblies (plus a conflicting pair); DCS-100 |
| §2.4 defaulting, error lines, per-process `required` at `run`, exit codes | DCS-044 with ADR-004 / DCS-040 | DCS-044 / DCS-100 |
| §2.5 `get` emits reduced form only | DCS-044 (resolution) / DCS-092 (samples) | DCS-092 |

## 5. Evidence

- `src/Distenka/ConfigReader.cs` — `FromJson`, `IsReducedForm`, `GetProcess` (reduced/full forms; NRE when `process` is missing).
- `src/Distenka/Hosting/ProcessCache.cs` — `FindProcessTypes` (includes invalid types), `ResolveProcessInfo` (Name/FullName, exact then case-insensitive, `TypeNotFoundException` for both none and many).
- `src/Distenka/Hosting/ProcessInfo.cs` — `IsValid`, `ProcessLoadErrors` (abstract, interface, generic definition).
- `src/Distenka/ProcessorConfig.cs`, `src/Distenka/Util/ProcessConfigConverter.cs` — string ↔ `{ type, package, version }`.
- `src/Distenka/ServiceExtensions.cs:124-165` — `list` / `get` / `run` and per-process sub-commands.
- `src/Distenka/Hosting/ListAction.cs`, `GetAction.cs` — current `list` output (default configs) and `get`.
- `src/Templates/samples/SubscriptionServiceProcessor/*` — three-process sample; `CreateSubscriptionProcess.json` uses the reduced form.
- Platform (recorded in the backlog, assessment of `D:\Product\Distenka` @ `4c3cf2c`): `Distenka.Core.Kernel/Execution/RunAcceptedNotice.cs` (`RunProcessName.From`); `Distenka.Core.API/Controllers/RunsQueryController.cs:258-342, :426, :471`; `Distenka.Core.API/Services/PluginInputSchema.cs`; `PluginInputRules.cs:119`; `Distenka.Core.API/Controllers/RunsController.cs:293-296`; `Distenka.Core.Executor/Program.cs:400-425` (`FindDll`), `:598` (`list`).
- Backlog decisions: AD-3, AD-7 (Decision 1, 2026-10-31); open question OQ-9.
