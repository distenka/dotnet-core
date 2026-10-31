# ADR-006 — Client authentication model: DK- account key as the execution credential, DKI- and drc1 for the invocation path; no login, session, JWT, refresh, org/env or HQ

| Field | Value |
|---|---|
| Status | **Accepted** — records backlog decision AD-9 (with AD-4, AD-8) |
| Date | 2026-10-31 |
| Backlog story | DCS-006 (Phase 0 — Architecture & Contract Alignment) |
| Supersedes | The deleted v1 client model (`x-api-key` + `Bearer` to `https://{OrgName}.{Env}.distenka.ai/hub`, configured from `ExecutionConfig.OrgName/APIKey/Env`); the earlier backlog plan for a JWT delegate credential (withdrawn by Decision 4) |
| Related | ADR-001 (package boundary), AD-4 (no tenant/HQ, base URL required), AD-8 (results over REST), AD-9, DCS-020 (client options), DCS-021 (credential types), DCS-022 (error model), DCS-027 (secure key supply), DCS-045 (removal of `ExecutionConfig.APIKey` etc.), DCS-080 (credential hygiene), DCS-081 (TLS) |
| Mirrors | `D:\Product\Distenka` `master` @ **`4c3cf2c`** (read for this ADR) |

## 1. Context

### 1.1 How the platform authenticates API callers (verified in source)

**One header, three API credentials, chosen by shape.** `Distenka.Core.API/Startup.cs:295-321` registers a policy scheme whose `ForwardDefaultSelector` asks each handler whether the request carries its credential, in this order:

1. `RunCapabilityAuthenticationHandler.TryReadCredential` (`:60-76`) — `Authorization` starts with `Bearer ` (case-insensitive), token (trimmed) starts with **`drc1.`** (ordinal, case-sensitive).
2. `InvocationKeyAuthenticationHandler.TryReadCredential` (`:62-78`) — `Bearer ` + token starting with **`DKI-`** (ordinal).
3. `ProductKeyAuthenticationHandler.TryReadCredential` (`:143-171`) — first the legacy header **`X-Product-Key`** (any non-blank value), then `Bearer ` + token starting with **`DK-`** (ordinal).
4. Otherwise the Keycloak **JWT bearer** scheme (console sessions).

The markers are prefix-free (`CredentialShape.IsDistinctFrom`), so at most one matches. Routing is by the **exact-case** marker; a key pasted as `dk-…` would be routed to the JWT scheme and fail.

**Shapes** (`Distenka.Core.Kernel/Identity/CredentialShape.cs`, `ApiKeyFormat.cs`, `InvocationKeyFormat.cs`):
- Minted keys are `DK-` / `DKI-` + **48** upper-case hex characters (`SecretLength = 48`).
- `Normalise` *accepts* any presented value that, after trimming, starts with the marker (**case-insensitive**), has only hex digits after it, and is between `PrefixLength` (marker + 8 hex: 11 for DK-, 12 for DKI-) and **128** characters long; it upper-cases the hex. Legacy 32-hex keys therefore remain valid.
- The public **prefix** = marker + first 8 hex characters; it is what the platform audits and displays. The rest is the secret.

**Run-result capability** (`Distenka.Core.Kernel/Identity/RunCapabilityService.cs`, ACC-38): `drc1.` + base64url(payload) + `.` + base64url(HMAC-SHA256). The payload `{version}|{runId}|{keyId}|{expiryUnix}` is readable but versioned and platform-internal; the platform returns the expiry separately as `resultCapabilityExpiresAt` in the `POST /v1/invoke` response.

**Account keys (DK-)** (`Distenka.Core.API/Controllers/AccountKeysController.cs`, ACC-07):
- Listed, issued and revoked only from an interactive session: every action calls `RefuseKeyCredentialAsync` (`:179-193`), which answers **403 `session_required`** — *"A key cannot issue or revoke keys, including itself."*
- `expires_at` is **null by default (no expiry)**; an owner *may* set `expiresAt` at issue time (future, at most 3650 days — `:46`, `:93-100`). An expired or revoked key fails authentication (401).
- Keys may be issued with `scopes`; the platform stores and returns them but **does not enforce** them (`CallerIdentity.Scopes`: "Carried, not enforced").
- The plaintext exists in exactly one response (issue) and is never stored or logged by the platform.

**Which credential each SDK surface uses** (AD-8; `docs/api-invocation-keys.md` §The three credentials; `ActiveAccountRequirement` / `InvocationController` codes `invocation_key_required`, `run_capability_required`, `scope_violation`):

