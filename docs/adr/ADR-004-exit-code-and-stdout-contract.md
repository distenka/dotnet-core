# ADR-004 — `run` exit-code and stdout/stderr contract: mirror the platform executor exactly

| Field | Value |
|---|---|
| Status | **Accepted** — records backlog decision AD-3 (run half; Decision 2: the executor implementation is authoritative) |
| Date | 2026-10-31 |
| Backlog story | DCS-004 (Phase 0 — Architecture & Contract Alignment) |
| Supersedes | The earlier backlog draft that proposed exit codes 2 (Cancelled) and 3 (configuration/startup error) — withdrawn: the executor gives them no meaning |
| Related | ADR-002 / ADR-003 (`list`), AD-3, OQ-2 (resolved by AD-3), **OQ-8** (termination/SIGTERM — open), DCS-009 (logging line format), DCS-040 (exit codes), DCS-041 (plain output), DCS-042 (ILogger → stdout), DCS-044 (configuration errors), DCS-046 (summary line), DCS-047 (graceful shutdown), DCS-093 / DCS-100 (agreement tests) |
| Mirrors | `D:\Product\Distenka` `master` @ **`4c3cf2c`**; `Distenka.Core.Executor/Program.cs` last changed in **`cdd1a54`** (2026-09-30). Re-verify this ADR whenever that file changes. |

## 1. Context

### 1.1 The executor's rules (transcribed from `Distenka.Core.Executor/Program.cs` @ `4c3cf2c`)

**Launch** (`:220-243`). `dotnet "<dll>" run "/tmp/distenka_<runId>_<guid>.json"`, working directory = the DLL folder, stdout and stderr redirected, environment cleared and replaced by `DOTNET_NOLOGO`, `DOTNET_CLI_HOME=/tmp`, `HOME=/tmp`, `PATH`, `DOTNET_GCHeapCount=2`, `DOTNET_GCServer=0`, `ACTIVITY_ID`, `COMPlus_GCHeapHardLimit=0x10000000` (256 MB).

**Stdout, per line** (`:245-260`):
```csharp
var (level, _) = ParseLogLevel(e.Data);
AppendOutput(outputLines, ref outputLineCount, $"[STDOUT] {e.Data}");
if (level == "ERROR" || e.Data.Contains("Exception"))
    hasPluginErrors = true;
_ = LogLineAsync(db, logKey, e.Data, level, elapsed);
```

**Stderr, per line** (`:262-276`) — every line, unconditionally:
```csharp
AppendOutput(outputLines, ref outputLineCount, $"[STDERR] {e.Data}");
hasPluginErrors = true;
_ = LogLineAsync(db, logKey, $"STDERR: {e.Data}", "ERROR", elapsed);
```

**Level classification** (`:427-433`):
```csharp
if (Regex.IsMatch(line, @"ERROR:", RegexOptions.IgnoreCase)) return ("ERROR", line);
if (Regex.IsMatch(line, @"(WARNING|WARN):", RegexOptions.IgnoreCase)) return ("WARNING", line);
if (line.Contains("Exception") || line.Contains("error:")) return ("ERROR", line);
return ("INFO", line);
```
Matches are **substrings anywhere in the line**, not prefixes. `ERROR:` / `error:` are case-insensitive in effect (the regex is), `Exception` is case-sensitive.

**Verdict** (`:326-328`, `:357-359`):
```csharp
infraSuccess  = exitCode != -1;
pluginSuccess = infraSuccess ? (exitCode == 0 && !hasPluginErrors) : (bool?)null;
...
var finalState = infraSuccess ? (pluginSuccess == true ? "Completed" : "Failed") : "Failed";
```

**Output** (`:196-199`, `:335`, `:435-442`). `output` = the first **2000** captured lines (stdout and stderr interleaved, each prefixed `[STDOUT] ` / `[STDERR] `); lines after the 2000th are **dropped** from `output` (they are still streamed to the run log by `LogLineAsync`, which keeps only the **newest 1000** lines — `:444-469`, DST-029; see ADR-009 §2.6).

**Executor exit** (`:378-380`): `Environment.Exit(infraSuccess ? 0 : 1)` — infrastructure only; it carries no plugin disposition.

**Retries** (DST-016, `:283-306`): a plugin-start mark is written *before* the plugin process is spawned; the platform retries only infrastructure failures that happened before that mark. **No plugin exit code causes a retry.**

### 1.2 Consequences of those rules that the SDK must respect

| Plugin behaviour | `hasPluginErrors` | Platform state |
|---|---|---|
| exit 0, no stderr, and no stdout line containing `ERROR:`/`error:` (any case) or `Exception` (`WARN:` lines allowed) | false | **Completed** (`pluginSuccess = true`) |
| exit 0 but **any** stderr line | true | **Failed** |
| exit 0 but any stdout line containing `Exception`, `error:` or `ERROR:` (anywhere, e.g. `WARN: retry after TimeoutException`) | true | **Failed** |
| any non-zero exit (1, 134 SIGABRT crash, 137 SIGKILL/OOM, …) | — | **Failed** |
| process could not be launched | — | Failed (infrastructure) |

