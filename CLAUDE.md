# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build --configuration Release --no-restore

# Run the API (from repo root)
dotnet run --project DeuxOrders.API

# Run all tests
dotnet test --configuration Release --verbosity normal

# Run a single test class
dotnet test --filter "FullyQualifiedName~OrderIntegrationFlowTest"

# Add a new EF Core migration
dotnet ef migrations add <MigrationName> --project DeuxOrders.Infrastructure --startup-project DeuxOrders.API

# Apply migrations
dotnet ef database update --project DeuxOrders.Infrastructure --startup-project DeuxOrders.API

# Start the PostgreSQL container
docker-compose up -d
```

## Local Setup

1. Start the database: `docker-compose up -d`
2. Configure user secrets on `DeuxOrders.API`:
   ```bash
   dotnet user-secrets set "JwtSettings:Secret" "<32+ char key>"
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=deux_orders;Username=admin_test;Password=admin_test_pwd"
   ```
3. Run migrations: `dotnet ef database update --project DeuxOrders.Infrastructure --startup-project DeuxOrders.API`
4. `dotnet run --project DeuxOrders.API`

Migrations are also applied automatically on startup via `context.Database.Migrate()` in `Program.cs`.

## Architecture

Clean Architecture with five projects:

- **DeuxOrders.Domain** — Entities, enums, repository interfaces (`IOrderRepository`, `IClientRepository`, etc.), `IUnitOfWork`, and shared models (`PagedResult<T>`). No external dependencies.
- **DeuxOrders.Application** — Business logic services, DTOs, and entity-to-DTO mapping (`DtoMappingExtensions`). Depends only on Domain.
- **DeuxOrders.Infrastructure** — EF Core `ApplicationDbContext`, repository implementations, migrations, and `TokenService` (JWT generation). Depends on Domain and Application.
- **DeuxOrders.API** — ASP.NET Core controllers, FluentValidation validators, `GlobalExceptionHandler` middleware, `ExportService` (CSV/PDF), and `Program.cs` DI wiring.
- **DeuxOrders.Tests** — xUnit integration tests.

**Request flow:** Controller → Service (Application) → Repository (Infrastructure) → EF Core → PostgreSQL

**Key design decisions:**
- All data access goes through repository interfaces defined in Domain.
- `OrderService` and `DashboardService` in the Application layer handle those domains; `ClientController` and `ProductController` bypass the service layer and call repositories directly.
- Controllers are thin.
- Domain entities are rich: business logic lives in entity methods (e.g., `Order.MarkAsCompleted()`, `Order.UpsertItem()`). Business rule violations throw `InvalidOperationException`.
- `GlobalExceptionHandler` middleware centralizes error responses — converts `InvalidOperationException` → 400, everything else → 500. Do not add try/catch in controllers.
- FluentValidation validators in `DeuxOrders.API/Validations/` run automatically via `AddFluentValidationAutoValidation()` before controllers are hit. Use these for input validation; use domain entity methods for business rules.
- Prices and totals are stored as `int`/`long` (integer cents) to avoid floating-point precision issues.
- `Order` uses Guid V7 (timestamp-based); all other entities use `Guid.NewGuid()`.
- `Order` tracks both `TotalPaid` (actual paid amount) and `TotalValue` (base price), enabling discount tracking at the item level (`PaidUnitPrice` vs `BaseUnitPrice`).
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

## Testing

Tests live in `DeuxOrders.Tests` and use xUnit + FluentAssertions. `IntegrationTestFactory<TProgram>` replaces PostgreSQL with an in-memory EF Core provider and injects a test JWT secret. There are no unit test mocks — all tests run against in-memory EF Core.

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
- `GET /products/dropdown?status=` — Returns `{ id, name, price }` list for UI dropdowns.

**Dashboard**
- `GET /dashboard/summary?startDate=&endDate=&status=` — Aggregate metrics (revenue, discounts, order counts).
- `GET /dashboard/revenue-over-time?startDate=&endDate=&status=` — Daily revenue data points.
- `GET /dashboard/top-products?startDate=&endDate=&status=&limit=10`
- `GET /dashboard/top-clients?startDate=&endDate=&status=&limit=10`
- `GET /dashboard/export?from=&to=&status=&format=csv|pdf` — Export via QuestPDF (PDF) or custom CSV writer. Excludes canceled items.

Dashboard repository queries use `AsNoTracking()`. Canceled orders are always counted separately regardless of status filter.
