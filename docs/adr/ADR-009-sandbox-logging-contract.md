# ADR-009 — Logging inside the sandbox: plain level-prefixed stdout lines, no console UI, and neutralisation of the executor's error heuristics in non-error log text

| Field | Value |
|---|---|
| Status | **Accepted** — completes AD-3 for logging; answers the question handed over by ADR-004 §2.3 rule 4 |
| Date | 2026-10-31 |
| Backlog story | DCS-009 (Phase 0 — Architecture & Contract Alignment) |
| Supersedes | `ProcessorHost.CreateDefaultBuilder`'s removal of all visible logging (`ClearProviders()` + Debug/EventSource/EventLog only) |
| Related | ADR-004 (exit codes, stdout/stderr hygiene, output budget), AD-3, DCS-041 (plain output / `IRunReporter`), DCS-042 (`ILogger` → stdout provider), DCS-046 (summary line), DCS-048 (debugger launch), DCS-051 (sandbox constraints doc), DCS-083 (`ACTIVITY_ID`), DCS-012 (dependencies) |
| Mirrors | `D:\Product\Distenka\Distenka.Core.Executor\Program.cs` @ `4c3cf2c` (file last changed `cdd1a54`) |

## 1. Context

### 1.1 What the executor does with each line (verified, `Program.cs` @ `4c3cf2c`)

- **Stdout** (`:245-260`): each line is classified by `ParseLogLevel`, appended to `output` as `[STDOUT] <line>`, streamed to the run log with that level, and sets `hasPluginErrors` when the level is `ERROR` **or** the line contains `Exception`.
- **Stderr** (`:262-276`): each line is appended as `[STDERR] <line>`, logged as `STDERR: <line>` at level `ERROR`, and **always** sets `hasPluginErrors`.
- **`ParseLogLevel`** (`:427-433`): `ERROR:` anywhere, case-insensitive → ERROR; else `WARNING:`/`WARN:` anywhere, case-insensitive → WARNING; else contains `Exception` (case-sensitive) or `error:` → ERROR; else INFO.
- **Run log** (`LogLineAsync`, `:444-469`): `{ ts, msg, lvl, elapsed_ms }` pushed to the run's log list, trimmed to the **newest 1000** lines (DST-029, `RunLogTail.MaxLines`) — this tail is what is archived and what `details.timeline` is built from. **`output`** keeps the **first 2000** lines (`AppendOutput`, `:435-442`). The executor timestamps every line itself (`ts`, `elapsed_ms`).
- **Line splitting**: `Process.OutputDataReceived` delivers one event per line terminated by `\n`, `\r` or `\r\n`.
- **Environment** (`:233-241`): cleared; no `TERM`, no `DOTNET_ENVIRONMENT`; working directory = the package DLL folder.

So the level a line is *recorded* with, and whether it *fails the run*, depend only on substrings in its text — not on any structure the SDK might intend.

### 1.2 What the SDK does today (this repository, HEAD `3eda536`)

- `ProcessorHost.CreateDefaultBuilder` calls `Host.CreateDefaultBuilder()`, then `logging.ClearProviders()` and re-adds `Debug`, `EventSource` and (Windows only) `EventLog`; `AddConfiguration(Logging)` is honoured. On Linux in the sandbox **no provider produces visible output**: every `ILogger` call by a processor author is lost. `Microsoft.Extensions.Logging.Console` is referenced in `Distenka.csproj` but unused.
- `Hosting/RunAction` drives Spectre.Console (`AnsiConsole.Status`, `Progress`, markup, `WriteException`) regardless of whether stdout is a terminal.
- Under the executor, `Host.CreateDefaultBuilder` resolves the environment as `Production` (no environment variables) and the content root as the working directory, i.e. the package folder — so the package's own `appsettings.json` is loaded and its `Logging` section applies; user secrets are not loaded.
- The generic host's lifetime logger (`Microsoft.Hosting.Lifetime`) logs `Application started. Press Ctrl+C to shut down.`, `Hosting environment: …` and `Content root path: …` at Information.

## 2. Decision

### 2.1 Two output modes

| Mode | Selected when | Behaviour |
|---|---|---|
| **Plain** | `Console.IsOutputRedirected` is true (always the case under the executor), or `run --plain` | Only the line format of §2.2 on stdout. No ANSI escape sequences, no cursor control, no spinners, no Spectre markup, no `Console.OutputEncoding` banner text. Nothing on stderr (ADR-004 §2.3 rule 1). |
| **Interactive** | stdout is a terminal and `--plain` is not given | Spectre UI for progress (DCS-041). `ILogger` output uses the same line format (level may be coloured); neutralisation (§2.4) is off — there is no executor reading it. |

The decision is made once at start-up; there is no environment-variable switch (the sandbox has no environment).

