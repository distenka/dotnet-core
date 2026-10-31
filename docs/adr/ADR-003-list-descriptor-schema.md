# ADR-003 — `list` emits the ACC-36 descriptor schema: CLR → descriptor mapping, rejection rules and output channel

| Field | Value |
|---|---|
| Status | **Accepted** — records backlog decision AD-3 (schema half) |
| Date | 2026-10-31 |
| Backlog story | DCS-003 (Phase 0 — Architecture & Contract Alignment) |
| Supersedes | The Runly `list` output (banner + default configurations) and the NJsonSchema JSON Schema output of `list --verbose --json` |
| Related | ADR-002 (process model — `process` descriptor and union rules), AD-3, AD-7, **OQ-9 (per-process schema — open)**, DCS-043 (implementation), DCS-044 (`run`-time checks), DCS-045 (removed config fields), DCS-012 (dependencies), DCS-093 / DCS-100 (validator port and contract tests) |

## 1. Context

### 1.1 What the platform does with `list` (verified in `D:\Product\Distenka` @ `4c3cf2c`, read for this ADR)

- **Invocation.** At package validation (ACC-24) the executor runs `dotnet exec <dll> list` — no flags — in the run sandbox: cleared environment (`DOTNET_NOLOGO`, `DOTNET_CLI_HOME=/tmp`, `HOME=/tmp`, `PATH`, `COMPlus_GCHeapHardLimit=0x10000000`), working directory = the DLL folder, **60-second timeout** (`Distenka.Core.Executor/Program.cs:558-645`).
- **Failure channel.** A non-zero exit rejects the package with *"The package exited with code N when asked to describe its configuration."* followed by **stderr**, truncated to 2000 characters (`:649-657`). Stdout is not consulted on failure.
- **Success channel.** On exit 0 the executor keeps the **first balanced JSON object** on stdout (`ExtractJsonObject`, `:676-704`). The scan counts raw `{` and `}` characters **without regard to JSON strings**: a brace inside a string value shifts the balance, and the candidate then fails to parse → *"The package produced no JSON configuration object on standard output."* That object is pinned as `plugins.published_schema` (ACC-33).
- **Dialect.** `PluginInputSchema.TryParse` (`Distenka.Core.API/Services/PluginInputSchema.cs`) compiles the pinned object and **fails closed**:
  - root = JSON object mapping property name → descriptor object; an empty root is valid (the job takes no input);
  - descriptor keywords ⊆ `Keywords` = { `type`, `required`, `nullable`, `description`, `default`, `enum`, `minimum`, `maximum`, `minLength`, `maxLength`, `minItems`, `maxItems`, `items`, `properties` }; any other keyword → unusable;
  - `type` required, one of `string`, `number`, `integer`, `boolean`, `object`, `array`;
  - `required`, `nullable`: JSON booleans, default `false`;
  - `description`: string; carried for consumer docs (ACC-40), never enforced;
  - `default`: **accepted but never read** — not compiled, not enforced, not shown;
  - `minimum`/`maximum`: numbers, only on `number`/`integer`; `minLength`/`maxLength`: whole numbers ≥ 0, only on `string` (UTF-16 length); `minItems`/`maxItems`: whole numbers ≥ 0, only on `array`; min ≤ max for each pair;
  - `enum`: non-empty array of strings, only on `string`;
  - `items`: required for and only for `array` (a full descriptor); `properties`: required for and only for `object` (a map of descriptors, which may be empty — an object with no `properties` keyword at all is refused);
  - property names: non-blank, unique;
  - nesting: the schema text is parsed with `JsonDocument` `MaxDepth = MaxSchemaDepth` and descriptor nesting is checked against the same limit; `MaxSchemaDepth` defaults to **16** (`PluginInputOptions.cs:56`, `appsettings.json:28`) and is deployment-configurable.
- **Validation of callers** (`PluginInputRules.Validate`, used by `POST /v1/invoke` and by `POST /api/runs` when the plugin has a published schema): property names are matched **ordinally (case-sensitive)**; undeclared properties are rejected (`property.unknown`); `required` properties must be present unless owner-bound; `null` is rejected unless `nullable`; `integer` values must fit `Int64`, `number` values `double`; `enum` comparison is **ordinal**; arrays validate each element against `items`; nested objects validate recursively with the same unknown-property rule.
- **Owner bindings** (ACC-37, `IPluginBindingService.CheckAgainstPublishedSchema`): an owner can only bind a key the published schema declares at the root; bound keys cannot be supplied by callers and satisfy `required`.

