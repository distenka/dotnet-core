# ADR-008 — Serialization: System.Text.Json (source-generated) for the client; Newtonsoft.Json stays internal to the processor host for 2.0

| Field | Value |
|---|---|
| Status | **Accepted** — ratifies backlog story DCS-008 |
| Date | 2026-11-01 |
| Backlog story | DCS-008 (Phase 0 — Architecture & Contract Alignment) |
| Related | ADR-001 (package boundary), ADR-003 (`list` descriptor), ADR-005 (target frameworks — client `netstandard2.0` dependencies), DCS-010 (dependency guard), DCS-020 (client project), DCS-022 (error model), DCS-023 (DTOs, `DistenkaJsonContext`, `RunState`), DCS-031/033 (details, cancel) |
| Mirrors | `D:\Product\Distenka` `master` @ **`4c3cf2c`** (`Distenka.Core.API`, `docs/api-*.md`). Re-verify §1.1 whenever `Startup.cs` JSON registration or a public DTO changes. |

## 1. Context

### 1.1 What the platform API puts on the wire (verified in `D:\Product\Distenka` @ `4c3cf2c`, read for this ADR)

- **Serializer.** `Distenka.Core.API/Startup.cs:216` calls `services.AddControllers()` with **no** `AddJsonOptions` and no `AddNewtonsoftJson` — so responses and request bodies use ASP.NET Core 8's System.Text.Json **Web defaults**: camelCase property names, case-insensitive property matching on read, numbers readable from JSON strings (`AllowReadingFromString`), `null` members **written** (no ignore condition). Services that serialize on their own also use `JsonSerializerDefaults.Web` (e.g. the account directory, invocation key store and plugin contract caches); `IIdempotencyStore` adds `WhenWritingNull` for its stored copy only. `Newtonsoft` appears in the platform only in `Distenka.Org.Web` (the website), not in the API.
- **No enum converter, no enum-typed DTO member.** There is no `JsonStringEnumConverter` registration in the API, and no public DTO property is typed as one of the API's enums (checked: none of the 12 `public enum` types in `Distenka.Core.API` is used as a property type under `Controllers/` or `DTOs/`). Every enum-like value is converted to a **string by hand** before it is serialized:
  - `state` — the kernel `RunState` name, PascalCase, via `RunDetailsFacts.StateName` (`RunsQueryController.cs:408`) or `State?.ToString()` (`:605`); documented set `Accepted, Queued, Dispatching, Running, Completed, Failed, Cancelled, TimedOut, Expired`, and `byState` is an object keyed by those names (`docs/api-run-submission.md`).
  - `status` — a free projection word, not a closed set: `"Pending"`, `"Completed"`, **`"Cancellation requested"`** (`RunsQueryController.cs:650`, `docs/api-run-cancellation.md`).
  - `code` — snake_case machine codes (`"scope_violation"`, `"idempotency_key_malformed"`, `"run_already_ended"`); `outcome` — lower-case words (`"allowed"`).
- **Errors** are the platform's own `{ "code": …, … }` bodies, not `ProblemDetails` (no `ProblemDetails`/`ValidationProblem` use in the run controllers) — owned by DCS-022.
- **Timestamps** are ISO 8601 with an explicit offset and up to 6/7 fractional digits (`"2026-10-01T02:40:11.123456+00:00"`).
- **Opaque JSON.** Plugin input schemas, examples and run configurations travel as raw JSON (`JsonNode` / `JsonObject` in `DTOs/PluginPublicationDtos.cs`). The platform **refuses duplicate keys** in plugin input (`PluginInputRules.cs:318-335`, rejection code `Duplicate`), because readers disagree about which duplicate wins.

### 1.2 What the SDK repository does today (HEAD `2ae62fe`)

- `src/Distenka/Distenka.csproj` references **Newtonsoft.Json 13.0.3** and **NJsonSchema 10.9.0** (which itself depends on Newtonsoft). Newtonsoft is used in `Config.cs`, `ConfigReader.cs`, `ConfigWriter.cs` (`ConfigWriter.Settings`, camelCase), `Error.cs`, `ResultLog.cs`, `Util/ProcessConfigConverter.cs`, `Hosting/ListAction.cs` and `Hosting/DescriptorSchemaGenerator.cs`.
- Job authors' config classes may carry Newtonsoft attributes (`[JsonProperty(Required = …)]`, honoured by `ConfigReader.CheckRequiredMembersPresent` since DCS-044) and Newtonsoft converters; `DescriptorSchemaGenerator` already rejects `TypeNameHandling` members (ADR-003).
- The deleted 1.x client (`git show 856048c^:src/Distenka/BroadCast/Run.cs`) mixed `System.Text.Json.Serialization` usings with Newtonsoft converters — the ambiguity this ADR removes.
- `Distenka.Client` does not exist yet (DCS-020).

