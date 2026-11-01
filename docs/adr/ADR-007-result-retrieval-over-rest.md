# ADR-007 — Run results are read over public REST by run identifier; callbacks are optional; no SignalR in the SDK

| Field | Value |
|---|---|
| Status | **Accepted** — ratifies backlog story DCS-007 (decision AD-8, product owner, 2026-10-31) |
| Date | 2026-11-01 |
| Backlog story | DCS-007 (Phase 0 — Architecture & Contract Alignment) |
| Related | ADR-001 (package boundary — forbidden dependencies), ADR-006 (credentials: DK-, DKI-, drc1.), ADR-008 (serialization), DCS-031 (details), DCS-035 (wait for completion), DCS-060 (invoke + `Callback-Url`), DCS-062 (capability result client), DCS-063 (callback verification), DCS-070 (observe by polling), DCS-071 (logs — Out of Scope, AD-13), DCS-072 (push adapter — Superseded) |
| Mirrors | `D:\Product\Distenka` `master` @ **`4c3cf2c`** — controller evidence as verified in the 2026-10-31 assessment (§5); docs `api-run-submission.md`, `api-invocation.md`, `api-run-cancellation.md` re-read 2026-11-01 from the owner's copies. Re-verify §1 when a run controller's routes or authorization policies change. |

## 1. Context

### 1.1 What the platform exposes for following a run

| Surface | Route | Who may call it | What it returns |
|---|---|---|---|
| Owner's run details | `GET /api/runs/{runId}/details` | The run's **owner** with an account credential (`DK-…`; `[Authorize]`, `RunsQueryController.cs:377`). Another account's run id answers `404`, identical to an unknown id (ACC-14). | `state` (one of the nine `RunState` names), `status`, lifecycle timestamps (`createdAt` … `completedAt`, `deadlineAt`, `cancelRequestedAt`), `attempt`, `invokedByKeyPrefix`, `totalExecutionMs`, `timeline` and `output` — "the run's own log and result" (`docs/api-run-submission.md` §Reading the run back; `RunsQueryController.cs:421/:470`). |
| Capability holder's result | `GET /api/runs/result` | Only a **run-result capability** (`Bearer drc1.…`): `[Authorize(Policy = RunCapabilityRequirement.PolicyName)]` (`RunResultsController.cs:78-80`). An account key is refused — the policy accepts no other scheme. | A smaller six-field document `{runId, status, progress, output, startedAt, completedAt}` with `ETag`; `If-None-Match` → `304`. The capability is minted once, with the invocation's `202`, and is good for 24 hours (`docs/api-invocation.md` §How long an accepted run may wait). |
| Callback | `Callback-Url` request header on `POST /v1/invoke/{pluginId}` | The **invoker** asks for it per invocation (`docs/api-invocation.md` §For the developer; `docs/api-callbacks.md`). `POST /api/runs` takes **no** callback header (`RunsController.cs` — no `Callback-Url` handling). | A signed `POST` to the caller's URL when the run finishes (`Distenka-Event: run.completed`, HMAC signature; verified by DCS-063). Delivery is retried on a schedule but is not a substitute for reading the result. |
| Console feeds | SignalR `RunsHub` at `/runsHub`, `ClusterHub` | The **console** only: `[Authorize]` with the console's cookie, an origin guard (`HubOriginGuard` against `WebOriginAllowlist`), Redis backplane — in `Distenka.Org.Web` (`docs/signalr-contracts.md`). | Live updates for the web console. Not a public API: no API-key authentication, no versioning promise. |
| Logs | — | — | **No public logs route exists.** `details.timeline` and `details.output` are the execution-log surface (AD-13). |

### 1.2 What the SDK had

The 1.x client opened a SignalR `HubConnection` to `/hub` (`git show 856048c^:src/Distenka/BroadCast/BroadCastChannel.cs`, `IDistenkaClient.cs`) — a channel that no longer exists in that form and that the platform reserves for its console. The code was deleted in `856048c`. Before Decision 3 (2026-10-31) the backlog waited for a public push channel; AD-8 removes that dependency.

## 2. Decision

### 2.1 The result is always readable by run id over REST

1. **Submit → run id.** `POST /api/runs` (`202`/`200` replay → `runId`) for owners with a DK- key; `POST /v1/invoke/{pluginId}` (`202`/`200` replay → `runId` plus `resultCapability`) for invokers with a DKI- key.
2. **Read the result by run id — always available, callback or not.**
   - **Owners (DK-):** `GET /api/runs/{runId}/details` — `Runs.GetDetailsAsync` (DCS-031), `Runs.WaitForCompletionAsync` (DCS-035).
   - **Invokers (drc1.):** `GET /api/runs/result` with the capability returned by the invocation — `RunResultClient.GetResultAsync` / `WaitForResultAsync` with `ETag`/`304` (DCS-062). The SDK never tries a DK- key on this route, nor a capability on `details`: the routes' policies make each credential valid on exactly one of them (ADR-006 §2.1).
3. **Callbacks are optional.** An invoker may send `Callback-Url` (DCS-060); the SDK verifies deliveries (DCS-063). A missing, late or failed callback never prevents reading the result over REST. The SDK offers no callback for `POST /api/runs`, because the platform has none.
4. **Observation is REST polling.** `Runs.ObserveAsync` (DCS-070) polls `details` (owners) or `result` with `ETag` (invokers), yields only on change and completes after the first terminal state. This **is** the design, not a placeholder for a push channel: no `IRunEventSource` abstraction is kept (DCS-072 Superseded).
5. **Logs are `details.timeline` and `details.output`** (AD-13). The SDK adds no logs route, log polling or streaming (DCS-071 Out of Scope).

