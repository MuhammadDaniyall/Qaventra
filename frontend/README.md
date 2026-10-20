# HRMS — Frontend

React 19 + TypeScript + Vite + Tailwind CSS v4 frontend for the multi-tenant HRMS.

## Run

Requires Node 20.19+ (Node 24 recommended). The backend API must be running on http://localhost:5080
(`dotnet run` in `backend/src/HRMS.Api`, `http` profile).

```bash
npm install
npm run dev      # http://localhost:5173, /api is proxied to http://localhost:5080
```

Other scripts:

```bash
npm run build    # type-check (tsc -b) and production build into dist/
npm run preview  # serve the production build locally
```

## Layout

- `src/api/` – fetch client (bearer token, ProblemDetails errors) and typed calls per area
- `src/types/` – TypeScript mirrors of the backend DTOs
- `src/auth/` – session context, role helpers and route guards
- `src/components/ui/` – small design-system components; `src/components/layout/` – app shell
- `src/pages/<area>/` – screens; `src/lib/` – formatting, theme, geolocation helpers
