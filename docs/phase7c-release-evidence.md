# Phase 7C — Final release evidence

**Result: local verification is green after resolving fixture regressions, but release sign-off is not granted.** External database preflights and staging/proxy evidence remain operator-owned and were not run. No Neon or production endpoint/database was contacted, and no remote mutation was performed.

## Build and tests

| Command | Result | Evidence |
|---|---|---|
| `dotnet build CleanArchitectureTemplate.slnx --no-restore` | PASS; 9.24 s, 0 warnings/errors | Initial Debug baseline build; the final fixture changes were subsequently compiled by the Release build below. |
| `dotnet build CleanArchitectureTemplate.slnx --configuration Release --no-restore` | PASS; 32.16 s, 0 warnings/errors | Release solution build after final test-fixture changes. |
| `dotnet test CleanArchitectureTemplate.slnx --no-build` | PASS | Debug run: Unit 598/598; Architecture 26/26; PostgreSQL integration 723 passed, 14 opt-in performance tests skipped; integration duration 3 m 50 s. |
| `dotnet test CleanArchitectureTemplate.slnx --configuration Release --no-build` | One transient unit failure; other projects passed | Architecture 26/26. PostgreSQL integration 723 passed, 14 skipped, 4 m. Unit tests 597 passed and `ClamAvAttachmentMalwareScannerTests.Scan_ShouldReturnUnavailableForConnectionFailure_AndPropagateCallerCancellation` failed once with `EndOfStreamException` during fake-server teardown. |
| `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~ClamAvAttachmentMalwareScannerTests.Scan_ShouldReturnUnavailableForConnectionFailure_AndPropagateCallerCancellation'` | PASS; 1/1, 5 s | The exact transient failure did not reproduce. |
| `dotnet test tests/Application.UnitTests/Application.UnitTests.csproj --configuration Release --no-build` | PASS; 598/598, 9 s | Full Release unit assembly rerun. Thus all project suites have passing evidence, although the first Release solution invocation had the one transient failure above. |
| Targeted rerun of the 20 previously failing integration tests | PASS; 20/20, 20 s | Covered posting/provenance, dotted seed permissions, migration rollback guard, secure-cookie startup, and phase 3A inventory tests. |

The posting regression fixtures now capture `DocumentLineProvenance` at line creation; the production fail-closed provenance guard was not weakened. Other fixture corrections align seeded permissions/scopes with the dotted-only catalog and Enterprise/Site/Warehouse assignment contract. The production secure-cookie test uses a pristine disposable database so unrelated intentionally invalid shared fixtures cannot defeat the strict startup invariant.

## Contracts and automated gates

| Command | Result | Evidence |
|---|---|---|
| `scripts/validate-ci-workflow.sh` | PASS | CI workflow and weekly-performance contracts valid. |
| `jq empty contracts/openapi/eiams-backend-v1.openapi.json` | PASS | Checked-in OpenAPI JSON parses; document reports OpenAPI 3.0.4. Full integration suite also passed OpenAPI snapshot tests. |
| `EIAMS_BASELINE_OUTPUT=/tmp/phase0-baseline-7c.json bash scripts/run-phase0-baseline.sh` | WARN, exit 4 | Source/API inventory completed; database status is UNAVAILABLE because no DB target was supplied. |
| `RBAC_V2_OUTPUT=/tmp/rbac-v2-preflight-7c.json bash scripts/run-rbac-v2-preflight.sh` | WARN, exit 4 | DB status is UNAVAILABLE because no DB target was supplied. |
| `bash scripts/run-migration-preflight.sh` | NOT RUN | Requires a specifically approved non-Neon PostgreSQL target; none was provided. |

## Backup/restore rehearsal

`bash scripts/backup-restore-rehearsal.sh` **passed** against a newly created, disposable loopback Docker PostgreSQL instance with synthetic rows (one user, one audit record) and one synthetic attachment. RPO/RTO objectives were each 300 seconds; measured potential data loss was 1 second and recovery time was 0 seconds. Restore verification found one user, one audit row, one attachment, zero missing/orphan files, and zero checksum mismatches. The disposable database/container and attachment fixture were removed afterward; the redacted evidence remains at `/tmp/eiams-phase7c-backup-evidence/backup-restore-rehearsal-*.json`.

The runner labels this evidence “local Testcontainers”; the actual source was a manually created disposable Docker PostgreSQL container, not an xUnit Testcontainers-managed container. It was nevertheless local, loopback-only, synthetic, quiesced, and disposable. This rehearsal does not establish production backup age, online atomicity, real-data fidelity, or operator RPO/RTO approval.

## Local performance evidence

`GITHUB_SHA=phase7c-final5-20260927 bash scripts/run-weekly-performance-checks.sh /tmp/eiams-phase7c-performance-final5` completed all 9 bounded Quick suites successfully. `scripts/assert-trx-tests-ran.sh` and `scripts/assert-json-artifacts.sh` passed for every suite; 9 TRX files and 12 valid redacted JSON evidence files are retained under `/tmp/eiams-phase7c-performance-final5/`.

The API latency and API-load benchmark fixtures now grant dotted catalog permissions only to the disposable integration administrator needed by their endpoint matrix; production role grants were not broadened. The synthetic dataset fixture uses dotted permissions and grants each synthetic role the scope types its test users actually receive.

## Operator-owned evidence still required

- Run read-only migration, identity/assignment, RBAC, and domain preflights against an explicitly approved staging restore; review zero-blocker counts and sanitized artifacts. The current source-only preflights are WARN/UNAVAILABLE, not clean-data evidence.
- Run staging release evidence against the approved same-origin reverse-proxy topology, including forwarded-header, secure-cookie, health, and authenticated read-only checks. Local tests do not establish real proxy behavior or staging data/permissions.
- Have the operator approve that the synthetic local backup rehearsal setup/objectives represent the intended recovery procedure; production backup age and real-data recovery remain untested.
- Compare retained local performance evidence against the designated staging/production-equivalent baseline and SLOs; local Quick measurements do not establish deployment capacity.

No credentials, connection strings, tokens, production identifiers, or response bodies are recorded in this report.
