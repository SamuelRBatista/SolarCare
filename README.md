# SolarCare

SolarCare is a SaaS platform for post-sale operations, technical support, and maintenance in the solar energy sector.

## Current status

This repository contains a .NET 10 modular solution, a React/TypeScript frontend, a local PostgreSQL service, tenant-scoped authentication, and initial operations APIs for the MVP. The authenticated frontend includes a live operations dashboard and customer management with search, pagination, create, update, and deactivation.

## Prerequisites

- .NET 10 SDK
- Node.js 20.19+ or 22.12+
- Docker Desktop with Docker Compose

## Run locally

1. Copy `.env.example` to `.env`. The example password is for local development only.
2. Start PostgreSQL:

   ```powershell
   docker compose up -d postgres
   ```

3. Configure local API secrets in a second PowerShell terminal. Use a unique random signing key; do not commit it:

   ```powershell
   $env:ConnectionStrings__SolarCare = "Host=localhost;Port=5432;Database=solarcare;Username=solarcare;Password=solarcare_local_dev_only"
   $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
   $key = New-Object byte[] 48
   $rng.GetBytes($key)
   $env:Jwt__SigningKey = [Convert]::ToBase64String($key)
   $personalDataKey = New-Object byte[] 32
   $rng.GetBytes($personalDataKey)
   $env:PersonalData__EncryptionKey = [Convert]::ToBase64String($personalDataKey)
   ```

4. Apply the versioned schema in that same terminal:

   ```powershell
   dotnet run --project backend\tools\SolarCare.DbMigrator
   ```

5. Start the API in that same terminal:

   ```powershell
   dotnet run --project backend\src\SolarCare.Api
   ```

6. Install and start the frontend in another terminal:

   ```powershell
   Set-Location frontend
   npm install
   npm run dev
   ```

7. Open the Vite URL printed in the terminal. The API liveness endpoint is available at `http://localhost:5080/api/health/live`.

The Compose password is only an example for local development. Use secret management and unique credentials outside local development. Migrations are explicit, ordered, checksummed, and run by `SolarCare.DbMigrator`; changed migrations are rejected. The migrator refuses to modify an existing untracked authentication schema. Back up any database before migration.

Authentication provides company registration (with the first CompanyAdmin), login, session refresh, logout, and current-user lookup. The access JWT expires after 15 minutes. Its signing key must be a base64-encoded random key of at least 32 bytes. Refresh tokens are opaque, rotated on use, stored as SHA-256 hashes, and delivered only through an HttpOnly, SameSite=Strict cookie. Authentication endpoints have an IP-based rate limit.

Customer CPF/CNPJ is encrypted with AES-256-GCM and separately keyed for tenant-local duplicate detection. Keep `PersonalData__EncryptionKey` stable and backed up in secret management; losing or changing it makes stored CPF/CNPJ unreadable. Password recovery, invitations, user administration, and email delivery are not implemented; no recovery or invitation flow is simulated.

## Backend MVP API

All operational routes require a bearer access token. Tenant IDs are derived from the signed token, and relational foreign keys also require related records to share the same tenant.

- `POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout`, `GET /api/auth/me`
- `GET /api/dashboard/summary`
- `GET|POST /api/customers`, `GET|PUT|DELETE /api/customers/{id}`
- `GET|POST /api/installations`, `GET|PUT|DELETE /api/installations/{id}`
- `GET|POST /api/equipment`, `GET|PUT|DELETE /api/equipment/{id}`
- `GET|POST /api/tickets`, `GET|PUT /api/tickets/{id}`, `PATCH /api/tickets/{id}/status`, `GET /api/tickets/{id}/history`
- `GET|POST /api/service-orders`, `GET|PUT /api/service-orders/{id}`, `PATCH /api/service-orders/{id}/status`, `GET /api/service-orders/{id}/history`, `POST /api/service-orders/{id}/complete`
- `GET|POST /api/maintenance-plans`, `GET|PUT|DELETE /api/maintenance-plans/{id}`, `POST /api/maintenance-plans/{id}/execute`, `GET /api/maintenance-plans/{id}/history`

List routes support `limit` (1–100) and `offset`. Request/response examples and a versioned public API contract are still pending.

The web frontend includes customer, installation, ticket, service-order, and maintenance-plan workflows. Installations and maintenance plans support soft deactivation. Tickets and service orders retain their audit history; instead of hard deletion, users can cancel them through their status workflows. Ticket details can be edited while a ticket is not closed or cancelled. Service-order edits are limited to pending, scheduled, and in-progress orders.

The current backend scope does not include contracts, CRM/pipeline, notifications, subscriptions or payment processing, attachments, external integrations, password recovery, invitations, or user administration. These require additional product and provider decisions before implementation.

## Demo data (optional)

For local testing, `backend/database/seed_demo.sql` adds clearly identified fictitious customers, installations, equipment, tickets, service orders, and maintenance plans to the first active tenant. It is safe to run again and does not change existing records. Apply it only to a development database with `psql -v ON_ERROR_STOP=1 -f backend\database\seed_demo.sql`.

## Verify

From the repository root:

```powershell
dotnet test backend\SolarCare.slnx
dotnet build backend\SolarCare.slnx
Set-Location frontend
npm run build
npm run lint
```

## Solution layout

- `backend/src/SolarCare.Api` — HTTP API and composition root.
- `backend/src/SolarCare.Application` — use cases and application contracts.
- `backend/src/SolarCare.Domain` — business rules and domain types.
- `backend/src/SolarCare.Infrastructure` — Dapper and PostgreSQL integrations.
- `backend/tests/SolarCare.Tests` — automated tests.
- `backend/tools/SolarCare.DbMigrator` — database migration runner.
- `backend/database/migrations` — versioned PostgreSQL migrations.
- `frontend` — React and TypeScript application.

See [ARCHITECTURE.md](./ARCHITECTURE.md) for the initial architecture decisions and open questions.