## 2. Decision

### 2.1 `Distenka.Client` uses System.Text.Json only

1. **No Newtonsoft.Json reference**, direct or transitive, in `Distenka.Client` — nor NJsonSchema, Spectre, System.CommandLine or SignalR (asserted by the `.nuspec` dependency test of DCS-010 AC 3).
2. **Package on `netstandard2.0` only:** `System.Text.Json` **≥ 8.0.5** (8.0.0–8.0.4 are affected by CVE-2024-43485 / GHSA-8g4q-xg66-9fp4). The `net8.0` asset uses the in-box serializer — no `System.Text.Json` package reference — so it is serviced with the runtime. The 8.x package line matches the `Microsoft.Extensions.*` 8.x dependencies of ADR-005 §2.2; consumers may float it higher.
3. **Source generation.** All client contracts are registered on one **internal** `DistenkaJsonContext : JsonSerializerContext` (DCS-023). The client never serializes through reflection-based `JsonSerializerOptions`; this keeps the `net8.0` asset trimming/AOT-safe and the `netstandard2.0` asset free of reflection-emit start-up cost.
4. **Options**, declared once on the context with `[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, …)]`:

   | Setting | Value | Why |
   |---|---|---|
   | Property naming | camelCase | matches the API (§1.1) |
   | Property name matching on read | case-insensitive (Web default) | tolerant of server casing |
   | Number handling | **`Strict`** — overrides the Web default `AllowReadingFromString` | the API writes numbers as numbers; a string where a number is expected is a contract break and must fail loudly |
   | Unknown members | **ignored** (`UnmappedMemberHandling.Skip`, the default) | the platform may add fields without breaking older clients |
   | `null` on write | `WhenWritingNull` | omit optional request members rather than send `null`; a member whose explicit `null` is meaningful opts back in with `[JsonIgnore(Condition = Never)]` |
   | Comments / trailing commas | disallowed (defaults) | server JSON never has them |
   | Max depth | 64 (default) | |
   | `[JsonExtensionData]` | not used on response models | not needed (unknown members are ignored); also the CVE-2024-43485 attack surface |

5. **Dates** are `DateTimeOffset` (never `DateTime`) so the offset the API sends is preserved.

### 2.2 Platform vocabularies: tolerant enums or strings — never a strict enum

`JsonStringEnumConverter` as originally sketched in DCS-008 would **throw** on any value it does not know, so a new platform state or status word would break every deployed client. Given §1.1, the rule is:

| Wire value | Client type | Converter |
|---|---|---|
| A documented, *closed but growing* set the client must branch on — `state` (`RunState`) | C# `enum` with an `Unknown = 0` member | a custom tolerant string converter: case-insensitive name match, unknown string → `Unknown`, the raw string kept on the model where DCS-023 says so; writes the PascalCase name |
| A free projection word — `status` (`"Cancellation requested"`), `outcome` | `string` (optionally with a static class of known constants) | none |
| Machine codes — `code` | `string` (DCS-022 exposes known codes as constants) | none |
| Maps keyed by a vocabulary — `byState` | `IReadOnlyDictionary<string, long>` (typed accessors may be added on top) | none |
| A value only the client sends and the API documents as a closed set | C# `enum` | `JsonStringEnumConverter<TEnum>` (the generic, source-gen-compatible form) |

Numeric enum serialization is never used on the wire.

### 2.3 Opaque JSON on the public surface

Run configuration supplied by the caller, input schemas, examples and result payloads are **arbitrary JSON**. The client exposes them as `System.Text.Json.JsonElement` (immutable, available on `netstandard2.0`) and accepts either a `JsonElement` or an object serialized through a caller-supplied `JsonTypeInfo<T>` / `JsonSerializerOptions` (overloads defined in DCS-030). No Newtonsoft type (`JToken`, `JObject`) appears in any public client signature. The client never builds request JSON by string concatenation, so it cannot emit duplicate keys; it does not add duplicate-key detection on responses (the server is the trusted writer).

### 2.4 The processor host keeps Newtonsoft.Json for 2.0