### 1.2 What the SDK does today (this repository, HEAD `3eda536`)

- `Hosting/ListAction` (no flags) prints `BroadCast Version: v…`, separators and one reduced **default configuration instance** per process — the first JSON object on stdout is therefore something like `{ "userId": 0, "planId": 0, "process": "…" }`, which `TryParse` refuses (*"'userId' is described by a number rather than by a descriptor object"*). Every package built with the SDK is uninvocable.
- `list --json` prints a Runly listing `{ clientVersion, Processes: [...] }`; `list --verbose --json` adds an NJsonSchema **JSON Schema** per config type (`Hosting/JsonSchema.Generate`) — a different dialect (`oneOf`, `$ref`, `definitions`, …) the platform also refuses.
- Configs are written with Newtonsoft and `ConfigWriter.Settings` (`DefaultContractResolver` + `CamelCaseNamingStrategy`, dictionary keys untouched) and read with Newtonsoft `JObject.ToObject(configType)` (case-insensitive member matching).
- `Config` (base) declares `Process` (`[Required]`, `ProcessConfigConverter`), `Execution` (`ExecutionConfig`) and `DistenkaApi` (`DistenkaApiConfig`, removed by DCS-045).

## 2. Decision

### 2.1 Output contract of `list` (no flags)

1. **Stdout** carries exactly one JSON object — the descriptor map — followed by a single newline, and **nothing else** (no banner, no version line, no blank lines before it, no logging). Compact (single-line) serialization.
2. **Braces in strings are escaped.** Every `{` and `}` occurring inside a JSON string (property names, `description`, `enum` values) is written as `\u007B` / `\u007D`. This is valid JSON, decodes to the same text, and keeps the executor's brace-counting extractor balanced. No other escaping changes.
3. **Exit codes.** `0` = descriptor written. `1` = the package cannot be described; stdout is **empty**, and each reason is one `ERROR: <member path>: <reason>` line on **stderr** (the only channel the platform shows the owner). No stack traces. The total stderr text should stay well under the platform's 2000-character display limit (at most 10 reasons, then `ERROR: … and N more`).
4. **No side effects.** `list` reflects over types only: it does not construct processors, resolve processor dependencies, read `appsettings.json` secrets, open files other than the assemblies, or touch the network. It must complete far inside the platform's 60-second limit. (Creating config instances is not needed for the mapping and is not done.)
5. **Determinism.** The same assemblies always produce byte-identical output: `process` first, then root properties in first-declaration order across processes taken in ordinal canonical-name order (ADR-002 §2.3); nested `properties` in declaration order (base class first, as Newtonsoft orders them).
6. **Depth.** The emitted JSON document nests at most **16** levels (the platform default `MaxSchemaDepth`; one nested `object` costs two JSON levels — descriptor + `properties` map — and `items`/`enum` one each). A deeper config type fails `list` with exit 1. Deployments that lower `MaxSchemaDepth` below 16 are outside what the SDK can know; the limit is documented.

### 2.2 Which members become root properties

- **Process properties.** For each process (ADR-002), the members of its configuration type that Newtonsoft would deserialize — derived from the `JsonObjectContract` of `ConfigWriter.Settings`' contract resolver: public properties and fields that are writable (setter or Newtonsoft-settable) and not `[JsonIgnore]` — **excluding every member declared on `Distenka.Config` itself** (`process`, `execution`, `distenkaApi`; any future base member). Only members declared on the process's own config type and its intermediate base classes count.
- **Names.** The serialized name from the contract (camelCase via `CamelCaseNamingStrategy`, or the explicit `[JsonProperty("name")]`). The platform matches names ordinally, so **callers must send exactly these names**; the host reader stays case-insensitive.
- **`process`.** Emitted exactly as ADR-002 §2.3 rule 1 defines it (`type: string`, `enum` of canonical names, `required` only when the package has more than one process, `description` listing them) — always present, first. A config type that declares its own member serialized as `process` fails `list`.
- **Union across processes.** Exactly ADR-002 §2.3 rules 2–6: identical descriptors merge (compared ignoring `description` and `required`); `required` only when every process requires it; incompatible declarations of one name fail `list`. Per-process `required` and per-process property sets are not expressible — **OQ-9**.
- **`execution` is not part of the published contract.** Callers cannot set `ExecutionConfig` values per run through the platform (the validator would reject `execution` as unknown). Execution tuning (`ParallelTaskCount`, retry counts, thresholds) is set by the package author in the config type's constructor. Whether any of it should become caller-settable is a product decision, not taken here.
- **Duplicate serialized names** (e.g. two members differing only in case, or a `[JsonProperty]` collision) fail `list`.

