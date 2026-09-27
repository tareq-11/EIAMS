# Real integration default and sandbox boundary

`tests/IntegrationTests` is a real database integration suite. Its default
`IntegrationTestWebAppFactory` starts a disposable PostgreSQL 17 Testcontainer, applies the
repository migrations, and supplies that container's connection string to the API host. If
Docker/PostgreSQL cannot start, fixture initialization fails; it does not substitute EF InMemory,
SQLite, or a mock repository. The safety tests assert the runtime EF provider/database and ensure
the integration project does not reference fake/in-memory database providers.

EF InMemory remains available to `Application.UnitTests` for isolated handler/domain tests. Those
unit tests are not evidence for PostgreSQL constraints, transactions, locks, migrations, or real
HTTP-pipeline persistence and must not replace the corresponding integration suites.

This repository contains no frontend application, UI API transport, mock service worker, browser
sandbox, or auth-bypass flag to audit. Therefore no UI sandbox can be enabled here, and no silent
UI mock mode exists in the checked-in tree. If a UI is added, sandbox/mock behavior must be a
separate, visible, explicit opt-in that defaults off; production/staging builds must fail closed
if mock or auth-bypass flags are enabled. A browser sandbox should use isolated synthetic data and
must not silently intercept or replace the real API in the normal application path.