| Surface | Credential | Routes |
|---|---|---|
| Owner execution (`Runs`) | **DK-** account key | `POST /api/runs`, `GET /api/runs/{id}/details`, `GET /api/runs/list`, `GET /api/runs/summary`, `POST /api/runs/{id}/cancel`, `POST /api/runs/{id}/rerun` |
| Third-party invocation (`Invocations`) | **DKI-** invocation key (issued by the owner from the console, per processor) | `POST /v1/invoke/{pluginId}`, `GET /v1/invoke/{pluginId}/docs` |
| Result polling (`RunResult`) | **drc1.** run-result capability (returned by `POST /v1/invoke`) | `GET /api/runs/result` |
| Console only — not SDK | session (JWT/cookie) | key management, plugin management (`WebAppOnly`), invocation-key issuance |

**No tenant, no HQ.** There is no organisation or environment in any route; the account is resolved from the credential. The API base URL is deployment-specific (self-hosted / on-premises).

### 1.2 What the SDK has today

Nothing: the v1 client (`BroadCast/*`, `ApiKeyProvider`, `Constants.ApiEndpointTemplate = https://{0}.{1}.distenka.ai`) was deleted in `856048c`. `ExecutionConfig.OrgName/APIKey/Env` and `DistenkaApiConfig` (default `https://api.distenka.ai/`) survive in the processor host with no readers (removed by DCS-045).

## 2. Decision

### 2.1 Credentials the SDK provides

`Distenka.Client` exposes a public `IDistenkaCredential` (one method: produce the bearer token value for a request, async, cancellable) and ships exactly **three** implementations:

| Type | For | Local shape check at construction |
|---|---|---|
| `AccountKeyCredential` | the SDK's **execution credential** (AD-9): the `Runs` surface | trim; marker `DK-` matched case-insensitively and **re-written as `DK-`**; only hex after it, upper-cased; total length 11–128. Does **not** require exactly 48 hex (legacy keys are valid on the platform). Rejects a `DKI-` value with a message saying it is an invocation key. |
| `InvocationKeyCredential` | the `Invocations` surface | same rules with marker `DKI-`, length 12–128. Rejects a `DK-` value with a message saying it is an account key. |
| `RunCapabilityCredential` | the `RunResult` surface | trimmed value starts with `drc1.` (ordinal) and contains no whitespace; no length or structure check (the platform defines none for clients). **Opaque otherwise**: the SDK does not decode the payload. Its optional `ExpiresAt` comes from `resultCapabilityExpiresAt` in the invocation response (DCS-060), never from parsing the token. |

- **Transport:** every request carries exactly `Authorization: Bearer <canonical token>`. The SDK never sends `X-Product-Key`, `x-api-key`, cookies, or a credential in a query string.
- **Canonicalisation matters:** the platform routes by exact-case marker, so normalising the marker client-side turns a lower-cased paste into a working key instead of a misrouted JWT failure.
- **Errors at construction** are `ArgumentException`s that state the expected shape and never include the presented value or any part of it beyond the marker.
- **Redaction:** `ToString()` and debugger display show the public prefix only — `DK-` + 8 hex + `…`, `DKI-` + 8 hex + `…`, `drc1.…` — matching what the platform audits. Full rules in DCS-080.
- **Extensibility:** `IDistenkaCredential` stays public so a deployment can supply its own (for example a JWT for a custom gateway). The SDK ships none, documents none as supported and gives such credentials no special handling.

### 2.2 What the SDK deliberately does not do

- **No account login, session, OIDC/JWT acquisition or refresh** (Decision 4). A person with a console session uses the console. `DelegatingBearerTokenCredential` is not provided.
- **No key management:** no listing, issuing, rotating or revoking DK- or DKI- keys (the platform refuses all of these to key credentials — 403 `session_required` — and DCS-026 is Out of Scope).
- **No expiry modelling for keys.** The SDK cannot know a key's expiry (it is not part of the key) and does not schedule renewals. A 401 is surfaced as a `DistenkaApiException` (DCS-022) and **never** retried, refreshed or recovered automatically; re-provisioning is an operator action.
- **No scope modelling** for DK- keys (the platform does not enforce them). `scope_violation` (403) is surfaced as-is.
- **No organisation, environment, tenant or default host.** `DistenkaClientOptions.BaseAddress` is required and has no default; no host name is compiled into the assembly (DCS-020). TLS rules: DCS-081.
- **No HQ or licence-server communication, telemetry, update or activation check.** The client talks only to `BaseAddress`.
- **No credential in processor configuration by default.** `ExecutionConfig.APIKey` is removed (DCS-045). A processor that itself calls the platform receives a DK- key as an owner-bound configuration property (ACC-37) and handles it per DCS-027.

