# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build --configuration Release --no-restore

# Run the API (from repo root)
dotnet run --project DeuxERP.API

# Run all tests
dotnet test --configuration Release --verbosity normal

# Run a single test class
dotnet test --filter "FullyQualifiedName~OrderIntegrationFlowTest"

# Add a new EF Core migration
dotnet ef migrations add <MigrationName> --project DeuxERP.Infrastructure --startup-project DeuxERP.API

# Apply migrations
dotnet ef database update --project DeuxERP.Infrastructure --startup-project DeuxERP.API

# Start the PostgreSQL container
docker-compose up -d
```

## Local Setup

1. Start the database: `docker-compose up -d`
2. Configure user secrets on `DeuxERP.API`:
   ```bash
   dotnet user-secrets set "JwtSettings:Secret" "<32+ char key>"
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=deux_orders;Username=admin_test;Password=admin_test_pwd"
   ```
3. Run migrations: `dotnet ef database update --project DeuxERP.Infrastructure --startup-project DeuxERP.API`
4. `dotnet run --project DeuxERP.API`

Migrations are also applied automatically on startup via `context.Database.Migrate()` in `Program.cs`.

## Architecture

Clean Architecture with five projects:

- **DeuxERP.Domain** — Entities, enums, repository interfaces (`IOrderRepository`, `IClientRepository`, etc.), `IUnitOfWork`, and shared models (`PagedResult<T>`). No external dependencies.
- **DeuxERP.Application** — Business logic services, DTOs, and entity-to-DTO mapping (`DtoMappingExtensions`). Depends only on Domain.
- **DeuxERP.Infrastructure** — EF Core `ApplicationDbContext`, repository implementations, migrations, and `TokenService` (JWT generation). Depends on Domain and Application.
- **DeuxERP.API** — ASP.NET Core controllers, FluentValidation validators, `GlobalExceptionHandler` middleware, `ExportService` (CSV/PDF), and `Program.cs` DI wiring.
- **DeuxERP.Tests** — xUnit integration tests.

**Request flow:** Controller → Service (Application) → Repository (Infrastructure) → EF Core → PostgreSQL

**Key design decisions:**
- All data access goes through repository interfaces defined in Domain.
- `OrderService` and `DashboardService` in the Application layer handle those domains; `ClientController` and `ProductController` bypass the service layer and call repositories directly.
- Controllers are thin.
- Domain entities are rich: business logic lives in entity methods (e.g., `Order.MarkAsCompleted()`, `Order.UpsertItem()`). Business rule violations throw `InvalidOperationException`.
- `GlobalExceptionHandler` middleware centralizes error responses — converts `InvalidOperationException` → 400, everything else → 500. Do not add try/catch in controllers.
- FluentValidation validators in `DeuxERP.API/Validations/` run automatically via `AddFluentValidationAutoValidation()` before controllers are hit. Use these for input validation; use domain entity methods for business rules.
- Prices and totals are stored as `int`/`long` (integer cents) to avoid floating-point precision issues.
- `Order` uses Guid V7 (timestamp-based); all other entities use `Guid.NewGuid()`.
- `Order` tracks both `TotalPaid` (actual paid amount) and `TotalValue` (base price), enabling discount tracking at the item level (`PaidUnitPrice` vs `BaseUnitPrice`).
- Order create/update intentionally accepts item prices that differ from the product catalog price. `UnitPrice`/`PaidUnitPrice` must only be non-negative; the product price remains the base price used for `TotalValue`.
- Enums are serialized as strings globally via `JsonStringEnumConverter` — use string enum values in request/response bodies.

## Domain Entity Pattern

Entities use private setters and a private parameterless EF Core constructor. All modifications go through public methods:

```csharp
// Correct: mutate via method
order.MarkAsCompleted();
order.UpsertItem(productId, quantity, paidPrice, basePrice, observation);

// Never: set properties directly
order.Status = OrderStatus.Completed; // compile error — private setter
```

`Order` is the aggregate root for `OrderItem`. Items are only accessible via order methods.

Note: `Product` omits the private parameterless constructor — EF Core ≥ 8 can instantiate it without one. New entities should still include `private Entity() { }` for consistency.

## PostgreSQL Array Columns

`List<string>` properties map to PostgreSQL native `text[]` columns. Configure them in `OnModelCreating`:

```csharp
entity.Property(o => o.MyList)
      .HasColumnType("text[]")
      .IsRequired(false);
