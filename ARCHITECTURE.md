# Initial architecture

## Approach

Start with a modular monolith: one ASP.NET Core API, a React web application, and PostgreSQL. This keeps local deployment and operational overhead low while preserving boundaries between domain rules, use cases, HTTP delivery, and infrastructure. Split services only when measured scale or independent deployment needs justify it.

## Projects

- `SolarCare.Domain` owns business concepts and invariants.
- `SolarCare.Application` owns use cases, validation, and ports.
- `SolarCare.Infrastructure` implements persistence and external adapters with Dapper and Npgsql.
- `SolarCare.Api` owns HTTP endpoints and application composition.
- `SolarCare.Tests` holds automated tests.

Entity Framework Core is intentionally not used.

## Current backend boundaries

The API controllers are split by HTTP resource. Persistence contracts in the Application project are also split by capability (customers, installations, equipment, tickets, service orders, maintenance, and dashboard) so each controller depends on a narrower port. Infrastructure currently implements these ports through a shared Dapper repository; the aggregate `IOperationsRepository` is retained temporarily as a compatibility composition during migration. The .NET solution, its projects, tests, migration runner, and SQL migrations are grouped under `backend/`.

Application request/response contracts are grouped by module under `SolarCare.Application/Operations`. These transport and use-case models belong to Application, not Domain. Domain entities and value objects should be introduced where they can own actual business invariants, rather than as data-only mirrors of database tables.

The Domain currently models ticket and service-order lifecycles as aggregates with typed statuses and guarded transitions. Persistence maps the existing database/API status strings to these domain types, preserving the external contract while centralizing lifecycle rules.

This is an incremental transition, not a claim that the backend already fully implements Clean Architecture. The next steps are to move request validation and orchestration into Application use cases, remove the temporary aggregate persistence contract, and split the Dapper implementation by capability.

## Frontend boundaries

The React entry component (`frontend/src/App.tsx`) acts as the composition root: it selects the active feature page and connects authentication, shared layout, and cross-feature refresh signals. Authentication, customer, and operation state/data effects are encapsulated in feature hooks (`useAuth`, `useCustomers`, and `useOperations`); their views consume the resulting state and actions. Feature screens live under `features`, reusable UI components under `components`, shared HTTP/session helpers under `shared`, and the authenticated navigation shell under `layout`.

The frontend is being modularized incrementally rather than treated as a literal copy of the backend's layers. Feature-specific data access remains close to its feature for now; the next step is to separate feature API adapters and view state from page presentation, including splitting the operational module page into focused feature components.

## Tenant isolation

Tenant identity must be derived from authenticated server-side identity, never trusted from a freely supplied request field. Application queries must scope access by tenant. Before persistence is introduced, the team must decide how authenticated tenant context is propagated to repositories and whether PostgreSQL Row Level Security is warranted in addition to application checks.

Tenant isolation tests are a release gate for every tenant-owned data access path.

The initial authentication flow assigns each user to one tenant. Company registration atomically creates the tenant and its initial CompanyAdmin. Access tokens carry a server-issued `tenant_id` claim. Access tokens are short-lived JWTs; refresh tokens are random opaque values, rotated on use, hashed before persistence, and sent in an HttpOnly cookie. The API requires a separately supplied JWT signing key and database connection string; neither has an insecure application default.

`ITenantContext` reads the tenant, user, and role from the authenticated HTTP principal and rejects missing or invalid claims. Tenant-owned data access should depend on this context rather than request-supplied tenant identifiers. Password recovery, invitations, and user administration are deferred until an email and account-management flow is designed.

## Data model status

The versioned schema includes tenants, users, refresh tokens, customers, installations, equipment, tickets and ticket history, service orders, maintenance plans, and maintenance records. Tenant-aware composite foreign keys prevent cross-tenant references at the database layer. CPF/CNPJ is stored encrypted and indexed using a keyed lookup digest.

Subscription, contract, sales opportunity, notification, attachments, and payment tables are not part of this MVP schema.

## Operational boundaries

In production, HTTPS terminates at the configured reverse proxy. Secrets must be supplied by the deployment environment, not committed to source control. The local Compose file is for development and is not a production deployment configuration.
