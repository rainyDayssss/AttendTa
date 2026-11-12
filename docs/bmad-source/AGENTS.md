<!-- bmad:context -->
<!-- Verified 2026-11-04 against f65b054. Managed by bmad-project-context; edits inside this block are replaced on refresh. Keep anything you want preserved outside the markers. -->

## AttendTa

Event registration and attendance system for a single student organization (Alliance Jumpstart J4S, Group 7). The repo currently holds a static HTML and vanilla-JS visual prototype that runs on localStorage mock data. The ASP.NET Core MVC, EF Core, and SQL Server app will be built here later. Requirements are in `docsss/`; the PRD and its addendum are in `_bmad-output/initiative-attendta/prd-attendta/`.

## Policy

- Never commit or push to `master`, `main`, or `dev`. Work on `feature/*` or `fix/*` branches cut from upstream `dev`.
- Push branches to your fork and open PRs against `dev` on `Wynnwrath/attendta` (the `upstream` remote), never against `master`. Every PR needs at least one peer approval.
- Keep a branch current by merging `dev` into it. Never run `git push --force` or `git reset --hard` on `dev` or `main`/`master`.
- Commit subjects use `<type>(optional-scope): <lowercase imperative description>`. Allowed types are only `feat`, `fix`, `docs`, `style`, `refactor`, `test`, and `chore`; never `perf`, `build`, or `ci`.
- When sources disagree, SPEC v2.1 (`docsss/AttendTa-System-Specification.md`) wins over the UC, HLD, WBS, and Project Plan, unless PRD §9 records an override (e.g. login by email or student number).
- Never edit the static prototype as part of ASP.NET app work. It is the frozen visual reference that Razor views are ported from.

## Where things are

- Schema source: `docsss/AttendTa-MVP.dbml`, with `docsss/AttendTa-ERD.md`. Stack choices, schema tensions, and module ownership: `_bmad-output/initiative-attendta/prd-attendta/addendum.md`.
- Prototype mock store and seed data: `assets/app.js` (`Ta.store`). Visual contract: `assets/DESIGN-TOKENS.md`, with values in `assets/tokens.css`.

## Running and verifying

- The prototype has no build step. Tailwind comes from the Play CDN, and `assets/tailwind.config.js` is loaded as a browser global. Never add `package.json`, npm, or the Tailwind CLI to it.
- Verify prototype changes with `python server.py` and a private browser window. In a normal window, stored `attendta:v2:*` localStorage keys override the seed data.
- After changing records in `seedData()`, bump `version` in `Ta.store._load` (`assets/app.js`), or existing browsers keep the old data. The bump wipes all stored state, including the session. Adding a new top-level key needs no bump.
- TODO (ASP.NET Core MVC / EF Core / SQL Server): record the exact build, test, and lint commands on the first refresh after the solution exists. Tests and lint must pass locally before every push (git_qa_guidelines Tier 1).

## Conventions that differ from defaults

- EF Core is database-first. Schema changes start in `docsss/AttendTa-MVP.dbml`, and entities are scaffolded from the database; never add EF code-first migrations. Enums are SQL Server lookup tables with foreign keys, not C# enums stored as ints.
- When porting a prototype screen, replace its mocks with server-side equivalents: localStorage becomes SQL Server, and the client-side QR (`js/qr-engine.js`) becomes a PNG rendered on the server from `registrations.qr_token`. SPEC requires persistent server-local file storage, never browser storage or Supabase.
- New or edited prototype markup follows the locked rules in `assets/DESIGN-TOKENS.md`, even where the surrounding admin and kiosk markup does not. Fix existing violations only in lines you touch.
- Console navigation is duplicated inline in all 13 `<aside>` sidebars, and student navigation in every student page and its drawer. Adding or renaming a nav item means editing every copy.
- Never rename the `attendta:v2:` storage prefix or change the shape of `prefs`. `assets/ink-seam.js` reads `attendta:v2:prefs` directly.
- When a page's query parameters change (`?id=`, `?state=`, `?modal=`, `?tab=`), update the matching deep links in `prototype-navigator.html`.
- Keep the Quick Test Personas on `account/login.html`. They are the demo path, and they fill in users seeded by `assets/app.js`.

<!-- /bmad:context -->
