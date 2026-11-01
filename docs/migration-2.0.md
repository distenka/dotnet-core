# Migrating from Distenka 1.x to 2.0

> Work in progress. Each breaking story adds its entry here as it lands; DCS-095 completes and reviews the guide before the 2.0 release.
> The processor package id stays `Distenka` (AD-12) — there is no package rename.

## Removed hosted-platform configuration fields (DCS-045)

The configuration no longer carries anything about a hosted Distenka service. The SDK never contacts Distenka HQ (AD-4); the run's platform context belongs to the platform, not to the configuration file.

| Removed | Was |
|---|---|
| `Config.DistenkaApi` (type `DistenkaApiConfig`: `Uri` = `https://api.distenka.ai/`, `OrganizationId`, `InstanceId`, `LogSuccessfulItemResults`) | API connection for the deleted v1 reporting channel |
| `ExecutionConfig.OrgName`, `Env` | Organization / environment subdomain of the hosted service |
| `ExecutionConfig.APIKey` | Credential inside the run configuration |
| `ExecutionConfig.Processor`, `RunId` | Run identity supplied by the hosted service |
| `ExecutionConfig.Delay`, `RunAfterId` | Queueing hints for the hosted scheduler |

`ExecutionConfig` keeps `ParallelTaskCount`, `ItemFailureRetryCount`, `ItemFailureCountToStopProcess`, `HandleExceptions`, `ResultsToFile`, `ResultsFilePath`, `ResultsToConsole` and `LaunchDebugger`.

**Existing configuration files keep loading.** Unknown JSON members are ignored, so a 1.x file that still contains `distenkaApi` or `execution.orgName`/`apiKey`/… runs unchanged; the values are dropped. Remove them from your files — in particular never keep an API key in a run configuration.

**Code that set or read these members no longer compiles.** Delete the assignments. A processor that itself needs to call the platform receives its DK- key as a runtime-injected secret, never through `ExecutionConfig` (ADR-006 §3–4, DCS-027).

```csharp
// 1.x
config.Execution.APIKey = "...";
config.DistenkaApi.Uri = "https://api.distenka.ai/";

// 2.0 — remove both lines; nothing replaces them in the processor configuration.
```

## `run`: process resolution, validation and working directory (DCS-044)

- **`process` may be omitted** when the package contains exactly one process — this is the document the platform writes. With several processes it is required; leaving it out now fails with `ERROR: configuration does not name a process and the package contains N processes (…)` and exit code 1 (1.x crashed with a `NullReferenceException`).
- **Resolution order:** exact full name, then exact short name, then either case-insensitively. A name that matches several processes at one step is an error that lists them (1.x reported "Type … not found"); an unknown name lists the available full names. Abstract and open-generic processor types are never resolved.
- **Validation at `run`:** members marked `[Required]` (or `[JsonProperty(Required = …)]`) must be present in the document, and DataAnnotations attributes — including `[RegularExpression]`, `[EmailAddress]`, `[Url]`, `[Compare]` and custom `ValidationAttribute`s — are enforced on the configuration and its nested objects. A violation fails the run with one `ERROR:` line naming the member, exit code 1.
- **The working directory is no longer changed** to the configuration file's folder. Code that opened files relative to the configuration file must build the path itself; relative `execution.resultsFilePath` values are still resolved against the configuration's folder.
- Configuration errors are written as one `ERROR:` line on stdout with exit code 1 — never as a stack trace on stderr.