- **There is no third value.** The executor distinguishes exit `0` from everything else. `-1` is not an exit status a Linux process can produce (`Environment.Exit(-1)` surfaces as 255; .NET reports a signal death as 128 + N), so from the plugin's side `infraSuccess` is effectively always true once it has started: the `-1` branch is not reachable by anything the SDK can do and must not be relied on.
- `WARN:` / `WARNING:` lines are recorded at level WARNING and do **not** fail the run — unless the same line also contains `Exception` or `error:`.
- A "Successful" run whose output mentions an exception type name is a **Failed** run on the platform.

### 1.3 What the SDK does today (this repository, HEAD `3eda536`)

- `Hosting/RunAction.RunAsync` never sets an exit code; a run ending in `Disposition.Failed` or `Cancelled` exits **0**, and the outer `catch { }` (`:244`) swallows every exception → exit 0.
- `ProcessorHost.RunProcessorAsync` is `host.RunAsync()`; processors' `Main` is `async Task Main` (template, sample), so the process exit code is `Environment.ExitCode` — which nothing sets.
- Item failures that stay under `ItemFailureCountToStopProcess` leave the disposition `Successful`, but `RunAction` prints them with `AnsiConsole.WriteException(...)` (`:192-193`), i.e. exception type names on stdout → the platform marks that "successful" run **Failed**. Conversely, a `Result.Failure(...)` with no exception text reaches the platform as **Completed**.

## 2. Decision

### 2.1 Two exit codes, nothing else

| Exit code | Meaning |
|---|---|
| **0** | The execution ended with `Disposition.Successful` **and** the SDK kept the success path clean (§2.3). |
| **1** | Every other outcome the SDK controls: `Failed`, `Cancelled`, configuration/startup error, unhandled exception. |

The SDK never exits with any other code by its own choice. Codes the runtime produces when the process dies (134, 137, …) are outside the SDK's control and are Failed on the platform. **Retryable vs terminal is not modelled**: the executor defines no such distinction for plugin exits and the platform never retries after the plugin started (DST-016). New meanings may only be introduced if the executor adds them (re-verify against the commit in the header).

Mechanism (DCS-040): set `Environment.ExitCode` before stopping the host, so `async Task Main` → `RunProcessorAsync()` processors get the code without changing their `Main`.

### 2.2 Outcome table

| # | Outcome | Disposition | Exit | Stdout (in order) | Summary `disposition` |
|---|---|---|---|---|---|
| 1 | All items succeeded | Successful | 0 | `INFO:` progress lines (bounded, §2.4) · summary | `Successful` |
| 2 | Some items failed, below `ItemFailureCountToStopProcess` | Successful | 0 | `WARN:` lines with **counts and item ids only** — no exception type, message or stack (§2.3) · summary | `Successful` |
| 3 | Item-failure threshold reached | Failed | 1 | `ERROR: <reason>` · summary | `Failed` |
| 4 | Exception in `InitializeAsync` / `GetItemsAsync` / enumeration / `CountAsync` / `FinalizeAsync` (handled by the pipeline) | Failed | 1 | `ERROR: <method>: <exception type>: <message>` (one line) · summary | `Failed` |
| 5 | Exception escaping the pipeline (`HandleExceptions = false`, SDK defect) | Failed | 1 | `ERROR: unhandled <exception type>: <message>` (one line) · summary if reachable | `Failed` |
| 6 | Cancelled (host stop, SIGTERM, `CancelAfter`) | Cancelled | 1 | `ERROR: run cancelled (<cause>)` · summary | `Cancelled` |
| 7 | Configuration/startup error: unparseable JSON, unknown/ambiguous/missing `process` (ADR-002 §2.4), required member missing, validation attribute violated (ADR-003 §2.4), processor construction or DI failure | — | 1 | `ERROR: <reason>` · summary | `Failed` |
| 8 | Process killed or runtime crash (OOM at the 256 MB cap, SIGKILL, fatal runtime error) | — | runtime's | whatever was written before death | none |

- Rows 3–7: the reason travels **in band** as one `ERROR:` line (newlines collapsed; format owned by ADR-009 / DCS-009). Consumers that need Failed vs Cancelled read `disposition` from the summary line (DCS-046, field set owned there). The platform state is `Failed` for all of them.
- Row 6 interacts with **OQ-8**: when the platform cancels, it deletes the Job; whether the plugin sees SIGTERM and whether its output is collected is unconfirmed (DCS-047 Blocked). The exit code still follows this table; the platform's own run state is set by the grain regardless.

### 2.3 Stdout / stderr hygiene

