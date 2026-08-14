# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Enrollify is a multi-tenant K-12 school enrollment system. A single deployment serves many schools (tenants); each school's data is isolated. It has a .NET 8 backend (`backend/`) using Clean Architecture + CQRS, and an Angular 18 standalone-component frontend (`enrollify.client/`).

## Commands

### Backend (`backend/`)
- Build: `dotnet build Enrollify.sln`
- Run API: `dotnet run --project src/Enrollify.API` — serves http://localhost:5221 (Swagger at `/swagger` in Development). HTTPS profile is https://localhost:7237.
- On startup the API auto-applies EF migrations and seeds demo data (`ApplicationDbContextSeed`). No manual DB setup is needed beyond a reachable SQL Server.
- EF migrations (run from `backend/`, the API project is the startup project):
  - Add: `dotnet ef migrations add <Name> --project src/Enrollify.Infrastructure --startup-project src/Enrollify.API`
  - Apply: `dotnet ef database update --project src/Enrollify.Infrastructure --startup-project src/Enrollify.API`
- There is no test project in this repo.

### Frontend (`enrollify.client/`)
- Install: `npm install`
- Dev server: `npm start` (`ng serve`) — http://localhost:4200, expects the API at http://localhost:5221.
- Build: `npm run build`
- Tests: `npm test` (Karma + Jasmine). Run a single spec by temporarily narrowing the `include` glob in `angular.json` or using `fdescribe`/`fit`.

### Local config
- Connection string and JWT settings live in `src/Enrollify.API/appsettings.json` (default DB: `Server=localhost;Database=EnrollifyDb;Trusted_Connection=true`).
- Seeded login (tenant "mshs", id `a1b2c3d4-e5f6-7890-abcd-ef1234567890`): `admin@mshs.edu.ph` / `Admin123!`, `registrar@mshs.edu.ph` / `Registrar123!`.

## Architecture

### Backend layers (Clean Architecture)
Dependencies flow inward: `API → Application → Domain`, with `Infrastructure → Application/Domain`. The API references both Application and Infrastructure only for DI wiring.
- **Domain** (`Enrollify.Domain`) — entities, enums, no dependencies. Entities derive from `BaseEntity` (Id, audit fields) or `TenantEntity` (adds `TenantId`).
- **Application** (`Enrollify.Application`) — CQRS handlers via MediatR, organized as `Features/<Area>/Commands` and `Features/<Area>/Queries`. DTOs in `DTOs/`. Handlers depend only on `IApplicationDbContext` (the interface, not the concrete DbContext). FluentValidation validators run automatically via `ValidationBehavior` MediatR pipeline.
- **Infrastructure** (`Enrollify.Infrastructure`) — EF Core `ApplicationDbContext`, entity configurations (`Persistence/Configurations`), migrations, JWT generation, `TenantProvider`, `CurrentUserService`.
- **API** (`Enrollify.API`) — thin controllers that inject `ISender` and dispatch MediatR commands/queries. Almost no logic lives in controllers.

The CQRS pattern: each command/query is a `record` implementing `IRequest<T>`, paired with a handler in the same file. Controllers translate HTTP requests into these and return the result. Follow this when adding endpoints — add a Feature command/query, then a thin controller action.

### Multi-tenancy (critical — read before touching data access)
Tenant isolation is enforced automatically, not per-query:
- `ApplicationDbContext.OnModelCreating` applies a **global query filter** to every `TenantEntity` so all reads are scoped to the current tenant.
- `SaveChangesAsync` auto-assigns `TenantId` on new `TenantEntity` rows (unless already set explicitly) and maintains `CreatedAt`/`UpdatedAt`.
- The current tenant comes from `ITenantProvider` (scoped per request), set by `TenantMiddleware` from (1) the JWT `TenantId` claim, then (2) the `X-Tenant-Id` header.
- `TenantMiddleware` exempts certain path prefixes from requiring a tenant: `/api/auth` (login), `/api/tenants` (cross-tenant tenant management), and `/api/schools/{slug}/...` (public slug-based endpoints where the tenant is in the path). `SuperAdmin` operates across tenants.
- To bypass the filter intentionally (e.g. verifying a tenant exists), use `.IgnoreQueryFilters()`. Do **not** add manual `where TenantId == ...` clauses to normal queries — the filter already does it.

### Roles
`UserRole` enum: `Admin`, `Registrar`, `Parent`, `Student`, `SuperAdmin`. `SuperAdmin` lives outside any tenant and manages the schools themselves.

### Enrollment workflow
`EnrollmentStatus` progresses `Draft → Submitted → Assessed → Approved → Paid → Enrolled`. Status transitions are guarded in handlers (e.g. `SubmitEnrollmentCommand` rejects non-Draft and requires all requirements uploaded) and recorded in `EnrollmentStatusHistory`. There is also a configurable `WorkflowDefinition`/`WorkflowStep` model per tenant.

### Frontend
- Angular 18 standalone components with lazy-loaded routes (`src/app/app.routes.ts`). Authenticated routes sit under a shell guarded by `authGuard`; role-specific areas: `parent/*`, `student` (`my-*`), `super/*`.
- All HTTP goes through `ApiService` (`core/services/api.service.ts`); base URL from `environments/`.
- `authInterceptor` attaches the JWT `Authorization` header and an `X-Tenant-Id` header (from `AuthService`), but only sets `X-Tenant-Id` when the caller hasn't already — public `/apply` flows pass the tenant from the route slug. Styling uses Tailwind CSS.