### 2.3 Mapping table — CLR member type → descriptor

`T?` (`Nullable<T>`) maps as `T` plus `"nullable": true`. Reference types are `"nullable": false` unless the member is annotated nullable under nullable reference types (`NullabilityInfoContext` reports `Nullable`); oblivious code is treated as non-nullable (omitting a property is how a caller leaves it unset).

| CLR type | `type` | Implicit constraints emitted | Notes |
|---|---|---|---|
| `string` | `string` | — | |
| `char` | `string` | `minLength: 1`, `maxLength: 1` | |
| `Guid` | `string` | `minLength: 32`, `maxLength: 38` | Newtonsoft accepts N/D/B/P forms (32–38 chars). |
| `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`, `Uri` | `string` | — | Format is not expressible; malformed values fail at `run` (exit 1). |
| `byte[]` | `string` | — | Newtonsoft reads base64 strings, not arrays. |
| `bool` | `boolean` | — | |
| `sbyte`, `byte`, `short`, `ushort`, `int`, `uint` | `integer` | `minimum` / `maximum` = the CLR type's range | Without these the platform would accept values the host cannot deserialize. |
| `long` | `integer` | — | The platform's `Int64` check already matches. |
| `ulong` | `integer` | `minimum: 0` | Values above `Int64.MaxValue` are refused by the platform — a documented narrowing. |
| `float`, `decimal` | `number` | `minimum` / `maximum` = the CLR type's range | |
| `double` | `number` | — | |
| `enum` (non-`[Flags]`) | `string` | `enum` = member names (or `[EnumMember(Value)]` where present), declaration order | Newtonsoft reads names (and writes numbers by default — irrelevant here). |
| `T[]`, `List<T>`, `IList<T>`, `ICollection<T>`, `IEnumerable<T>`, `IReadOnlyList<T>`, `IReadOnlyCollection<T>`, `HashSet<T>`, `ISet<T>` | `array` | `items` = descriptor of `T` | `T` must itself be mappable. |
| Non-abstract class or struct with a public parameterless (or Newtonsoft-usable) constructor, not a collection or dictionary | `object` | `properties` = its members by the same rules as §2.2 (no base-`Config` exclusion needed) | Recursive; cycles are rejected. |

### 2.4 Attribute mapping

| Attribute | Applies to | Descriptor |
|---|---|---|
| `[Required]` (DataAnnotations) or `[JsonProperty(Required = Required.Always / AllowNull / DisallowNull)]` | any | `required: true` (`AllowNull` also forces `nullable: true`) |
| `[Required]` with `AllowEmptyStrings = false` (the default) | `string` | additionally `minLength: 1` unless a larger `minLength` is set |
| `[Range(min, max)]` (numeric or `(Type, string, string)` form parsed invariant) | numeric | `minimum` / `maximum`, intersected with the implicit CLR range |
| `[Range]` with `MinimumIsExclusive` / `MaximumIsExclusive` | `integer` types | bound adjusted by ±1 |
| ″ | `number` types | **rejected** — exclusive bounds are not expressible |
| `[StringLength(max, MinimumLength = n)]` | `string` | `maxLength` / `minLength` |
| `[MinLength(n)]`, `[MaxLength(n)]`, `[Length(min, max)]` | `string` | `minLength` / `maxLength` |
| ″ | `array` | `minItems` / `maxItems` |
| `[AllowedValues(...)]` (string values) | `string` | `enum` |
| `[Description("…")]` (System.ComponentModel) | any | `description` (braces escaped per §2.1 rule 2) |
| `[JsonIgnore]` | any | member omitted |
| `[JsonProperty("name")]` | any | serialized name |
| `[DefaultValue]`, constructor-assigned defaults | any | **not emitted** — the platform accepts `default` but never compiles, enforces or displays it |
| `[RegularExpression]`, `[EmailAddress]`, `[Url]`, `[Phone]`, `[Compare]`, `[DeniedValues]`, `[CustomValidation]`, custom `ValidationAttribute`s | any | **not emitted** (no descriptor keyword enforces them). They are host-side only: enforced at `run` by DataAnnotations validation of the resolved configuration (DCS-044), failing with exit 1. |
| A constraint attribute on a type it does not fit (e.g. `[Range]` on `string`, `[StringLength]` on `int`) | — | **rejected** — emitting it would produce a descriptor the platform refuses; ignoring it would silently drop the author's intent. |

