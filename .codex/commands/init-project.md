# init-project

Initialize the DeuxERP backend for local development.

## Steps

1. Start the PostgreSQL container.

```bash
docker-compose up -d
```

2. Ensure the API project has local secrets configured.

The API project already declares a `UserSecretsId` in `DeuxERP.API/DeuxERP.API.csproj`, so `dotnet user-secrets init` is not required unless that value is removed.

Use the local development values from `.env` or your own safe equivalents:

```bash
dotnet user-secrets set "JwtSettings:Secret" "<32+ char secret>"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=deux_order_management_db;Username=admin_test;Password=admin_test_pwd"
```

3. Restore packages.

```bash
dotnet restore
```

4. Start the API.

```bash
dotnet run --project DeuxERP.API
```

The application applies migrations on startup via `context.Database.Migrate()`, so this also initializes the database schema.

## Notes

- The repository also includes `CLAUDE.md` with the same local setup flow.
- If you are using a remote PostgreSQL database, replace the connection string above with the appropriate value.
