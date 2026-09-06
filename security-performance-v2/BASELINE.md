# Security and Performance V2 — Baseline

Date: 2026-09-05. Starting commit: `e59dcfa`.

This is a source/configuration scan and authorized test run, not an external penetration test or a production load test.

## Environment and inventory

| Item | Value |
|---|---:|
| .NET SDK | 10.0.107 |
| PostgreSQL client | 18.3 |
| Controllers | 172 |
| HTTP controller actions | 172 |
| Query handlers | 82 |
| Command handlers | 96 |
| EF migrations, excluding designers | 30 |
| NuGet projects audited | 8 |

The integration suite uses an isolated PostgreSQL 17 Testcontainer and generated test data. No production/Neon database was mutated.

## Verification before V2 changes

| Check | Result |
|---|---|
| Release build | PASS — 0 errors, 0 warnings |
| Application unit tests | PASS — 452/452 |
| Architecture tests | PASS — 13/13 |
| Integration tests | PASS — 248 passed, 2 skipped, 250 total |
| NuGet direct/transitive vulnerability scan | PASS — no known vulnerable packages reported |
| Endpoint authorization classification | PASS — 168 permission/authenticated controllers, 4 explicitly anonymous |

The two skipped tests are explicit performance measurements gated by `RUN_PERFORMANCE_TESTS` and `RUN_API_BENCHMARKS`. They are not security regressions, but this means the baseline does not yet contain representative latency percentiles.

## Six-layer security scan

| Layer | Status at baseline | Evidence |
|---|---|---|
| Packages | PASS | All 8 `.csproj` files audited, including transitive dependencies |
| Secrets | WARN | No Neon token in current tracked tree; historical credential pattern remains in 8 commits and requires external rotation confirmation |
| OWASP code patterns | PASS/WARN | No dangerous deserialization, MD5/SHA1 security use, dynamic process loading, or obvious interpolated raw SQL; 3 raw SQL call sites reviewed as parameterized/constant |
| Authentication/authorization | WARN | Explicit route protection is present; bootstrap, session races, and auth input limits remain in findings |
| CORS/proxy/host | FAIL at baseline | Empty trusted-proxy collections caused forwarded headers to trust any peer; explicit production host validation was incomplete |
| Data protection/logging | PASS/WARN | Defensive responses and audit redaction present; browser refresh token remains in JSON and dedicated auth-failure metrics are absent |

The only non-Development tracked password match is the disposable local `docker-compose.yml` PostgreSQL credential. It is classified INFO, not a production secret. Current remote credentials were not printed in scan output.

## Performance limits of this baseline

- The existing API benchmark has only 7 warm samples per read and 3 repeated logins.
- It runs through the integration host rather than a real Kestrel/TLS path.
- No representative Small/Medium/Large dataset was generated in this run.
- `pg_stat_statements` and production-like connection/network measurements were not used.
- Therefore no p95/p99 or throughput claim is accepted from this stage.

## Artifacts

- `ENDPOINT-MATRIX.md`: 172 controller actions with effective `/api/v1` path, access declaration and named limiter.
- `FINDINGS.md`: confirmed findings, hypotheses requiring deterministic tests, and current remediation status.