The SDK never emits `default` and never emits any keyword outside the platform's `Keywords` set.

### 2.5 Rejection rules (`list` exits 1, stdout empty, `ERROR:` lines on stderr)

A package is rejected — rather than described partially — when any root or nested member is:

1. a dictionary (`IDictionary<,>`, `IReadOnlyDictionary<,>`, `Dictionary<,>`, `Hashtable`, `ExpandoObject`), `object`, `dynamic`, `JToken`/`JObject`/`JArray`/`JValue`, `JsonElement`/`JsonNode`;
2. an interface or abstract type that is not one of the collection interfaces in §2.3, or a type with `[JsonConverter]` / a `TypeNameHandling` setting (polymorphic or custom wire shape — not derivable from the CLR shape);
3. a `[Flags]` enum, or an enum with no members;
4. a type that recursively contains itself (cycle);
5. carrying a constraint attribute that does not fit its type, or an exclusive bound on a `number`;
6. part of a conflict across processes (ADR-002 §2.3 rule 5), a duplicate serialized name, or a member serialized as `process`;
7. nested beyond the depth limit (§2.1 rule 6);

and when the package has no valid process (ADR-002 §2.3 rule 6).

Each `ERROR:` line names the member path in the emitted naming (e.g. `ERROR: settings.lookup: Dictionary<string,string> is not expressible in the Distenka input schema (use a nested class or an array of key/value objects)`).

### 2.6 Other `list` modes and the NJsonSchema output

- `list --verbose` prints a human-readable listing (process names, config types, properties with their descriptors) for developers. It is not part of the executor contract; the executor never passes flags.
- The Runly `list --json` listing is kept as `list --legacy-json` for one major version and is removed afterwards; `--json` itself is withdrawn so that no flag combination prints a non-descriptor JSON object first by accident.
- **The NJsonSchema JSON Schema output is dropped**: it is a second, incompatible dialect that the platform refuses. `Hosting/JsonSchema` is deleted in DCS-043; removing the `NJsonSchema` package and the `NJsonSchema.Annotations` attributes (`[NotNull]`, `[JsonSchemaIgnore]` on `Config`, `ProcessorConfig`, `ExecutionConfig`) is done in DCS-012 once DCS-043 has landed.

### 2.7 Worked example — `Distenka.Diagnostics` (`DiagnosticConfig`, single process)

`DiagnosticConfig` declares `[Description("The number of items to process.")] int NumberOfItems`, `Category[] Categories` (nested `Name: string`, `IsSuccessful: bool`, `Count: int`), `int MillisecondDelayPerItem`, eleven `bool` switches and `string MessageToLogInProcessAsync`. `list` prints (wrapped here for reading; the real output is one line):

```json
{"process":{"type":"string","required":false,"enum":["Distenka.Diagnostics.DiagnosticProcessor"],"description":"Process to run. One of: Distenka.Diagnostics.DiagnosticProcessor."},
 "numberOfItems":{"type":"integer","minimum":-2147483648,"maximum":2147483647,"description":"The number of items to process."},
 "categories":{"type":"array","items":{"type":"object","properties":{
     "name":{"type":"string"},
     "isSuccessful":{"type":"boolean"},
     "count":{"type":"integer","minimum":-2147483648,"maximum":2147483647}}}},
 "millisecondDelayPerItem":{"type":"integer","minimum":-2147483648,"maximum":2147483647},
 "canCountItems":{"type":"boolean"},
 "...": "one boolean descriptor per remaining switch",
 "messageToLogInProcessAsync":{"type":"string"}}
```