1. `Distenka` (processor host) keeps **Newtonsoft.Json 13.0.3** and NJsonSchema for the 2.0 line. Reason: job authors' configuration classes, the `[JsonProperty]` required-member rules (DCS-044), `ProcessConfigConverter`, the `list` descriptor generation (ADR-003) and the `get` example (`ConfigWriter.ToExampleJson`) are all built on its contract model; switching serializers would silently change how existing job configs bind — a breaking change for every job, with no benefit to the client.
2. Newtonsoft stays an **implementation dependency of the host**, not part of the client contract: host and client share **no** serializer settings, converters or model types. The host's own settings stay as they are: `ConfigWriter.Settings` camelCase, `TypeNameHandling.None` (polymorphic `TypeNameHandling` members remain rejected by the descriptor generator).
3. **Migration of the host to System.Text.Json** is out of scope for 2.0. It is a candidate for the next major of the `Distenka` package and needs its own story covering: author-attribute compatibility (`[JsonProperty]` → `[JsonPropertyName]`/`[JsonRequired]`), converter rewrites, descriptor generation without NJsonSchema, and a migration guide. Trigger: a Newtonsoft security issue without a fix, or the platform requiring STJ-specific config semantics.

## 3. Consequences

**Positive**
- `Distenka.Client` adds one small, Microsoft-serviced dependency on `netstandard2.0` and none on `net8.0`; consumers who use Newtonsoft (or not) are unaffected.
- New platform states, status words and codes never crash an older client.
- Source generation keeps the `net8.0` client trimming/AOT-compatible.

**Negative / limitations**
- Two serializers exist in the repository (STJ in the client, Newtonsoft in the host); contributors must not move models between them.
- The host's Newtonsoft dependency flows into every job package for the whole 2.0 line.
- The tolerant `RunState` converter is custom code that needs its own tests (DCS-023 AC 2).

## 4. Implementation

| Rule | Story |
|---|---|
| §2.1.1 no Newtonsoft/NJsonSchema/Spectre/System.CommandLine/SignalR in the client `.nuspec` | **DCS-010 AC 3** (dependency test — the "package-dependency test" of DCS-008's AC) |
| §2.1.2 `System.Text.Json` ≥ 8.0.5 on `netstandard2.0` only; central package version | DCS-010 (`Directory.Packages.props`), DCS-020 (client csproj) |
| §2.1.3–2.1.5 `DistenkaJsonContext`, options, `DateTimeOffset` | DCS-023 |
| §2.2 tolerant `RunState`, string `status`/`outcome`, `byState` map | DCS-023 (AC 2 already requires `Unknown` + raw value), DCS-031, DCS-033 |
| §2.2 `code` as string constants; error bodies are not `ProblemDetails` | DCS-022 |
| §2.3 opaque JSON as `JsonElement`; typed-config overloads | DCS-030 |
| §2.4 host keeps Newtonsoft; no code change in 2.0 | — (this ADR) |

## 5. Evidence

Platform (`D:\Product\Distenka` @ `4c3cf2c`, read 2026-11-01, folder access read-only):
- `Distenka.Core.API/Startup.cs:216` — `services.AddControllers();` (no JSON options; no `AddNewtonsoftJson` anywhere in the API).
- `Distenka.Core.API/Controllers/RunsQueryController.cs:408` (`State = RunDetailsFacts.StateName(...)`), `:605` (`State = result.State?.ToString()`), `:650` (`Status = "Cancellation requested"`).
- `Distenka.Core.API/Services/PluginInputRules.cs:318-335` — duplicate keys refused.
- `Distenka.Core.API/Services/IIdempotencyStore.cs:405` — `WhenWritingNull` on the stored copy; `JsonSerializerDefaults.Web` in `Authentication/CachingAccountDirectory.cs:54`, `Authentication/CachingInvocationKeyStore.cs:184`, `Services/PluginContractCache.cs:270`.
- `Distenka.Core.API/DTOs/PluginPublicationDtos.cs` — `[JsonPropertyName]`, `JsonNode`/`JsonObject` for schema and example.
- `docs/api-run-results.md` (`"status": "Completed"`, `"code": "scope_violation"`), `docs/api-invocation.md` (`"status": "Pending"`), `docs/api-account-audit.md` (`"outcome": "allowed"`), `docs/api-run-submission.md` (`byState`, `"code": "idempotency_key_malformed"`), `docs/api-run-cancellation.md` (`"status": "Cancellation requested"`, offset timestamp).
- `Distenka.Org.Web/Distenka.Org.Web.csproj:15` — the only Newtonsoft (`Microsoft.AspNetCore.Mvc.NewtonsoftJson`) reference in the platform.

SDK (this repository, HEAD `2ae62fe`): `src/Distenka/Distenka.csproj:23` (Newtonsoft.Json 13.0.3) and the Newtonsoft users listed in §1.2.

External: CVE-2024-43485 / GHSA-8g4q-xg66-9fp4 — System.Text.Json 8.0.0–8.0.4 affected, fixed in 8.0.5 (models with `[JsonExtensionData]`).
