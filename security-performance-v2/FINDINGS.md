# Security and Performance V2 — Findings Register

This register distinguishes confirmed behavior from concurrency/performance hypotheses. Severity may change after executable proof and deployment-context review.

| ID | Severity | Status | Finding |
|---|---|---|---|
| SP-01 | High | Fixed in working tree; targeted tests pass | Empty `KnownProxies`/`KnownIPNetworks` trusted arbitrary forwarded headers. Processing is now disabled with empty trusted-peer configuration and limited to one hop when explicitly enabled |
| SP-02 | High hypothesis | Proof test pending | Authorization cache invalidates before an enclosing transaction commits, allowing possible refill with pre-commit grants |
| SP-03 | High hypothesis | Proof test pending | Refresh is serialized by token, while Logout and Suspend use different/no session lock |
| SP-04 | Performance P0 | Fixed functionally; benchmark pending | Custody and counterpart now compose `UNION ALL`, count, order and paginate in PostgreSQL instead of fetching `offset + pageSize` per source |
| SP-05 | Medium | Fixed for current single-instance limiter; distributed limit pending | Rate limiting now runs after authentication but before authorization, preserving user partitioning while rejecting over-limit requests before permission work |
| SP-06 | Medium | Planned | Local HybridCache does not prove cross-instance permission invalidation |
| SP-07 | Medium/performance | Planned | PBKDF2 work is intentionally expensive, but the stored format has no algorithm/work-factor version for safe tuning |
| SP-08 | Performance P1 | Confirmed | Existing benchmark sample and execution path are insufficient for stable p95/p99 claims |
| SP-09 | High | Confirmed | Anonymous first-user bootstrap can be claimed by the first network caller against an empty production database |
| SP-10 | Medium | Fixed in working tree; targeted tests pass | Production host validation accepted a wildcard embedded inside a semicolon-separated list and top-level any-address values |
| SP-11 | Medium | Contract migration pending | Refresh token is returned to browser JavaScript in JSON despite also using an HttpOnly cookie; body wins on conflict |
| SP-12 | Low | Planned | CI lacks PR trigger/SHA-pinned actions and parses localized human-readable NuGet audit output |
| SP-13 | Medium | Fixed in working tree; targeted tests pass | Auth controllers use an 8 KiB request limit; email, password, and refresh-token inputs have explicit validation bounds |
| SP-14 | Low | Planned | Dedicated bounded-label metrics for login failures, 429 and refresh replay are absent |
| SP-15 | Medium, operational | External confirmation pending | Historical remote database credential pattern exists in Git history; current tracked tree has no Neon token |

## SP-01 remediation evidence

- `ForwardedHeaders.None` is selected when no trusted proxy/network exists.
- Explicit proxy configuration enables only `X-Forwarded-For` and `X-Forwarded-Proto` with `ForwardLimit = 1`.
- Invalid IP configuration fails startup/options resolution.
- 3 focused tests pass.

## SP-10 remediation evidence

Production validation rejects empty hosts, `*`, mixed wildcard lists, `0.0.0.0`, `[::]`, schemes, paths and empty list entries. It accepts explicit hosts and optional explicit ports. Development keeps its local wildcard behavior. Twelve focused cases plus the three proxy cases pass.

## SP-04/SP-05/SP-13 remediation evidence

- PostgreSQL integration tests execute the new custody and counterpart multi-source queries successfully.
- SQL-side pagination uses a stable `FromUtc`, subject type, and custody ID ordering for custody records.
- A low-limit integration test proves an unauthorized request receives 401 first and 429 next, so repeated denied requests do not repeat authorization work indefinitely.
- Controller metadata tests cover all five authentication/user-provisioning body limits, while validator tests cover oversized credentials and tokens.

## Next executable proofs

1. SP-02: deterministic pre-commit cache refill test.
2. SP-03: deterministic Refresh/Logout/Suspend interleavings.
3. SP-09: production bootstrap configuration and operator provisioning test.
4. SP-04: record before/after allocation and query-plan evidence on a representative dataset.
5. Complete concurrency and distributed rate limiting decisions from Phase 1.
