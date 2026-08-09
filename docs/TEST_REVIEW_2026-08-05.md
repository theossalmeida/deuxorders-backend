# Test Suite Review — 2026-08-05

## Result

After the endpoint and business-rule expansion, the local baseline is:

- 18 unit tests passed;
- 68 API integration tests passed with the local EF Core InMemory fallback;
- 0 failed and 0 skipped.

The GitHub workflow now runs those 60 API tests against PostgreSQL 17. That CI run
is the authoritative integration result; PostgreSQL was not available in the local
environment during this review.

## Findings

### Critical — the suite and CI did not start

`Program.cs` requires `ALLOWED_ORIGINS` outside `RUN_MODE=DEV`, while neither the
test factory nor the workflow supplied it. All 60 tests failed during host startup.
The factory and workflow now provide an isolated test origin.

### High — integration tests used a non-relational provider

All API tests used EF Core InMemory, so unique indexes, foreign keys, migrations,
PostgreSQL query translation, transactions, and `xmin` concurrency were not being
tested. The factory now selects Npgsql when `TEST_DATABASE_CONNECTION_STRING` is
set, and CI supplies a PostgreSQL 17 service and applies the real migrations.

### High — there was no unit-test layer

Domain behavior could only be exercised through the full HTTP host. A dedicated
unit project now covers representative order totals and guards, inventory weighted
cost, and manual cash-entry rules. This is an initial layer, not complete domain
coverage.

### High — CI did not run daily or on every branch name

There was no schedule. The push filter used `*`, which does not reliably represent
nested branch names such as `feature/orders`. The workflow now uses `**`, runs at
09:00 UTC daily, and also supports manual dispatch.

### Medium — test evidence was not ready for Xray

Only TRX was emitted. Both test projects now emit JUnit XML (accepted by Xray), TRX
for the GitHub check UI, and Cobertura coverage. Artifacts are retained for 30 days.
The actual Xray import step depends on whether the Jira installation is Xray Cloud
or Server/Data Center and on the Jira project/Test Plan keys.

### Medium — test isolation was implicit

Class fixtures own their databases, while broad assertions in parts of the suite
made the intended isolation unclear. Test parallelization is now disabled. In the
PostgreSQL path each class factory recreates and migrates its database before use.

### Remaining gaps

- Automatic cash-event uniqueness still needs a direct relational constraint test.
- Deployed-service availability is not tested; a code-level daily regression run
  is not an uptime monitor.
- The official order transition matrix remains a product decision. Current tests
  permit reopening completed and canceled orders through the update endpoint.
- Restore reports a high-severity advisory for transitive `Microsoft.OpenApi`
  2.0.0; dependency remediation should be handled separately and verified against
  the API's OpenAPI behavior.

See `docs/TEST_PLAN.md` for the proposed Xray Test Plan and Test Sets.
