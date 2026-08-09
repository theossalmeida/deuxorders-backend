# DeuxERP Backend Test Plan

## Objective

Continuously verify that the backend builds and that its order, inventory, cash,
client, product, authentication, notification, and persistence behavior remains
safe after every commit and in a daily PostgreSQL-backed regression run.

## Test levels

| Level | Project | Purpose | Trigger |
| --- | --- | --- | --- |
| Unit | `DeuxERP.UnitTests` | Fast domain rules with no HTTP, database, clock wait, or external service | Every push, pull request, daily, manual |
| Integration | `DeuxERP.Tests` | HTTP API, authentication, validation, persistence, migrations, and domain-event side effects using PostgreSQL in CI | Every push, pull request, daily, manual |

Local integration tests fall back to EF Core InMemory when
`TEST_DATABASE_CONNECTION_STRING` is absent. This is convenient for development,
but the CI PostgreSQL result is the release signal because it exercises relational
constraints and provider-specific behavior.

## Xray structure

Create one Xray Test Plan named `DeuxERP Backend Continuous Regression`, then add
the following Test Sets. Automated tests imported from JUnit should be assigned by
their namespace or `TestSet` trait.

| Test Set | Scope | Current automated coverage |
| --- | --- | --- |
| Backend - Orders | lifecycle, items, totals, status transitions | unit + integration |
| Backend - Payments and Cash | pay/unpay, idempotency, audit, summaries | unit + integration |
| Backend - Inventory | materials, recipes, deduction and restoration | unit + integration |
| Backend - Clients and Products | CRUD, search, totals, statistics | integration |
| Backend - Authentication and Authorization | login and role boundaries | integration |
| Backend - Notifications | subscription lifecycle and status | integration |
| Backend - Storage | validation, upload sessions, single use, delete failure and compensation | unit + integration |
| Backend - PostgreSQL Constraints | unique indexes, concurrency, migrations, SQL translation | CI integration |

Each CI import should create a Test Execution linked to the continuous-regression
Test Plan. Use `CI` and `PostgreSQL` as Test Environments and include the commit SHA
and workflow URL in the execution metadata.

## Entry and exit criteria

Entry criteria:

- solution restores and compiles on .NET 10;
- PostgreSQL service is healthy and migrations apply;
- required test configuration is present;
- storage and push delivery remain replaced by controlled test doubles.

Exit criteria:

- all unit and integration tests pass;
- no test is skipped without an approved reason;
- TRX, JUnit, and Cobertura outputs are uploaded even on failure;
- Xray receives the JUnit execution when its credentials are configured.

Any failure blocks the CI job. A scheduled failure should be investigated as a
service-regression signal even when no source commit was made that day.

## Endpoint scenario matrix

Every current controller route is exercised by at least one automated scenario.
Coverage is organized around lifecycles so one deterministic setup validates the
success path, meaningful boundary conditions, persistence effects, and security.

| Route group | Scenario focus |
| --- | --- |
| Authentication | bootstrap registration, validation, duplicate identity, valid/invalid login |
| Clients | create, detail, update, status, dropdown, list/stats/orders, order eligibility, delete |
| Products | create, detail, update, status, dropdown/list/search, image compensation, recipes/stats/delete |
| Orders | lifecycle, filters, totals, item mutations, terminal states, payment, references, delete-as-cancel |
| Inventory | material lifecycle, weighted restock, recipes/options, deduction, warnings, restoration |
| Cash | authorization, create/get/update/audit/delete, soft-delete visibility, payment/reversal idempotency |
| Dashboard | exact aggregate dataset, timeline, rankings, filtered CSV export |
| Push | subscribe, status, ownership isolation, unsubscribe, reactivation |

## Quality rules

- Prefer one lifecycle scenario with exact state assertions over several shallow
  endpoint-only tests.
- Keep separate tests when the failure represents a distinct operational risk,
  especially payment idempotency, authorization, storage compensation, and
  relational integrity.
- Do not create a test for every FluentValidation clause. Cover representative
  boundaries plus rules that change money, authorization, ownership, or persisted
  state.
- A new endpoint must be added to the matrix or explicitly documented as excluded.

## Remaining decisions and gaps

1. Confirm the official allowed order-status transition matrix. Current behavior
   permits reopening completed and canceled orders, and the suite documents that.
2. Add a PostgreSQL duplicate automatic cash-event test alongside the existing
   unique-user and `xmin` concurrency scenario.
3. Add health/smoke monitoring against the deployed homologation endpoint. The
   current daily workflow proves the code and database integration, not that a
   deployed instance is reachable.

## Ownership and maintenance

- A behavior change must update its automated test in the same pull request.
- A production defect should gain a regression test before or with the fix.
- Review flaky tests immediately; do not weaken assertions or add sleeps.
- Review this plan and its Xray Test Sets whenever a backend module is added.
