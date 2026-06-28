# Trip Planner & Wishlist — Design

## 1. Concept summary

Three pillars, sharing a common Tag/Location/Media vocabulary:

1. **Wishlist Locations** — places you want to go "someday". Each has notes, links,
   tags, a map pin, a freeform plan (how/when/where to stay) and a whiteboard
   (visual canvas) for brainstorming.
2. **Goals** — a *generic, user-configurable* container for a checklist-style
   ambition that is satisfied over one or many trips. Examples the system must
   support **without code changes**, purely from the frontend:
   - "Visit all continents" — a goal with 7 items, each a continent, checked
     off when visited.
   - "Michael Jackson statues" — a goal whose items are statues, each with
     custom fields (sculptor, year, city), a map pin, and on completion:
     photos, notes, links, memorabilia, visited date.
   - "3-week USA road trip" — a goal that is really just one big Trip; modeled
     as a Goal that *links* to a Trip rather than re-implementing trip logic.
3. **Trips** — a concrete, dated trip: stops on a map, start/end, bookings
   (flights/hotels/cars/tickets), and a **Timeline** of journal entries
   (photo/video/note/url) captured chronologically and geotagged, mostly from
   a phone while traveling.

Everything (locations, goal items, trips, timeline entries) can carry **Tags**
and **Media**, and can optionally be pinned to a **map coordinate**.

## 2. Domain model

```
Tag (id, name, color)
  *-* TaggedItem (polymorphic: EntityType + EntityId)

MediaItem (id, url/blobPath, kind: Photo|Video, caption, takenAt, lat, lng, EntityType, EntityId)

WishlistLocation
  id, name, description, lat, lng, country, createdAt
  tags: many-to-many via TaggedItem
  notes: WishlistNote[] (text, createdAt)
  links: WishlistLink[] (url, label)
  plan: WishlistPlan (1:1) { transportDetails, accommodationDetails, dateRangeStart/End, freeformText }
  whiteboard: Whiteboard (1:1) { contentJson }  -- canvas elements (sticky notes, shapes, images, lines)
  media: MediaItem[]
  goalItemLinks: GoalItem[] (a wishlist location can BE the subject of a goal item)

Goal
  id, name, description, icon, kind: Checklist | TripLink
  fieldDefinitions: GoalFieldDefinition[]   -- defines the *custom schema* for items
  items: GoalItem[]
  linkedTripId: nullable FK -> Trip          -- used when kind = TripLink (e.g. "USA Roadtrip")
  tags

GoalFieldDefinition
  id, goalId, key, label, fieldType: Text|Number|Date|Url|Boolean|Location, sortOrder

GoalItem
  id, goalId, name, lat, lng, isCompleted, completedAt
  fieldValues: GoalItemFieldValue[] (fieldDefinitionId, value)
  notes: text[]
  links: url[]
  media: MediaItem[]
  tags

Trip
  id, name, description, startDate, endDate, status: Planning|Active|Completed
  stops: TripStop[] (name, lat, lng, arriveDate, departDate, sortOrder, isStart, isEnd)
  bookings: Booking[] (type: Flight|Hotel|CarRental|Ticket|Other, title, confirmationNo, startAt, endAt, lat, lng, detailsJson, attachments)
  timeline: TimelineEntry[] (type: Note|Photo|Video|Url, content, lat, lng, capturedAt, nearestStopId)
  tags

TaggedItem / MediaItem use a lightweight polymorphic (EntityType enum + EntityId guid)
pattern instead of separate join tables per entity — keeps Tag/Media reusable
across WishlistLocation, GoalItem, Trip, TripStop, Booking, TimelineEntry.
```

Key generic-design decision: **Goal + GoalFieldDefinition + GoalItemFieldValue**
is an EAV (entity-attribute-value) sub-schema. Creating "MJ statues" is just:
create a Goal, add field defs (Sculptor: Text, Year: Number, City: Text),
then add GoalItems with values for those fields — all via API/UI, no backend
change. The "visit all continents" goal needs zero custom fields — just name +
isCompleted per item. The "USA roadtrip" goal uses `kind=TripLink` to point at
a real Trip instead of duplicating itinerary modeling.

## 3. Tech stack

- **Backend**: ASP.NET Core 8 Web API, EF Core 8 + Npgsql (PostgreSQL).
  Layered: `TripPlanner.Domain` (entities), `TripPlanner.Infrastructure`
  (DbContext, migrations, repositories), `TripPlanner.Api` (controllers, DTOs).
- **DB**: PostgreSQL. JSONB columns for whiteboard contents, booking details,
  goal item field values where appropriate.
- **Frontend**: React 18 + TypeScript + Vite, shadcn/ui + Tailwind, React
  Router, TanStack Query for data fetching, Leaflet (OpenStreetMap) for maps,
  a lightweight canvas (custom `<Whiteboard>` using `react-konva`) for the
  wishlist whiteboard.
- **Responsive strategy**: one React app, two layout modes — a desktop
  "planner" layout (sidebars, multi-column, drag/drop) and a mobile "trip
  mode" layout (bottom-tab nav, big tap targets, camera/file quick-add,
  ticket/hotel detail cards optimized for a phone screen) selected by
  viewport + a manual "On the trip" toggle.

## 4. API surface (REST, JSON)

```
/api/tags                          CRUD
/api/locations                     CRUD wishlist locations
/api/locations/{id}/notes          CRUD
/api/locations/{id}/links          CRUD
/api/locations/{id}/plan           GET/PUT
/api/locations/{id}/whiteboard     GET/PUT
/api/goals                         CRUD (incl. fieldDefinitions in payload)
/api/goals/{id}/items              CRUD
/api/goals/{id}/items/{itemId}/complete   POST
/api/trips                         CRUD
/api/trips/{id}/stops              CRUD
/api/trips/{id}/bookings           CRUD
/api/trips/{id}/timeline           CRUD (GET sorted by capturedAt)
/api/media                         POST upload, GET by entity
```

## 5. Frontend screens

- **Dashboard** — overview cards: upcoming trips, active goals progress, recent wishlist adds.
- **Wishlist** — map + list toggle, filter by tag; detail page tabs: Overview / Notes / Links / Plan / Whiteboard.
- **Goals** — list of goals as progress cards; Goal detail: table/grid of items (columns driven by fieldDefinitions) + map; "New Goal" wizard lets user define custom fields; Goal item drawer: complete checkbox, photos, notes, links, memorabilia.
- **Trips** — list; Trip detail (desktop): map with stops, itinerary timeline, bookings tabs. Trip detail (mobile / "On the trip" mode): bottom tabs [Now, Itinerary, Bookings, Journal], big quick-add FAB for photo/video/note that auto-geotags + timestamps and slots into the Journal timeline.

## 6. Build order

1. Backend domain + EF Core + Postgres migrations.
2. CRUD API.
3. Frontend scaffold + API client.
4. Wishlist UI (incl. whiteboard).
5. Goals UI (generic builder + item checklist).
6. Trip planner + mobile trip mode.