(The `"..."` line stands for the remaining boolean members and is not literal output.) No `execution`, no `distenkaApi`, no defaults, no keyword outside the platform set.

## 3. Consequences

**Positive**

- Every package whose config types use supported shapes publishes a schema the platform compiles: `/v1/invoke` and published-schema validation on `POST /api/runs` work, and ACC-40 consumer docs are generated from real types and descriptions.
- Values the platform accepts are values the host can deserialize (implicit CLR ranges; enum names; `Guid` length).
- Unsupported shapes fail at publish time with a reason the owner sees, instead of producing an uninvocable package or a permissive contract.

**Negative / limitations**

- **OQ-9** (per-process schema) is unchanged — see ADR-002 §3.
- `execution` is no longer caller-settable through the platform (it never was in a validated way).
- Format constraints (`[EmailAddress]`, regex, dates) are enforced only at `run` time by the host, so a malformed value is accepted by the API and fails the run (exit 1) instead of returning 400.
- Property names are case-sensitive at the platform: callers using PascalCase (accepted by the host locally) are rejected by the API. README/template must show camelCase (DCS-090, DCS-015).
- `ulong` above `Int64.MaxValue`, exclusive bounds on `number`, `[Flags]` enums and dictionaries are not expressible. Dictionaries are the likeliest to bite; the error text names the workaround.
- Breaking for tooling that parsed the Runly `list --json` output (kept one major version as `--legacy-json`) and for anyone using `--verbose --json` JSON Schema (removed).

## 4. Implementation and verification

| Rule | Story | Verified by |
|---|---|---|
| §2.1–2.5 generator, output channel, escaping, rejections | DCS-043 | Unit tests per mapping row and per rejection rule; golden files for `DiagnosticConfig`, a two-process assembly and a conflicting pair; output parsed by a port of `PluginInputSchema.TryParse` |
| Port of `TryParse` / `PluginInputRules` with a pointer to `Distenka.Core.API/Services/PluginInputSchema.cs` @ `4c3cf2c` | DCS-093 | DCS-100 executor-contract suite (spawns `dotnet exec <dll> list` with a cleared environment and applies `ExtractJsonObject` exactly) |
| Host-side DataAnnotations validation at `run` (formats, per-process `required`) | DCS-044 | DCS-044 / DCS-100 |
| `--legacy-json`, `--verbose`, removal of `Hosting/JsonSchema` | DCS-043 | Golden-output tests |
| Removal of `NJsonSchema` and its annotations | DCS-012 | Dependency guard |

## 5. Evidence

Platform (`D:\Product\Distenka` @ `4c3cf2c`, read 2026-10-31):
- `Distenka.Core.Executor/Program.cs:558-660` — `RunValidationAsync` (`exec "<dll>" list`, cleared environment, 60 s timeout, non-zero exit → stderr truncated to 2000 chars); `:676-704` `ExtractJsonObject` (raw brace counting).
- `Distenka.Core.API/Services/PluginInputSchema.cs` — `PluginInputType`, `PluginInputProperty`, `Keywords` (line 133-137), `TryParse`, `ReadProperty`, `ReadPermitted` (strings only, non-empty), `TryReadCount` (≥ 0 ints), applicability checks per type.
- `Distenka.Core.API/Services/PluginInputRules.cs` — `ValidateObject` (ordinal names, unknown/duplicate/owner-bound/required), `ValidateValue` (`Int64` / `double`, `nullable`), `ValidateString` (ordinal `enum`), `ValidateArray`.
- `Distenka.Core.API/Services/PluginInputOptions.cs:53-56`, `appsettings.json:27-28` — `MaxPayloadDepth` / `MaxSchemaDepth` = 16.
- `Distenka.Core.API/Services/IPluginBindingService.cs:291-338` — owner bindings must be declared root properties.

SDK (this repository):
- `src/Distenka/Hosting/ListAction.cs`, `Hosting/JsonSchema.cs`, `ConfigWriter.cs` (`Settings`), `ConfigReader.cs`, `Config.cs`, `ExecutionConfig.cs`, `DistenkaApiConfig.cs`, `ProcessorConfig.cs`.
- `src/Diagnostics/DiagnosticConfig.cs` — worked example.
- ADR-002 (process descriptor and union rules).