### 2.3 Key supply (summary — specified by DCS-027)

The DK- key is provisioned in the console and consumed by the SDK as a **runtime-injected secret**: configuration binding (`Distenka:ApiKey`) from environment variables, user-secrets in Development, or a host-supplied secret provider; never a source literal, never committed, never inside a NuGet package, never in logs, exception messages, the results file or the run summary line.

## 3. Consequences

**Positive**
- The client works against any self-hosted deployment with nothing but a base URL and a key.
- The credential model maps one-to-one onto the platform's own selector; there is no SDK-only authentication concept to drift.
- A leaked SDK-held key cannot be used through the SDK to mint or revoke keys (the platform forbids it; the SDK does not even expose the routes).

**Negative / limitations**
- Applications that want to act as a signed-in *user* (JWT) are not served by the SDK; they must bring their own `IDistenkaCredential` without support.
- Key expiry or revocation shows up as a 401 at the next call; there is no proactive warning.
- **Discrepancy recorded, not bridged:** AD-9 says keys "do not expire under the current product model". The platform's *default* is no expiry, but an owner can set `expiresAt` at issue (≤ 10 years). The SDK's behaviour is the same either way (surface 401), so no decision is reopened; the README wording must say "no expiry unless the owner set one" (DCS-027 / DCS-090).
- `X-Product-Key` remains accepted by the platform for one deprecation cycle; the SDK not sending it has no compatibility cost because the same handler accepts `Bearer DK-` (`ProductKeyAuthenticationHandler.cs:158-168`). **Confirmed by the platform owner (Karim), 2026-10-31: new SDK clients never need `X-Product-Key`.**

## 4. Implementation

| Rule | Story |
|---|---|
| `IDistenkaCredential`, three credential types, shape checks, canonicalisation, prefix-only `ToString()` | DCS-021 |
| `BaseAddress` required, no default host, single `Authorization` header via a delegating handler | DCS-020 |
| 401 / 403 `session_required` / `scope_violation` / `invocation_key_required` / `run_capability_required` surfaced, 401 never retried | DCS-022, DCS-025 |
| `RunCapabilityCredential.ExpiresAt` from `resultCapabilityExpiresAt` | DCS-060, DCS-062 |
| Secret supply (configuration binding, owner binding, packaging guard) | DCS-027 |
| Redaction in logs, exceptions, options | DCS-080 |
| TLS default | DCS-081 |
| Removal of `ExecutionConfig.OrgName/APIKey/Env/…`, `DistenkaApiConfig` | DCS-045 |

## 5. Evidence

Platform (`D:\Product\Distenka` @ `4c3cf2c`, read 2026-10-31):
- `Distenka.Core.API/Startup.cs:295-321` — credential selector and its order.
- `Distenka.Core.API/Authentication/ProductKeyAuthenticationHandler.cs:109` (`X-Product-Key`), `:122-124` (`DK-`, `Bearer `), `:143-171` (`TryReadCredential`).
- `Distenka.Core.API/Authentication/InvocationKeyAuthenticationHandler.cs:62-78`; `RunCapabilityAuthenticationHandler.cs:60-76`.
- `Distenka.Core.Kernel/Identity/CredentialShape.cs` (`PrefixHexLength = 8`, `MaximumLength = 128`, `Normalise`, `IsDistinctFrom`); `ApiKeyFormat.cs:42-48`; `InvocationKeyFormat.cs:37-43`; `CredentialShapes.cs`.
- `Distenka.Core.Kernel/Identity/RunCapabilityService.cs:79` (`drc1.`), `:121-131` (mint), `:141-190` (read), `:195-207` (payload format).
- `Distenka.Core.API/Controllers/AccountKeysController.cs:20-45` (session-only rationale), `:46` (`MaximumLifetime` 3650 days), `:76-100` (list/issue, optional `expiresAt`), `:179-193` (`session_required`).
- `Distenka.Core.Kernel/Identity/CallerIdentity.cs:50-60` (scopes carried, not enforced).
- `docs/api-invocation-keys.md` §The three credentials, §Issuing one.

SDK: `git show 856048c^:src/Distenka/BroadCast/Constants.cs`, `…/ApiKeyProvider.cs`, `…/IDistenkaClient.cs`; `src/Distenka/ExecutionConfig.cs`, `src/Distenka/DistenkaApiConfig.cs`.

Backlog: AD-4, AD-8, AD-9 (Decision 4, 2026-10-31).
