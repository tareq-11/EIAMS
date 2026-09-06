# Phase 0 and First Remediation Batch

Date: 2026-09-05

## Completed

- Captured SDK, repository, package-audit, endpoint, security-static-analysis, build, and test baselines.
- Closed the forwarded-header trust bug: no configured trusted peer now means forwarded headers are disabled; explicit peers are limited to one hop.
- Hardened production `AllowedHosts` validation against wildcard list entries, any-address values, malformed paths, schemes, and empty entries.
- Moved rate limiting before authorization and proved rejected traffic is limited before repeated permission work.
- Added an 8 KiB body limit to login, refresh, logout, bootstrap registration, and administrator user creation.
- Added bounded validation for login email/password and refresh/logout tokens.
- Replaced in-memory multi-source deep-page merging for counterparts and custodies with PostgreSQL `UNION ALL`, total count, stable ordering, and SQL-side pagination.

## Verification

- Release builds used warnings-as-errors and completed with zero warnings/errors.
- Focused configuration, request-limit, rate-limit, custody, and counterpart integration tests passed.
- Release build: 0 warnings, 0 errors.
- Application unit tests: 455 passed, 0 failed.
- Architecture tests: 13 passed, 0 failed.
- Integration tests: 270 passed, 0 failed, 2 explicitly skipped performance benchmarks.

## Still open in Phase 1

- Add explicit concurrency limits for CPU-heavy authentication, large uploads, reports, and posting after selecting safe per-instance defaults.
- Define gateway/distributed rate limiting for multi-instance production deployment.
- Finish bounded collection/search limits across non-auth endpoints.
- Capture representative PostgreSQL query plans and allocation measurements; functional correctness alone is not a performance claim.