```

No JSON serialization needed — Npgsql handles the mapping natively. See `Order.References` as the reference implementation.

## Product Endpoints: Multipart Form Data

`POST /products/new` and `PUT /products/{id}` use `[FromForm]` (multipart/form-data) because they accept an optional `IFormFile` image. All other endpoints use JSON. Image files are uploaded directly to Cloudflare R2 by the API; clients receive a public URL in the response.

The unused records `CreateProduct` and `UpdateProduct` in `DeuxERP.Application/DTOs/` are stale — the actual request models used by `ProductController` are `CreateProductRequest` and `UpdateProductRequest` in `DeuxERP.API/Models/ProductRequests.cs`.

## Testing

Tests live in `DeuxERP.Tests` and use xUnit + FluentAssertions. `IntegrationTestFactory<TProgram>` replaces PostgreSQL with an in-memory EF Core provider and injects a test JWT secret. There are no unit test mocks — all tests run against in-memory EF Core.

`BaseIntegrationTest` provides `_client` (HttpClient), `_factory`, and `AuthenticateAsync()` which registers a user, logs in, and sets the Bearer token on `_client`.

## API Routes

All routes are prefixed with `/api/v1/`. All endpoints except `POST /auth/login` require a JWT Bearer token (`POST /auth/register` also requires a valid JWT — only authenticated users can create new users).

**Auth**
- `POST /auth/login` — Returns JWT. Rate-limited to 10 requests/minute.
- `POST /auth/register` — Requires JWT. Creates a new user.

**Orders**
- `POST /orders/new`
- `GET /orders/all?page=&size=&status=` — Paginated. Response: `{ items[], totalCount, pageNumber, pageSize }`.
- `GET /orders/{id}`
- `PUT /orders/{id}` — Update delivery date, status, or items.
- `DELETE /orders/{id}`
- `PATCH /orders/{id}/complete` / `PATCH /orders/{id}/cancel`
- `PATCH /orders/{id}/items/{productId}/cancel`
- `PATCH /orders/{id}/items/{productId}/quantity`
- `POST /orders/references/presigned-url` — Returns a pre-signed R2 PUT URL and object key. iOS uploads image directly to R2, then passes the key in the order request.

**Clients**
- `POST /clients/new`, `GET /clients/{id}`, `PUT /clients/{id}`, `DELETE /clients/{id}`
- `GET /clients/all?search=&status=` — Searchable, filterable.
- `PATCH /clients/{id}/active` / `PATCH /clients/{id}/inactive`
- `GET /clients/dropdown?status=` — Returns `{ id, name }` list for UI dropdowns.

**Products**
- `POST /products/new`, `GET /products/{id}`, `PUT /products/{id}`, `DELETE /products/{id}`
- `GET /products/all?search=&status=` — Searchable, filterable.
- `PATCH /products/{id}/active` / `PATCH /products/{id}/inactive`
- `GET /products/dropdown?status=` — Returns `{ id, name, price, category, size }` list for UI dropdowns. The frontend groups duplicate product names and uses `size` to choose the concrete product variant.

**Dashboard**
- `GET /dashboard/summary?createdAtFrom=&createdAtTo=&status=` — Aggregate metrics (revenue, discounts, order counts).
- `GET /dashboard/revenue-over-time?createdAtFrom=&createdAtTo=&status=` — Daily revenue data points.
- `GET /dashboard/top-products?createdAtFrom=&createdAtTo=&status=&limit=10`
- `GET /dashboard/top-clients?createdAtFrom=&createdAtTo=&status=&limit=10`
- `GET /dashboard/export?from=&to=&status=&format=csv|pdf` — Export via QuestPDF (PDF) or custom CSV writer. Excludes canceled items.

Dashboard repository queries use `AsNoTracking()`. Canceled orders are always counted separately regardless of status filter.

**Payments (AbacatePay)** — payment infrastructure exists in the DB (`PaymentTransaction`, `WebhookEventLog`, `CheckoutSession`) but no payment controller has been built yet.

**Cash Flow**
- `POST /cash/entries` — Create income or expense entry.
- `GET /cash/entries?from=&to=&type=` — Paginated list filtered by date range and type.
- `GET /cash/balance` — Running balance (sum of all entries up to today).
- `POST /cash/entries/{id}/pay` / `POST /cash/entries/{id}/unpay` — Mark entry paid/unpaid.
- `GET /cash/audit/{entryId}` — Audit log for a specific entry (append-only via `CashFlowAuditInterceptor`).

## Domain Events

`Entity` (base class in `DeuxERP.Domain/Common/Entity.cs`) supports domain events via `AddDomainEvent()` / `ClearDomainEvents()`. Events implement `IDomainEvent` (single property: `DateTime OccurredAt`). No dispatcher is wired yet — events accumulate on the entity but are not published.

## EF Core Migrations

The `dotnet ef` CLI is blocked by WDAC on this machine. **Always write migrations manually** as `.cs` files in `DeuxERP.Infrastructure/Migrations/`, using the fully-qualified entity type names (e.g., `DeuxERP.Domain.Cash.CashFlowEntry`). Follow the existing migration files as templates.

## Environment / Auth Mode

A `.env` file at the repo root sets `RUN_MODE=DEV|PROD`. In `DEV` mode, authentication is relaxed and OpenAPI/Swagger is enabled. Secrets (JWT secret, connection string, R2 keys) must be in environment variables — never in `appsettings.json`.
