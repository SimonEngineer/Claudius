# Claudius — 3D Name Tag Generator

Generate printable name tags from a list of names. Pick a shape (rectangle,
rounded rectangle, oval, circle, heart, star, plaque, or a custom uploaded
SVG outline), and each name is extruded as embossed text on top of that
shape and exported as a binary STL ready to slice and print.

The mesh is always generated server-side and is never persisted — only the
generation parameters (shape, dimensions, font, text depth, names) are
saved, so editing a saved project and re-opening it always regenerates a
fresh, up-to-date model.

## Architecture

- **`backend/NameTags.Core`** — pure mesh pipeline (no ASP.NET dependency):
  glyph/SVG outline extraction (SkiaSharp + Svg.Skia), polygon triangulation
  (LibTessDotNet), extrusion, plate+text combination, and a hand-written
  binary STL writer.
- **`backend/NameTags.Api`** — ASP.NET Core Web API exposing inline
  generation (`/api/models`) and persisted-project CRUD (`/api/projects`),
  both backed by the same `ModelGenerationService`.
- **`backend/NameTags.Data`** — EF Core + SQLite persistence for
  `TagProject` (shared shape/plate/font params) and `TagName` (per-name
  text, sort order, optional per-name text-depth override).
- **`frontend`** — React + Vite + Tailwind + hand-rolled shadcn/ui
  components, with `@react-three/fiber`/`drei` + three's `STLLoader` for
  live in-browser previews of the generated STL.

## Running locally

### Backend

```bash
cd backend/NameTags.Api
dotnet run
```

Listens on `http://localhost:5050` by default (see `Properties/launchSettings.json`).
On first run it creates `nametags.db` (SQLite) and applies EF Core migrations
automatically at startup.

### Frontend

```bash
cd frontend
npm install
npm run dev
```

Set `VITE_API_BASE_URL` if the backend isn't running at `http://localhost:5050`.

## Features

- Shape presets + custom SVG outline upload, with shape-specific knobs
  (corner radius, star points/inner radius, curve segment count).
- Live, debounced STL preview per name with auto-fit text sizing, before
  ever saving anything.
- Save/load/delete named projects; editing a saved project and re-fetching
  its preview/download always reflects current params (no stale cached mesh).
- Per-name text-depth override on top of a project's shared depth.
- "Download all as .zip" for a saved project's full name list.
- Basic input validation (non-empty name text, 40-character max, SVG
  parse-validation on upload) surfaced as inline errors in the UI.

## Known limitations

- No headless browser is available in the sandbox this was built in, so UI
  rendering/interaction was verified via `tsc`/`vite build` and manual API
  contract testing (curl) rather than an automated or visual browser check.
- One STL per name — no multi-name plate nesting/packing.
- Text-plate combination is overlap-based (small z-overlap, not a true CSG
  boolean), which is print-safe for FDM but not a literal subtractive engrave.
