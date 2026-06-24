# Wanderlist — Trip Planner & Wishlist

A self-hosted app for tracking places you want to go, ambitions you want to complete
("visit all continents", "see every MJ statue"), and concrete trips with stops,
bookings, packing lists, a journal, and budget tracking.

- **Backend:** .NET 8 Web API + EF Core + PostgreSQL
- **Frontend:** React 18 + TypeScript + Vite + Tailwind, talking to the API over `/api`

See [`docs/DESIGN.md`](docs/DESIGN.md) for the full domain model and feature rationale.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/) and npm
- A running [PostgreSQL](https://www.postgresql.org/) instance

## 1. Database

Create a database and user matching the connection string in
`backend/src/TripPlanner.Api/appsettings.json` (or override it, see below):

```bash
psql -U postgres -c "CREATE USER tripplanner WITH PASSWORD 'tripplanner';"
psql -U postgres -c "CREATE DATABASE tripplanner OWNER tripplanner;"
```

Apply the EF Core migrations:

```bash
cd backend/src/TripPlanner.Api
dotnet tool install --global dotnet-ef   # first time only
dotnet ef database update
```

To point at a different database (e.g. a managed Postgres instance), set the
`ConnectionStrings__Default` environment variable instead of editing
`appsettings.json`:

```bash
export ConnectionStrings__Default="Host=...;Port=5432;Database=...;Username=...;Password=..."
```

## 2. Run the backend

```bash
cd backend/src/TripPlanner.Api
dotnet run
```

The API listens on `http://localhost:5230` and serves uploaded media from
`/uploads`. Swagger UI is available at `http://localhost:5230/swagger` in
Development mode.

## 3. Run the frontend

```bash
cd frontend
npm install
npm run dev
```

Open `http://localhost:5173`. The Vite dev server proxies `/api` and
`/uploads` to `http://localhost:5230`, so the backend must already be running
(see `frontend/vite.config.ts` if you need to change the backend port/host).

## Building for production

```bash
# Backend
cd backend/src/TripPlanner.Api
dotnet publish -c Release -o ./publish

# Frontend
cd frontend
npm run build   # outputs to frontend/dist
```

Serve `frontend/dist` with any static host (or point the API's static file
middleware at it) and run the published backend behind a reverse proxy of
your choice, with `ConnectionStrings__Default` and CORS origins configured
for your production domain.

## Using the app

- **Dashboard** — overview cards plus a "next trip" countdown.
- **Wishlist** — places you want to visit. Each location supports notes,
  links, a freeform plan, a whiteboard for brainstorming, tags, and a status
  (Idea → Planned → Booked → Visited).
- **Goals** — build your own checklist-style ambition (e.g. "visit all
  continents"): define custom fields per item (text/number/date/url/boolean/
  location), check items off, and attach notes/links/photos to each. A goal
  can also just link to a Trip instead of having its own items.
- **Trips** — a dated trip with map-pinned stops (drag to reorder), bookings
  (flights/hotels/cars/tickets) with type-specific fields, a generated
  day-by-day itinerary, a packing checklist, a Budget tab (target vs. actual
  spend by category), and a Journal timeline for photos/videos/notes/links
  captured (with geolocation) while traveling — toggle "On the trip?" for a
  simplified mobile-first journaling view.
- **Tags** — shared across Wishlist/Goals/Trips; rename, recolor, or delete a
  tag (with a usage count) from the Tags page.

## Project layout

```
backend/
  src/TripPlanner.Domain/         Entities, enums
  src/TripPlanner.Infrastructure/ EF Core DbContext + migrations
  src/TripPlanner.Api/            Controllers, DTOs, Program.cs
frontend/
  src/pages/                      One component per route
  src/components/                 Shared UI (layout, map, shadcn-style primitives)
  src/api/                        Axios client + per-resource API methods
  src/types/                      Shared TS types/enums mirroring the backend
docs/DESIGN.md                    Domain model and feature design notes
```
