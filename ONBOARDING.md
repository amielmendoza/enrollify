# Enrollify — Team Onboarding

A shared knowledge base for getting productive on Enrollify, a **multi-tenant K-12 school enrollment system**. One deployment serves many schools (tenants); each school's data is isolated automatically. .NET 8 backend (Clean Architecture + CQRS) and an Angular 18 frontend.

> Open this in Claude Code and ask it to walk you through any section, run setup, or point you to the right file.

---

## 1. Get it running

### Prerequisites
- **.NET 8 SDK** (the SDK 9 toolchain also builds the `net8.0` target).
- **Node.js 20+** and npm.
- **SQL Server** reachable at `localhost` with Windows auth (the default connection string uses `Trusted_Connection=true`). SQL Server Express / LocalDB / Developer all work.

### Backend (`backend/`)
```bash
dotnet build Enrollify.sln
dotnet run --project src/Enrollify.API
```
- Serves **http://localhost:5221** (Swagger at `/swagger` in Development). HTTPS profile is https://localhost:7237.
- **No manual DB setup needed.** On startup the API auto-applies EF migrations and seeds demo data (`ApplicationDbContextSeed`). Just point the connection string at a reachable SQL Server.

### Frontend (`enrollify.client/`)
```bash
npm install
npm start        # ng serve → http://localhost:4200, expects the API at :5221
npm run build
npm test         # Karma + Jasmine
```

### Demo logins (seeded)
Tenant **"mshs"**, id `a1b2c3d4-e5f6-7890-abcd-ef1234567890`:
- `admin@mshs.edu.ph` / `Admin123!`
- `registrar@mshs.edu.ph` / `Registrar123!`

### Local config
Connection string + JWT settings live in `backend/src/Enrollify.API/appsettings.json` (default DB: `Server=localhost;Database=EnrollifyDb;Trusted_Connection=true`).

---

## 2. The one thing to understand first: multi-tenancy

**Tenant isolation is enforced automatically — not per query.** Read this before touching any data access.

- `ApplicationDbContext.OnModelCreating` applies a **global query filter** to every `TenantEntity`, so all reads are scoped to the current tenant.
- `SaveChangesAsync` auto-assigns `TenantId` on new `TenantEntity` rows (unless set explicitly) and maintains `CreatedAt` / `UpdatedAt`.
- The current tenant comes from `ITenantProvider` (scoped per request), set by `TenantMiddleware` from: (1) the JWT `TenantId` claim, then (2) the `X-Tenant-Id` header.
- `TenantMiddleware` exempts some path prefixes from requiring a tenant: `/api/auth` (login), `/api/tenants` (cross-tenant management), and `/api/schools/{slug}/...` (public slug-based endpoints where the tenant is in the path). `SuperAdmin` operates across tenants.

**Rules of thumb**
- ❌ Do **not** add manual `where TenantId == ...` clauses to normal queries — the filter already does it.
- ✅ To intentionally bypass the filter (e.g. verifying a tenant exists), use `.IgnoreQueryFilters()`.
- Entities that should be tenant-scoped must derive from `TenantEntity` (which adds `TenantId`); otherwise derive from `BaseEntity`.

---

## 3. Backend architecture (Clean Architecture + CQRS)

Dependencies flow inward: `API → Application → Domain`, with `Infrastructure → Application/Domain`. The API references Application and Infrastructure only for DI wiring.

| Layer | Project | What lives here |
|-------|---------|-----------------|
| **Domain** | `Enrollify.Domain` | Entities, enums. No dependencies. Entities derive from `BaseEntity` (Id, audit fields) or `TenantEntity` (adds `TenantId`). |
| **Application** | `Enrollify.Application` | CQRS handlers via MediatR under `Features/<Area>/Commands` and `Features/<Area>/Queries`. DTOs in `DTOs/`. Handlers depend only on `IApplicationDbContext` (the interface). FluentValidation validators run automatically via the `ValidationBehavior` pipeline. |
| **Infrastructure** | `Enrollify.Infrastructure` | EF Core `ApplicationDbContext`, entity configs (`Persistence/Configurations`), migrations, JWT generation, `TenantProvider`, `CurrentUserService`. |
| **API** | `Enrollify.API` | Thin controllers that inject `ISender` and dispatch MediatR commands/queries. Almost no logic in controllers. |

