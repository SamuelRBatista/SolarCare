# SolarCare frontend

The React and TypeScript application provides the authenticated operations dashboard and customer-management screens. It communicates with the ASP.NET Core API through the `/api` Vite development proxy.

## Run locally

From this directory:

```powershell
npm install
npm run dev
```

The API must be running at `http://localhost:5080`; see the repository [README](../README.md) for the full local setup, including PostgreSQL and migrations.

## Verify

```powershell
npm run build
npm run lint
```