### 2.2 Constraints

- **Public REST contracts only.** No SignalR client, no console hub, no persistent connection (WebSocket, SSE, long-poll) in any SDK package.
- **No platform internals.** No Orleans grain/stream, Redis stream or database access to learn run state (ADR-001 §2.1).
- **`Microsoft.AspNetCore.SignalR.Client` (and any `Microsoft.AspNetCore.SignalR.*` package) is forbidden** in `Distenka`, `Distenka.Client` and `Distenka.Processor.Testing`. Enforced by the `.nuspec` dependency test of DCS-010 (`Package_boundaries`: prefix `Microsoft.AspNetCore.SignalR` in the list prohibited for every package, added with this ADR) and by CI dependency scanning (DCS-014).

### 2.3 Polling cost

Polling is the client's to pace. Capability-result polls count against the invocation key's own per-minute window (`429 rate_limit_exceeded` + `Retry-After`, `docs/api-run-results.md` §Rate limit); owners' `details` polls share the account key's limits. The SDK therefore:

- sends `If-None-Match` on every capability poll after the first (a `304` is cheap for both sides) — DCS-062;
- backs off geometrically between polls (`PollingOptions`: initial 2 s, ×1.5, max 30 s) — DCS-035/DCS-070;
- honours `Retry-After` through the retry policy (DCS-025) and never polls past the capability's `ExpiresAt` (24 h) — DCS-062.

## 3. Consequences

**Positive**
- One mechanism (REST by run id) works for every deployment — self-hosted, on-premises, behind any proxy — with only the API's base address reachable; no sticky sessions, backplane or console origin needed (AD-4).
- The client's dependency set stays small and `netstandard2.0`-compatible (no SignalR client, ADR-005 §2.2).
- Results are never lost to a missed push or callback: reading by run id is idempotent and repeatable.

**Negative / costs**
- Latency of observation is the polling interval (seconds), not push latency. Acceptable for batch runs that take tens of seconds or more (`docs/api-invocation.md`: every run is a Kubernetes Job polled every 10 s by the platform itself).
- Polls consume rate-limit budget; mitigated by `ETag`/`304` and backoff (§2.3).

**Risks**
- If the platform later publishes an API-authenticated push channel, using it needs a new product decision and a new ADR (DCS-072 would be reopened). Nothing in the SDK's public surface presumes one.

## 4. Implementation

| Rule | Story |
|---|---|
| `Runs.GetDetailsAsync` / `TryGetDetailsAsync`; `timeline`/`output` as the log contract | DCS-031 |
| `Runs.WaitForCompletionAsync` with `PollingOptions` | DCS-035 |
| `Invocations.InvokeAsync` with optional `Callback-Url`; `resultCapability` → `RunCapabilityCredential` | DCS-060 |
| `RunResultClient` (`/api/runs/result`, `ETag`/`304`, `ExpiresAt`) | DCS-062 |
| Callback signature verification | DCS-063 |
| `Runs.ObserveAsync` over REST polling | DCS-070 |
| No logs API | DCS-071 (Out of Scope) |
| No push adapter | DCS-072 (Superseded) |
| `Microsoft.AspNetCore.SignalR*` forbidden in every SDK package (`.nuspec` test) | DCS-010 test, extended by DCS-007 |
| CI dependency scanning | DCS-014 |

## 5. Evidence

Platform (`D:\Product\Distenka` @ `4c3cf2c`, verified in the 2026-10-31 assessment; not re-read for this ADR — the platform folder was not connected):
- `Distenka.Core.API/Controllers/RunsQueryController.cs:377` `[HttpGet("{runId}/details")]` under `[Authorize]`; `:421`/`:470` `response.Output`.
- `Distenka.Core.API/Controllers/RunResultsController.cs:78-80` `[Route("api/runs/result")]`, `[Authorize(Policy = RunCapabilityRequirement.PolicyName)]`.
- `Distenka.Core.API/Controllers/RunsController.cs` — no `Callback-Url` handling (grep).
- `docs/signalr-contracts.md` — `RunsHub` `/runsHub`: `[Authorize]` console cookie, Redis backplane, origin guard.
- `docs/api-run-results.md` §Polling cheaply: ETag and 304, §Rate limit; `docs/api-callbacks.md` (invocation callbacks).

Platform docs re-read 2026-11-01 (owner-supplied copies, saved in the project as `claude/platform-docs/`):
- `docs/api-run-submission.md` §Reading the run back (owner `details`, `404` for another account's run; capability holders read the smaller document in `api-run-results.md`; "`timeline` and `output` are the run's own log and result").
- `docs/api-invocation.md` §The response (async only; poll `GET /api/runs/result` with the capability; `resultCapability` may be `null` without a signing key), §How long an accepted run may wait (capability good for 24 h), §For the developer (`Callback-Url`).
- `docs/api-run-cancellation.md` (the outcome of a cancel is read from the run's details).

SDK: `git show 856048c^:src/Distenka/BroadCast/BroadCastChannel.cs`, `…/IDistenkaClient.cs` (deleted SignalR client); `test/Distenka.Tests/Scenarios/Packaging/Package_boundaries.cs` (dependency guard).

Backlog: Decision 3 / AD-8 and AD-13 (product owner, 2026-10-31).