**The pattern:** each command/query is a `record` implementing `IRequest<T>`, paired with its handler in the same file. Controllers translate HTTP → command/query → result.

### How to add an endpoint (the golden path)
1. Add a `record` command/query + handler in `Application/Features/<Area>/Commands|Queries`.
2. Add/extend a DTO in `Application/DTOs/<Area>` if needed.
3. Add a FluentValidation validator if the input needs guarding (runs automatically).
4. Add a thin controller action that injects `ISender` and sends the request.

### Roles
`UserRole`: `Admin`, `Registrar`, `Parent`, `Student`, `SuperAdmin`. `SuperAdmin` lives outside any tenant and manages the schools themselves.

### Enrollment workflow
`EnrollmentStatus` progresses **Draft → Submitted → Assessed → Approved → Paid → Enrolled**. Transitions are guarded in handlers (e.g. `SubmitEnrollmentCommand` rejects non-Draft and requires all requirements uploaded) and recorded in `EnrollmentStatusHistory`. There is also a configurable `WorkflowDefinition` / `WorkflowStep` model per tenant.

### Database migrations
Run from `backend/` (the API project is the startup project):
```bash
# Add
dotnet ef migrations add <Name> --project src/Enrollify.Infrastructure --startup-project src/Enrollify.API
# Apply (usually unnecessary — the API applies migrations on startup)
dotnet ef database update --project src/Enrollify.Infrastructure --startup-project src/Enrollify.API
```

---

## 4. Frontend architecture (Angular 18)

- **Standalone components** with lazy-loaded routes (`src/app/app.routes.ts`). Authenticated routes sit under a shell guarded by `authGuard`; role-specific areas: `parent/*`, `student` (`my-*`), `super/*`.
- All HTTP goes through **`ApiService`** (`core/services/api.service.ts`); base URL comes from `environments/`.
- **`authInterceptor`** attaches the JWT `Authorization` header and an `X-Tenant-Id` header (from `AuthService`) — but only sets `X-Tenant-Id` when the caller hasn't already. Public `/apply` flows pass the tenant from the route slug instead.
- Styling: **Tailwind CSS**.

---

## 5. Conventions & gotchas

- **Don't filter by tenant manually** (see §2). The single biggest source of subtle bugs for newcomers.
- **Controllers stay thin** — business logic belongs in MediatR handlers, not controllers.
- **Validation is automatic** — add a FluentValidation validator rather than hand-rolling checks in the handler.
- **Status transitions are guarded** — change the status state machine in the handler, and append to `EnrollmentStatusHistory`.
- **Migrations apply on startup** — you usually only need `migrations add`, not `database update`.
- **There is no backend test project.** Frontend tests use Karma + Jasmine (`npm test`); run a single spec with `fdescribe`/`fit` or by narrowing the `include` glob in `angular.json`.

---

## 6. Where things live (quick map)

```
backend/
  src/Enrollify.Domain/          Entities/, Enums/, Common/BaseEntity.cs
  src/Enrollify.Application/      Features/<Area>/{Commands,Queries}, DTOs/, Common/Behaviors
  src/Enrollify.Infrastructure/   Persistence/ (DbContext, Configurations, Migrations), Identity/, Services/
  src/Enrollify.API/             Controllers/, Middleware/, Program.cs, appsettings.json
enrollify.client/
  src/app/core/                  services/ (ApiService, AuthService), guards/, interceptors/, models/
  src/app/pages/                 feature pages (enrollments, admissions, parent, student, super-tenants, ...)
  src/environments/              apiUrl + defaultTenantId per environment
CLAUDE.md                        instructions for Claude Code working in this repo
```

---

*Working in this repo with Claude Code? `CLAUDE.md` holds the same architecture facts in a form Claude reads automatically. Keep both in sync when conventions change.*
