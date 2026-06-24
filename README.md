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
  binary STL writer. `Pipeline/WineGlassCharmBuilder` and
  `Pipeline/ClothesClipBuilder` build the two pack accessory variants;
  `Pipeline/PackGenerationService` lays out standing tag + charm + clip
  side by side into one combined mesh.
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
  (corner radius, star points/inner radius, curve segment count). The SVG
  parser walks `<path>/<rect>/<circle>/<ellipse>/<polygon>/<polyline>`
  elements and composes nested `<g transform="...">`/element `transform`
  attributes, so real-world exported SVGs (not just bare single-path files)
  work.
- Live, debounced STL preview per name with auto-fit text sizing, before
  ever saving anything.
- Independent per-side text margins (left/right/top/bottom) and
  horizontal/vertical text alignment within those margins.
- Optional bevel/chamfer on the plate's top edge (skipped for custom SVG
  outlines, since re-fitting raw SVG geometry to a smaller inset doesn't
  preserve point-count topology).
- Mounting holes: add any number of circular holes at arbitrary offsets,
  punched straight through the plate.
- Text is guaranteed to never exceed the plate's actual silhouette (not just
  its bounding box) — every text-contour vertex is containment-tested
  against the outer outline and every hole, and text is shrunk about its
  alignment anchor if needed. This matters most for non-rectangular shapes
  (Heart, Star, Oval, Plaque, CustomSvg) where bounding-box fitting alone
  can let corners poke outside a curved or pointed edge.
- Save/load/delete named projects; editing a saved project and re-fetching
  its preview/download always reflects current params (no stale cached mesh).
- Per-name text-depth override on top of a project's shared depth.
- **Wine-glass charm + clothes clip "packs"**: besides the normal standing
  table tag, each name can also be exported as a small charm with an open
  hook (sized to hang from a wine glass stem, name reading horizontally) or
  as a chest tag with a printed alligator-clip spring fused to its back
  edge. A "pack" download (`pack.stl`) combines all three variants —
  standing tag, charm, clip — pre-arranged side by side flat on the bed, so
  it drops straight into a slicer with nothing left to rearrange. The
  charm's hook radius/band thickness and the clip's plate size/arm
  length/gap/arm thickness are all per-project settings (`PackSettingsPanel`
  in the editor), so a project can be tuned to fit a particular glass stem
  or fabric thickness. The editor's per-name preview card can toggle
  between the standing tag and the full pack live, in 3D, before downloading.
- "Download all as .zip" and "Download all packs (.zip)" stream a project's
  full name list (or every pack) as a zip directly from the server without
  buffering the archive or any individual mesh in memory — built for
  ~100-name projects, not just a handful.
- Basic input validation (non-empty name text, 40-character max, SVG
  parse-validation on upload) surfaced as inline errors in the UI.
- An experimental, unsaved `/experimental` page builds meshes live in the
  browser (no backend round trip) for comparing live-preview responsiveness
  against the normal backend-driven flow. Supports a subset of shapes
  (Rectangle, RoundedRectangle, Circle, Oval) and a single uniform margin —
  it's a responsiveness comparison, not a feature-complete alternate editor.

## Known limitations

- No headless browser was available for parts of this project's history; UI
  rendering/interaction has since been verified with Playwright against a
  locally running dev server (`vite --port 5173`, matching the backend's
  CORS allowlist) in addition to `tsc`/`vite build` and API contract tests.
- One STL per name (plus its pack) — no multi-name plate nesting/packing
  across different names onto a single print bed.
- Text-plate combination is overlap-based (small z-overlap, not a true CSG
  boolean), which is print-safe for FDM but not a literal subtractive engrave.
- The wine-glass charm and clothes clip are always a plain rounded
  rectangle — they don't inherit the project's chosen plate shape, bevel,
  margins/alignment, or mounting holes (only their own size/hook/clip
  dimensions are configurable). Their spring action and stem fit have not
  been validated on a real printer in this environment.
