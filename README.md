# Overview
#### distenka; // let's scale the vibe coding
Build in a couple of hours & reduce cost/time for your current/next feature

![Logo](https://avatars.githubusercontent.com/u/191408113?s=200&v=4)

**Distenka Platform** Is scalable and secured serverless computing platform for **AI** and **vibe coding**.


## Features at a Glance

* Vibe coding assistant in your first processor
* Single or multi-cluster setup.
* Automated failover to backup servers.
* Auto-discovery of nearby servers.
* Real-time process status with live log viewer.
* Processor can be written in any language **Currently .Net**.
* Schedule events in multiple time zones.
* Track CPU and memory usage for each process.
* Historical stats with performance graphs.
* Simple JSON messaging system for Processors.
* Web hooks for external notification systems.
* Simple REST API for scheduling and running events.
* API Keys for authenticating remote apps.

## Documentation

Documentation coming soon on:

- &rarr; **[Installation & Setup](https://distenka.ai/docs)**
- &rarr; **[Configuration](https://distenka.ai/docs)**
- &rarr; **[Web UI](https://distenka.ai/docs)**
- &rarr; **[Processors](https://distenka.ai/docs)**
- &rarr; **[Command Line](https://distenka.ai/docs)**
- &rarr; **[Inner Workings](https://distenka.ai/docs)**
- &rarr; **[API Reference](https://distenka.ai/docs)**
- &rarr; **[Development](https://distenka.ai/docs)**



## 🚀: Get Started

Check `/src/Templates/samples/` **SubscriptionServiceProcessor** example processor
- ```dotnet add package Distenka --version 1.0.0```

- It includes processes for managing subscriptions and bank accounts, such as creating, modifying, activating, and deactivating subscriptions, as well as adding and validating bank accounts.

### Prerequisites
- .NET 6.0 or later
- SQL Server (or your database of choice)
- Configure the database connection in appsettings.json or via environment variables

### Setting Up the Database
- Update the connection string in appsettings.json or set it via environment variables.
- Example (appsettings.json):
json

```
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=dbName;Trusted_Connection=True;"
  }
}
```

### Running Processes
The project uses the Distenka framework to manage and execute processes. Below are the key CLI commands for interacting with the processes.

To see the input schema the Distenka platform reads from your package, run:

```
dotnet run -- list
```

`list` prints exactly one JSON object: one descriptor per configuration property, plus a `process` property naming the processes in the package. Use `dotnet run -- list --verbose` for a readable listing of processes and their schema (`list --legacy-json` prints the 1.x listing for one more major version; `list --json` was withdrawn).

### Describing your configuration

The schema is generated from your `Config` subclass (public, settable properties; names in camelCase or as set by `[JsonProperty("name")]`; members of the base `Config` such as `execution` are never published):

| C# | Descriptor |
|---|---|
| `string`, `DateTime`, `DateTimeOffset`, `TimeSpan`, `Uri`, `byte[]` | `string` |
| `char` / `Guid` | `string` with length 1 / 32–38 |
| `bool` | `boolean` |
| `byte` … `int`, `uint` / `long` / `ulong` | `integer` with the type's range / no range / `minimum: 0` |
| `float`, `decimal` / `double` | `number` with the type's range / no range |
| `enum` (not `[Flags]`) | `string` with `enum` of the member names (`[EnumMember(Value)]` honoured) |
| `T[]`, `List<T>`, `IEnumerable<T>`, … | `array` with `items` |
| a class with properties | `object` with `properties` |
| `T?` / nullable reference (`string?` with nullable annotations on) | adds `nullable: true` |

Attributes: `[Required]` → `required` (and `minLength: 1` on strings unless `AllowEmptyStrings`), `[Range]` → `minimum`/`maximum`, `[StringLength]`, `[MinLength]`, `[MaxLength]`, `[Length]` → length or item counts, `[AllowedValues]` → `enum`, `[Description]` → `description`, `[JsonIgnore]` → omitted. Defaults are not published. `[RegularExpression]`, `[EmailAddress]` and other validators are checked by the host when the run starts, not by the platform.

`list` fails (exit 1, the reason on stderr) instead of publishing an unusable schema when a member is a dictionary, `object`, `JObject`/`JsonElement`, an interface or abstract type, a `[Flags]` enum, a self-referencing type, a member with `[JsonConverter]`, a constraint attribute that does not fit its type, nesting deeper than 16 JSON levels, or when two processes declare the same property differently. Callers must send property names exactly as published (case-sensitive) and the process by its full name. Details: `docs/adr/ADR-003-list-descriptor-schema.md`.

To get a runnable example configuration for a process, run

```
dotnet run -- get "SubscriptionServiceProcessor.CreateSubscriptionProcess" CreateSubscriptionProcess.json
```
Here is the output file

```
{
  "userId": 0,
  "planId": 0,
  "process": "SubscriptionServiceProcessor.CreateSubscriptionProcess"
}
```

to run process locally 

```
dotnet run -- run "CreateSubscriptionProcess.json" "createSubscriptionProcess-results.json"
```

### Output and logging on the platform

When stdout is redirected (always the case on the Distenka platform) or `run --plain` is given, `run` writes plain lines only — no colours or progress bars:

```
INFO: Starting SubscriptionServiceProcessor.CreateSubscriptionProcess
INFO: [CreateSubscriptionProcess] Subscription 42 created
WARN: [CreateSubscriptionProcess] Retrying item 7 | System.Timeoutexception: timed out
INFO: Disposition=Successful
```

- Everything you log through `ILogger<T>` appears as `<LEVEL>: [<Class>] <message>`, one line per event. Trace/Debug → `DEBUG:` (hidden by default), Information → `INFO:`, Warning → `WARN:`, Error/Critical → `ERROR:`. Levels follow the `Logging` section of your package's `appsettings.json`.
- **`LogError`/`LogCritical` fail the run on the platform**, even when the processor ends Successful. Record tolerated problems with `LogWarning`.
- The platform treats any line containing `Exception` or `error:` as an error. In `DEBUG:`/`INFO:`/`WARN:` lines the SDK therefore writes `Exception` as `exception` and `error:` as `error :`. Set `"Distenka": { "Logging": { "NeutraliseHeuristicTriggers": false } }` in `appsettings.json` to keep your text unchanged (and accept that such lines fail the run).
- Text written directly with `Console.Write*` is not touched; anything written to `Console.Error` fails the run.

The contract is recorded in `docs/adr/ADR-009-sandbox-logging-contract.md` and `docs/adr/ADR-004-exit-code-and-stdout-contract.md`.

### Reading execution logs (Distenka.Client)

A run's log is read from the run itself — there is **no logs endpoint** (AD-13, `docs/adr/ADR-007-result-retrieval-over-rest.md`):

```csharp
var details = await client.Runs.GetDetailsAsync(runId);       // GET /api/runs/{runId}/details
foreach (var entry in details.Timeline)                        // one entry per line the processor wrote
    Console.WriteLine($"{entry.Elapsed} {entry.Level} {entry.Message}");
var output = details.Output;                                   // the captured stdout/stderr, verbatim
```

- `Timeline` and `Output` are the whole execution-log contract. `Timeline` entries carry the level from the `INFO:` / `WARN:` / `ERROR:` prefix and the elapsed time; runs archived before structured logs have entries without a level.
- The executor keeps at most **2000 lines** of output, and the timeline is built from them: neither is a complete log archive. Put item-level results in a results file, not in the log.
- `TryGetDetailsAsync` returns `null` instead of throwing when the run is not visible (`404`: unknown, another account's, or purged) — handy in polling loops.

### Supplying the API key (Distenka.Client)

The account key (`DK-…`) is a secret. You create it in the Distenka console — the SDK cannot issue, list or rotate keys — and it has **no expiry unless the owner set one when issuing it** (`expiresAt`, at most 3650 days). An expired or revoked key surfaces as a `401` from the platform (`DistenkaApiException`); the SDK never refreshes it.

**In an application** (a service or tool that calls the platform), read it from configuration — any provider works:

```csharp
// appsettings.json holds only non-secret settings:  "Distenka": { "BaseAddress": "https://distenka.example.com/" }
services.AddDistenkaClient(configuration.GetSection("Distenka"));   // also reads Distenka:ApiKey
```

- Environment variable: `Distenka__ApiKey=DK-…` (containers, CI, systemd units).
- Development: `dotnet user-secrets set "Distenka:ApiKey" "DK-…"`.
- Production: your vault's configuration provider (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault, SOPS-decrypted files …) mapped to `Distenka:ApiKey`.

`ApiKey` becomes an `AccountKeyCredential`. A value that is not an account key fails at startup with a message that never quotes it. An invocation key (`DKI-…`) is set in code with `InvocationKeyCredential`.

**In a processor** that itself calls the platform (for example, to submit child runs), declare the key as a configuration property and let the platform supply it as an **owner binding**. The owner stores it once in the console and the platform merges it into each run's configuration server-side — it is never part of your package or of what a caller sends:

```csharp
public class MyConfig : Config
{
    public string DistenkaApiKey { get; set; }   // bound by the owner, never given a default
}

var credential = new AccountKeyCredential(Config.DistenkaApiKey);
```

**Never**

- write the key in source code, `appsettings*.json`, launch profiles or test fixtures;
- commit it to Git (the repository's `.gitleaks.toml` flags `DK-`/`DKI-`/`drc1.` values);
- pack it into a NuGet package — the SDK's own packages are scanned for key-shaped strings in its tests;
- log it — log the credential object instead: `AccountKeyCredential.ToString()` shows only the public prefix (`DK-0123ABCD…`), and the client redacts the `Authorization` header in `HttpClient` logs.

### Keeping credentials out of output

The client's guarantees, all covered by tests (`test/Distenka.Client.Tests/Credential_hygiene.cs`):

- `DistenkaApiException` never contains request headers or the request body. If the platform or a proxy in between echoes a credential back (an error page that lists the request headers, an error text that quotes the token), the SDK replaces it before it reaches `Message`, `Error`, `Body`, `RawContent` or `RequestId` — `Bearer …` becomes `Bearer *`, a key keeps only its public prefix (`DK-0123ABCD…`), a capability becomes `drc1.…`. Logging the exception (`logger.LogError(ex, …)`) is safe.
- Every credential's `ToString()` shows only its public prefix: `DK-0123ABCD…`, `DKI-0123ABCD…`, `drc1.…`.
- The `Authorization` header is redacted (`*`) in the `HttpClient` logs written under `System.Net.Http.HttpClient.Distenka.Client.*`, for the configured key and for a per-call capability. If you pass your own `HttpClient` to `new DistenkaClient(…)`, its logging is yours: redact `Authorization` (`RedactLoggedHeaders`) or don't log headers.
- The client writes nothing to the console or to the `Debug`/`Trace` listeners.

What stays your responsibility:

- The run capability returned by an invocation (`InvocationAccepted.ResultCapability`) is a secret. Serializing `InvocationAccepted` to JSON writes it, so it can be stored and polled later — keep that JSON in a secret store, never in a log.
- **Any** secret a processor needs — a database password or a third-party token as much as a `DK-` key — is an owner binding (ACC-37), never part of the configuration a caller submits. The platform stores the submitted configuration, shows it in the console (a value that looks like a secret replaces the whole displayed copy with `{}`) and reuses it for reruns.
