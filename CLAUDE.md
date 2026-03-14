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

Migrations are also applied automatically on startup via `dbContext.Database.MigrateAsync()` in `Program.cs`.

## Architecture

Clean Architecture with four projects:

- **DeuxOrders.Domain** — Entities, enums, repository interfaces (`IOrderRepository`, `IClientRepository`, etc.), `IUnitOfWork`, and shared models (`PagedResult<T>`). No external dependencies.
- **DeuxOrders.Application** — Business logic (`OrderService`), DTOs, and entity-to-DTO mapping. Depends only on Domain.
- **DeuxOrders.Infrastructure** — EF Core `ApplicationDbContext`, repository implementations, migrations, and `TokenService` (JWT generation). Depends on Domain and Application.
- **DeuxOrders.API** — ASP.NET Core controllers, FluentValidation validators, `GlobalExceptionHandler` middleware, and `Program.cs` DI wiring.

**Request flow:** Controller → Service (Application) → Repository (Infrastructure) → EF Core → PostgreSQL

**Key design decisions:**
- All data access goes through repository interfaces defined in Domain.
- Services (Application layer) contain business rules; controllers are thin.
- `GlobalExceptionHandler` middleware centralizes error responses — do not add try/catch in controllers.
- FluentValidation validators in `DeuxOrders.API/Validations/` are registered via `AddFluentValidationAutoValidation()` and run automatically before controllers are hit.
- CORS allows `localhost:3000` and `orders.deuxcerie.com.br`.

## Testing

Tests live in `DeuxOrders.Tests` and use xUnit + FluentAssertions. Integration tests use an in-memory database via `IntegrationTestFactory`. There are no unit test mocks for the database — tests run against an in-memory EF Core provider.

## API Routes

All routes are prefixed with `/api/v1/`:

| Controller | Endpoints |
|---|---|
| `AuthController` | `POST /auth/register`, `POST /auth/login` |
| `OrderController` | `GET/POST /orders`, `GET/PUT/DELETE /orders/{id}`, `PATCH /orders/{id}/status` |
| `ClientController` | `GET/POST /clients`, `GET/PUT /clients/{id}`, `PATCH /clients/{id}/status` |
| `ProductController` | `GET/POST /products`, `GET/PUT /products/{id}`, `PATCH /products/{id}/status` |

All endpoints except auth require a JWT Bearer token.