1. **The SDK never writes to stderr during `run`.** Every SDK-originated line goes to stdout. (Stderr is reserved for `list` failures — ADR-003 §2.1 — and is otherwise left to the .NET runtime's own fatal-crash output, which only happens on runs that are failing anyway.)
2. **Success path (rows 1–2):** no SDK-originated stdout line may contain the substrings `ERROR:` or `error:` (any case) or `Exception` (case-sensitive). This applies to banners, progress, item-failure warnings, the summary line (including any user data the SDK copies into it, such as category names or `FinalizeAsync` output) and every SDK-generated log message. Item failures below the threshold are reported by count and id only. When user-supplied text the SDK echoes on the success path would contain one of the substrings, the SDK omits that text and says so (`WARN: <n> item messages omitted from output`).
3. **Failure paths (rows 3–7):** exactly one `ERROR:` line states the reason; exception type names and messages are allowed there (the run is already Failed). Stack traces are not written to stdout (they would consume the 2000-line budget); they go to the results file when `ResultsToFile` is set.
4. **Processor-author output is not rewritten by this ADR.** A processor that logs or prints text containing `Exception` or `error:` makes its run Failed on the platform. Whether the SDK's `ILogger` provider neutralises such text, and how, is decided by ADR-009 (DCS-009) and implemented in DCS-042. The README states the rule for authors (DCS-090/DCS-051).
5. **No interactive UI on a pipe:** ANSI control sequences and spinner frames are never written when stdout is redirected (DCS-041).

### 2.4 Output budget

The platform keeps the **first** 2000 lines in `output`. The SDK therefore:
- keeps its own lines bounded and small — a start line, throttled progress (DCS-041), at most a fixed number of item-failure lines followed by a count, the `ERROR:` line, the summary — so that SDK output never approaches the cap on its own;
- writes the summary as the **last** stdout line (DCS-046). If processor-author output pushes the total past 2000 lines, the summary is missing from `output` but still present in the run log (`LogLineAsync` streams every line and the run log keeps the newest 1000, so the last line survives). This limitation is documented; the SDK does not try to work around it.

### 2.5 Agreement tests

The contract is verified by tests that apply the executor's rules, not by assumption:
- **DCS-093** `ExecutorContractHarness` re-implements `:245-276`, `:326-328`, `:357-359` and `:427-433` verbatim (with this commit reference) and returns `PluginSuccess` / `FinalState` / `Output` (first 2000 lines) / `Violations[]` for a spawned `dotnet <dll> run <config>` with a cleared environment.
- **DCS-100** asserts, for every row of §2.2 (using `Distenka.Diagnostics` fault switches and the Echo sample), the exit code, the expected platform state (`Completed` only for rows 1–2), the presence and uniqueness of the `ERROR:` line, the absence of stderr, and §2.3 rule 2 on the success path — including a row-2 run whose item exceptions are `TimeoutException`s (must still be `Completed`).

## 3. Consequences

**Positive**
- Platform state matches the processor's disposition: failures are no longer reported Completed, and handled item failures no longer turn a successful run into Failed.
- No invented semantics: every rule traces to a line in the executor at a named commit.

**Negative / limitations**
- Failed and Cancelled are indistinguishable by exit code; consumers must read the summary line.
- Detail about tolerated item failures is not in the platform `output` on successful runs (counts and ids only); full detail requires `ResultsToFile` or the processor's own reporting.
- Processor-author text can still fail a run through the executor's substring heuristics until ADR-009 decides on neutralisation.
- Summary line lost from `output` when author output exceeds 2000 lines.
- Breaking for scripts that relied on exit 0 from failed local runs (migration guide, DCS-095).

## 4. Implementation

| Rule | Story |
|---|---|
| §2.1 exit codes, `Environment.ExitCode`, removal of the empty `catch { }` | DCS-040 |
| §2.2 rows 2–6 stdout shape, §2.3 rules 1–3 and 5, §2.4 budget | DCS-041 (plain output), DCS-046 (summary) |
| §2.2 row 7 | DCS-044 |
| §2.3 rule 4 | ADR-009 / DCS-009, DCS-042 |
| §2.2 row 6 under platform cancellation | DCS-047 (Blocked on OQ-8) |
| §2.5 | DCS-093, DCS-100 |

## 5. Evidence

- `D:\Product\Distenka\Distenka.Core.Executor\Program.cs` @ `4c3cf2c` (file last changed `cdd1a54`, 2026-09-30): `:196-199` output queue / `MaxOutputLines = 2000`; `:220-243` launch and environment; `:245-260` stdout handler; `:262-276` stderr handler; `:283-306` DST-016 plugin-start mark; `:326-328` verdict; `:335` output join; `:357-359` final state; `:378-380` executor exit; `:427-433` `ParseLogLevel`; `:435-442` `AppendOutput` (first 2000 kept).
- `D:\Product\Distenka\Distenka.Core.Host\KubernetesJobExecutor.cs:932-945` — the grain reads `pluginSuccess` from the result hash.
- SDK: `src/Distenka/Hosting/RunAction.cs` (`:192-193` `WriteException` on tolerated item failures; `:227-232` disposition banner; `:244` empty `catch`), `src/Distenka/ProcessorHost.cs:122-130` (`RunProcessorAsync` = `host.RunAsync()`), `src/Templates/templates/App/Program.cs`, `src/Templates/samples/SubscriptionServiceProcessor/Program.cs` (`async Task Main`).
- Backlog: AD-3, Decision 2 (2026-10-31), OQ-2 (resolved).
