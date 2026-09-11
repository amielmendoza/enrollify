# Deploying Enrollify

## Backend (Enrollify.API)

### Required configuration

`appsettings.Production.json` ships with **empty placeholders**. The API **fails fast at
startup** in the Production environment when any of these is missing or left at a dev value:

| Setting | Env-var override | Notes |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | Real SQL Server. Values containing `localhost` are rejected in Production. |
| `Jwt:Key` | `Jwt__Key` | Strong secret (256-bit+). The committed dev key is rejected in Production. |

Recommended (no fail-fast, but needed for a working deployment):

| Setting | Env-var override | Notes |
|---|---|---|
| `AllowedOrigins` | `AllowedOrigins__0` (`__1`, ...) | Exact origins of the deployed SPA (e.g. `https://app.example.edu.ph`). Defaults to `http://localhost:4200` when unset. |
| `Email:*` | `Email__Enabled`, `Email__Host`, `Email__Port`, `Email__UserName`, `Email__Password`, `Email__FromAddress`, `Email__FromName` | SMTP relay. With `Email__Enabled=false` (default) the app runs fine; sends are logged and skipped. |

Environment variables use the double-underscore (`__`) separator for nesting and always win
over the JSON files.

### Health endpoint

`GET /health` — anonymous, no tenant header required. Returns `Healthy` only when the
database answers. Point load-balancer/deploy-script probes here.

### Migrations run at boot — single instance only

On startup the API applies EF Core migrations (`Database.MigrateAsync()`) and seeds
baseline data. Consequences:

- **Run exactly one instance during a deploy.** Two instances booting simultaneously can
  race the same migration. Scale out only after one instance is up and `/health` is green.
- The database login used by the connection string needs DDL rights (or pre-apply
  migrations with `dotnet ef database update` and deploy with an already-migrated DB).

### Back up before every deploy

Migrations are applied automatically and are not automatically reversible. **Take a SQL
Server backup (or snapshot) immediately before deploying a new build.**

### Deploy steps (summary)

1. Back up the production database.
2. Stop the running API instance.
3. Publish: `dotnet publish backend/src/Enrollify.API -c Release`.
4. Set the environment: `ASPNETCORE_ENVIRONMENT=Production` plus the variables above.
5. Start ONE instance; wait for `/health` to return `Healthy` (migrations run during boot).
6. Scale out if needed.

## Frontend (enrollify.client)

Build with the production configuration so the production `environment` file (real API base
URL) is swapped in — the UI developer wires the actual values:

```
npm ci
npm run build -- --configuration production
```

Serve the build output (`dist/`) from static hosting/CDN, and make sure the site's origin is
listed in the API's `AllowedOrigins`.
