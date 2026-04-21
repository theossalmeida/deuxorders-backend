# .NET Development Rules

## Architecture
- Enforce Clean Architecture: Domain → Application → Infrastructure → Presentation (API)
- Enforce DDD: Aggregates, Entities, Value Objects, Domain Events, Repositories (interface in Domain, impl in Infrastructure)
- No business logic in Controllers or Infrastructure — Controllers dispatch, Infrastructure persists
- Use Result<T> or similar pattern instead of throwing exceptions for expected failures
- Prefer rich domain models over anemic models

## Security
- Never trust external input — validate at the boundary (FluentValidation or DataAnnotations)
- Never expose internal IDs directly in API responses when avoidable — use slugs or encoded references
- Always use parameterized queries — never concatenate SQL
- Secrets via environment variables or IConfiguration — never hardcoded
- Apply authorization checks at the application layer, not only at the controller level

## Performance
- Evaluate need for caching on every read-heavy endpoint: IMemoryCache for single-instance, IDistributedCache (Redis) for distributed
- Use async/await throughout — no .Result or .Wait() blocking calls
- Prefer IQueryable projections (Select) over loading full entities when returning DTOs
- Paginate all list endpoints — never return unbounded collections
- Profile N+1 queries — use Include() or split queries deliberately

## Financial & Business Operations (Orders, Cash Flow, Inventory)
- Every financial operation must be wrapped in a database transaction
- Apply idempotency keys on commands that can be retried (payments, stock movements)
- Use the Outbox Pattern for operations that emit domain events or integrate with external services
- Apply optimistic concurrency (row versioning / ETag) on aggregates with concurrent write risk
- Implement soft delete — never hard delete financial or operational records
- Maintain full audit trail (CreatedAt, UpdatedAt, CreatedBy, UpdatedBy) on all business entities

## Dependency Injection
- Register dependencies explicitly — avoid service locator pattern
- Prefer constructor injection
- Respect correct lifetimes: Singleton for stateless services, Scoped for per-request, Transient for lightweight stateless
- Never inject Scoped into Singleton

## Code Quality
- Follow SOLID strictly
- One class per file, named after the class
- Handlers (MediatR or equivalent) must be thin — orchestrate, do not implement business logic inline
- No magic strings — use constants or strongly-typed enums
- All public interfaces and domain methods must have XML doc comments

## Testing
- Unit test Domain and Application layers in isolation — mock Infrastructure interfaces
- Integration test Infrastructure (repositories, external calls) against real or containerized dependencies
- Never adapt production code to make a test pass — fix the code or fix the test
- Aim for high coverage on Domain and Application; Infrastructure tests are narrower but must exist