### 2.2 Line format (plain mode)

```
<LEVEL>: [<Category>] <message>[ | <ExceptionType>: <exception message>[ | <stack>]]
```

- **`<LEVEL>`** — exactly one of the prefixes the executor recognises:

  | `LogLevel` | Prefix | Recorded by the executor as | Fails the run? |
  |---|---|---|---|
  | `Trace`, `Debug` | `DEBUG:` | INFO | no (suppressed by default, §2.5) |
  | `Information` | `INFO:` | INFO | no |
  | `Warning` | `WARN:` | WARNING | no |
  | `Error`, `Critical` | `ERROR:` | ERROR | **yes** (`hasPluginErrors`) |

- **`[<Category>]`** — the logger category shortened to its last segment (type name without namespace, generic arity removed); omitted for the SDK's own reporter lines (DCS-041). The full category is not printed (budget).
- **One event = one physical line.** In the message, category, exception text and stack, `\r` and `\n` are written as the two characters `\r` / `\n`; every other C0 control character (including ESC) is written as `\uXXXX`. A line is capped at **8192 characters**; longer content is cut and suffixed with `… (+<n> chars)`.
- **Exceptions** passed to `ILogger`: ` | <full type name>: <message>` for the exception and each inner exception. The (demystified) **stack trace is appended only on `ERROR:` lines**, escaped onto the same physical line and subject to the 8192 cap — it costs one line of the 2000-line budget, which is consistent with ADR-004 §2.3 rule 3 (no multi-line traces on stdout). On `WARN:`/`INFO:`/`DEBUG:` lines the stack is omitted.
- **No timestamps, scopes or event ids** in the line: the executor stamps every line (`ts`, `elapsed_ms`), and each extra field costs budget. (`ACTIVITY_ID` correlation is DCS-083's decision.)
- **Writing:** one synchronous, complete-line write per event to `Console.Out` under a lock (no interleaving of partial lines); the provider is flushed before the exit code is set and before the summary line (DCS-046), so the summary is always the last stdout line.

### 2.3 Providers

- **Plain mode:** the SDK's `StdoutLineLoggerProvider` (DCS-042, no dependency on `Microsoft.Extensions.Logging.Console`) plus `EventSource` (diagnostics, invisible). `Debug` and `EventLog` are not registered. The unused `Microsoft.Extensions.Logging.Console` package reference is removed (DCS-012).
- **Interactive mode:** the same line provider (rendered through the reporter so it does not corrupt Spectre's live display — DCS-041) plus `Debug` and `EventSource`; `EventLog` stays Windows-only as today.
- **Never** a provider that writes to stderr.

### 2.4 Neutralisation of the executor's heuristics in non-error text — the answer to ADR-004 §2.3 rule 4

**Decision: in plain mode the SDK neutralises heuristic triggers in every line it writes at `DEBUG:`, `INFO:` and `WARN:` level, by default.** `ERROR:` lines are never altered.

The transformation is minimal, visible and deterministic, applied to the whole formatted line after escaping:

| Trigger (executor rule) | Rewritten as | Example |
|---|---|---|
| `Exception` (case-sensitive substring) | `exception` — only the capital `E` is lowered | `TimeoutException` → `Timeoutexception` |
| `error:` in any case (covers the `ERROR:` regex and the `error:` substring) | the same word followed by a space before the colon | `Connection error: reset` → `Connection error : reset` |

Nothing else is changed. `WARN:` lines stay WARNING because the executor's ERROR checks no longer match and its `WARN:` check does.

**Why.** Under ADR-004 the processor's outcome is carried by the exit code and the summary `disposition`; an author's choice of `LogWarning`/`LogInformation` is an explicit statement that the event is not a failure. Without neutralisation the executor's substring heuristics overrule both: `LogWarning(ex, "Retrying item {Id}")` with a `TimeoutException`, or `LogInformation("No errors: 0")`, turns a run that ended `Successful` with exit 0 into a **Failed** run — the exact mismatch ADR-004 exists to remove. Rewriting text was preferred to the alternatives:
- *Pass-through (no change)* — keeps author text byte-exact but leaves successful runs failing on ordinary words; rejected.
- *Invisible separators (zero-width characters) or homoglyphs* — keep the text looking identical but silently break copy/paste and search, and disguise what happened; rejected.
- *Mapping every `ILogger` line to `INFO:` and dropping its level* — loses the WARNING level in the run log and still fails on `Exception`; rejected.

**Trade-offs, stated.**
1. Author text in non-error lines is not byte-exact in the platform's output and run log (one lowered letter, one inserted space). Searching case-insensitively for a type name still finds it.
2. Authors can no longer fail a run by writing trigger words at Information/Warning level. Failing a run is done by the supported means: a failed `Result`, an exception escaping a processor method, reaching the item-failure threshold, or logging at **Error** level.
3. **`LogError` / `LogCritical` fail the run on the platform** (they produce `ERROR:` lines), even when the execution ends `Successful` and exits 0 — in that case the platform state is Failed while the summary says `disposition: Successful`. This is deliberate: an error-level log is a statement that something went wrong, and the platform rule for `ERROR:` lines is not the SDK's to override. Authors record tolerated problems with `LogWarning`. The README and template state this rule (DCS-090, DCS-015).
4. **Only text that goes through the SDK is covered.** Direct `Console.Write*` by processor code bypasses the provider and is not neutralised; any write to `Console.Error` fails the run (ADR-004). The SDK does not intercept `Console.Out`/`Console.Error`: doing so would alter output the author wrote deliberately and is a larger behavioural change than this ADR justifies. Documented in DCS-051.
5. **Opt-out.** `Distenka:Logging:NeutraliseHeuristicTriggers` (default `true`), read from the package's `appsettings.json`, lets an author keep text byte-exact and accept the executor's verdicts. It does not affect `ERROR:` lines or SDK reporter lines, which are always kept clean (ADR-004 §2.3 rule 2).

The SDK's own reporter lines (DCS-041) and the summary (DCS-046) do not rely on neutralisation: they are composed so that they never contain a trigger on the success path (ADR-004 §2.3 rule 2); neutralisation is the safety net for text the SDK echoes from user data.

### 2.5 Levels and filters (plain mode)

- Default minimum level **Information**.
- Default filters: `Microsoft` and `System` categories at **Warning**; `Microsoft.Hosting.Lifetime` at **None** (its start-up lines are noise in a sandboxed run; the host's status messages are also suppressed via `ConsoleLifetimeOptions.SuppressStatusMessages`).
- The package's `appsettings.json` `Logging` section overrides these defaults with the standard `Microsoft.Extensions.Logging` rules (it is the only configuration source in the sandbox).

### 2.6 Budget

Every line written in plain mode — author logs included — counts toward the platform's 2000-line `output` cap (first 2000 kept) and the 1000-line run-log tail (newest kept). The SDK does not throttle or drop author log events; DCS-041 bounds the SDK's own lines (ADR-004 §2.4). DCS-051 documents the arithmetic: on a run with more than 3000 lines the middle of the run appears in neither `output` nor the run log.

## 3. Consequences

**Positive**
- Processor `ILogger` output becomes visible on the platform with correct levels in the run log and timeline.
- A run's platform state follows its disposition and its error-level logs, not incidental words in informational text.
- No console UI bytes are written to a pipe; the line budget is spent on content.

**Negative / limitations**
- Non-error log text is altered slightly in the platform's view (opt-out available).
- `LogError` on a run that otherwise succeeds makes the platform report Failed (deliberate; documented).
- Direct `Console` writes by processor code are outside the SDK's control.
- Interactive and plain output differ; tests must run in plain mode to reflect the platform (DCS-093/DCS-100).

## 4. Implementation

| Rule | Story |
|---|---|
| §2.1 mode switch, `--plain`, no ANSI on a pipe; reporter lines | DCS-041 |
| §2.2 line format, escaping, 8192 cap, exception rendering; §2.3 provider; §2.4 neutralisation + opt-out; §2.5 defaults | DCS-042 |
| Removal of `Microsoft.Extensions.Logging.Console` | DCS-012 |
| Provider flushed before exit code and summary | DCS-040, DCS-046 |
| Sandbox documentation (budget arithmetic, direct `Console` writes) | DCS-051 |
| Agreement tests: each `LogLevel`, each trigger, opt-out, multi-line message, ESC in message, 10 KB message — through the DCS-093 harness | DCS-042, DCS-100 |

## 5. Evidence

- `D:\Product\Distenka\Distenka.Core.Executor\Program.cs` @ `4c3cf2c` (last changed `cdd1a54`): `:233-241` cleared environment; `:245-260` stdout handler; `:262-276` stderr handler; `:427-433` `ParseLogLevel`; `:435-442` `AppendOutput` (first 2000); `:444-469` `LogLineAsync` (newest 1000, DST-029).
- `src/Distenka/ProcessorHost.cs` — `CreateDefaultBuilder` (`ClearProviders`, `AddConfiguration(Logging)`, `AddDebug`, `AddEventSourceLogger`, `AddEventLog` on Windows, `ActivityTrackingOptions`).
- `src/Distenka/Distenka.csproj` — `Microsoft.Extensions.Logging.Console` 8.0.0 referenced, unused.
- `src/Distenka/Hosting/RunAction.cs` — Spectre `Status`/`Progress`/markup/`WriteException`.
- ADR-004 §2.3–2.4.
