# AGENTS.md — AttendTa

Student Organization Event Registration & Attendance System (Group 7).
ASP.NET Core MVC (.NET 10) web app for org events, member registration,
attendance check-in, certificate eligibility, and participation reports.

## Stack
- .NET 10 / ASP.NET Core MVC, EF Core 10 (SQL Server), AutoMapper, LinqKit, Serilog
- Razor views + Bootstrap + jQuery (toastr for notifications)

## Build & Run
- Install the .NET 10 SDK before building or running the solution.
- `dotnet build` — build the full solution.
- `dotnet run --project "ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj"` — start the app.
  (Pass the full path: two `ASI - Backup.*.csproj` files make `dotnet run --project ASI.Basecode.WebApp` ambiguous.)
- Startup project: `ASI.Basecode.WebApp`. Sign-in is real email + password against `users`
  (Argon2id hash, cookie authentication). There is no default account: provision the first
  Administrator with `database/seed-local-admin.sql` before signing in.
- Generate a password hash without starting the web server. Build first so `dotnet run` emits no
  build output, then capture stdout:
  `dotnet build "ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj"` then
  `dotnet run --project "ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj" --no-build -- --hash-password "<secret>"`.
  Only the PHC `$argon2id$...` hash goes to stdout; the timing note goes to stderr.
- The connection string is not committed. Set it in the PowerShell session before running:
  `$env:ConnectionStrings__DefaultConnection = "Server=(localdb)\MSSQLLocalDB;Database=AttendTaMVP_Test;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"`.
  The app fails fast with a clear message when it is missing.

## Architecture
4-layer solution (Repository → Service → Controller):

- **Data** (`ASI.Basecode.Data/`) — `AsiBasecodeDbContext.cs`, entities (`Models/`),
  repositories (`Repositories/` + `Interfaces/`). DB access only.
- **Services** (`ASI.Basecode.Services/`) — business logic (`Services/` + `Interfaces/`),
  viewmodels (`ServiceModels/`).
- **Resources** (`ASI.Basecode.Resources/`) — constants (`Constants/`), messages (`Messages/*.resx`).
- **WebApp** (`ASI.Basecode.WebApp/`) — controllers (`Controllers/`), views (`Views/`),
  `Startup.*.cs` (DI, AutoMapper, Auth, Logger), `Program.cs`.

## Module Conventions
- Pattern: Repository → Service → Controller.
- Repos: `Data/Repositories/<X>Repository.cs` + `Data/Interfaces/I<X>Repository.cs`;
  inherit `BaseRepository`, take `IUnitOfWork`.
- Services: `Services/Services/<X>Service.cs` + `Services/Interfaces/I<X>Service.cs`;
  inject repo + `IMapper`.
- Viewmodels live in `Services/ServiceModels` (data annotations for validation).
- Register repos/services in `WebApp/Startup.DI.cs`; AutoMapper maps in `WebApp/Startup.AutoMapper.cs`.
- Controllers extend `ControllerBase<TController>` (namespace `ASI.Basecode.WebApp.Mvc`).

## Key Patterns

### BaseRepository / UnitOfWork
`BaseRepository` exposes `GetDbSet<T>()` and `SetEntityState(...)`; repos call
`UnitOfWork.SaveChanges()`. Each repo owns its add/update/delete + uniqueness checks
(e.g. `StudentNumberExists`, `TypeNameExists`).

### AutoMapper
`Startup.AutoMapper.cs` maps ViewModel ↔ Entity both directions. Services map the
ViewModel onto the tracked entity and mutate it before save.

### Notifications
toastr is wired in `_Layout.cshtml`; success/error messages flow through
`TempData["SuccessMessage"]` / `TempData["ErrorMessage"]`.

### Authentication and authorization
Cookie authentication (`AttendTa.Auth`, 8 hours, non-sliding, non-persistent) issues
`NameIdentifier` = `users.user_id` and `Role` = the lowercase lookup code (`admin`,
`officer`, `member`). `OnValidatePrincipal` reloads the account on every request and rejects
an inactive or role-changed session. Endpoints use the named policies `Policies.Management`
(officer or admin) and `Policies.Administrator`. Every mutation is a POST with antiforgery;
logout is POST-only. `ICurrentActor` is the only service-layer source of the actor.

### Master Data status toggle
Members use an active/inactive membership status; event types and venues use `is_active`.
Do not hard-delete these records.

## SPEC Reference
Full specifications live in `spec/` (git submodule, private repo
`github.com/wigglebop25/SPEC-ALLIANCE`):

| File | Content |
|------|---------|
| `spec/AttendTa-SPEC.md` | Master Data module spec — fields, business rules, acceptance criteria |

Reference by path only. The SPEC is the source of truth for planned features not yet implemented.

## Database
- PM-approved MVP schema reference: `docs-local/ERD-DATABASE-DESIGN/new mvp.sql` (local/ignored).
  The tracked setup copy is `database/attendta-mvp-schema.sql`; follow it for team setup.
- Setup guide: `docs/database-setup.md`. Use the disposable SQL Server LocalDB database
  `AttendTaMVP_Test` for MVP setup and testing. `database/reset-local-mvp-test-db.sql` is
  destructive to that named test database only; never run it against a shared or production DB.
- For local setup, generate a hash with `--hash-password`, then run `database/seed-local-admin.sql`
  with `-v AdminName=... AdminEmail=... AdminPasswordHash=...`, then run
  `database/seed-event-types.sql`. The admin seed is guarded to the disposable test database,
  validates that the hash is a `$argon2id$` PHC string, creates one active Administrator and one
  `actor_type_id = 2` `activity_logs` row. It does not store a plaintext password or a precomputed
  password hash.
- No historical records are migrated. Keep the existing `AsiBasecodeDB` untouched as a fallback.
- Database-first: scaffold/align EF mappings from the approved MVP schema; keep generated models
  and mappings consistent with the executable schema script.
- This assignment implements Master Data (`members`, `event_types`, `venues`) only. Auth owns
  `users`/`account_requests`; Events, Registrations, Attendance, Certificates, and Activity Logs
  are owned by other members. Coordinate schema/API boundaries rather than implementing those modules.
- MVP `members.user_id` is required and unique. Full name/email belong to the linked user; member
  records carry course, year level, section, mobile number, membership type, and membership status.
  Member creation is exposed through `CreateMemberFromApprovedAccountRequestAsync` for Auth integration;
  do not add standalone member creation.
- Master Data inserts require a valid authenticated actor ID for `created_by`; never hard-code or
  substitute `0`. `ControllerBase.GetActorUserId()` reads the real `users.user_id` from the
  authenticated cookie claims.
- Venue `image_path` is required and relative, stored as `uploads/venues/<name>` from
  `<Common:SetupRoot>`. The shared private store is rooted outside `wwwroot` and the deployment
  tree; venue and event images accept JPEG/PNG up to 2 MiB and persist only area-prefixed relative paths.
- Existing venue keys in the `venues/<name>` form are migrated with
  `database/migrate-venue-image-paths.sql`; stop the old app before the migration and deploy the
  new app before restarting. Back up the intended local database and setup-root uploads first.
  Never run this migration against shared or production databases or `AsiBasecodeDB`.
- `database/seed-local-admin.sql` creates the initial Administrator from the `AdminName`,
  `AdminEmail` and `AdminPasswordHash` sqlcmd variables. Do not commit plaintext passwords, password
  hashes, or shared test passwords.
- `database/seed-event-types.sql` seeds the six starter classifications after an active
  Administrator exists. It is idempotent and selects the active admin by role for `created_by`.

## Test Conventions
- Run `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj`
  (Argon2id hasher rules).
- Run `dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj`
  with `ATTENDTA_TEST_SQL` set to a local SQL Server **master** connection string
  (for example `Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True`).
  Each test class creates and drops its own `AttendTa_IT_<guid>` database from the canonical script.
  The suite covers the schema, the admin seed, `AccountService`, and the cookie/policy auth flow
  through `WebApplicationFactory`.

## Documentation
- `spec/AttendTa-SPEC.md` — Master Data spec (submodule).
- `bruno/` — Bruno API collection for manual CRUD testing (no browser needed).
- `docs/database-setup.md` — MVP LocalDB setup/reset, schema, admin provisioning, and event-type seed instructions.

## UI review
The impeccable design skill is installed at `.opencode/skills/impeccable`, with `/impeccable` in `.opencode/commands`. For any UI-facing ticket — a new screen, form, table, dialog, navigation change, or a refactor sweep that touches Razor or CSS — run `/impeccable critique` on the changed surface and `/impeccable polish` before review. Keep `PRODUCT.md` current; `docs/DESIGN.md` remains the visual authority.

Exception (2026-11-06 demo rescope, project owner): tickets in the demo and integration phases of `_bmad-output/initiative-attendta/delivery-queue.md` do not run per-ticket critique or polish. The demo rehearsal (8.15) runs critique and polish once for the demo surface, and 8.12 and 2.11 run them for the integrated app. Each pull request still keeps green CI and one peer approval.

## Subagents
Subagents (`general`, `explore`) run on `mistral/deepseek-v4-1-flash-1017`, set in `opencode.jsonc`
with no pinned variant. The orchestrator chooses the reasoning effort for each task by passing
`model` to the `subagent` tool:
- `#low` — mechanical lookups, pattern searches, formatting, small single-file edits.
- `#high` — normal research, implementation, and review work.
- `#max` — deep debugging, cross-module analysis, or adversarial review.
- `#none` — deterministic, trivial tasks where reasoning adds nothing.

Start at the cheapest effort that will reliably finish the task and escalate only when the result
is insufficient; do not default to `#max`.

The same model is also pinned in the global `~/.config/opencode/opencode.json` as a machine-wide
fallback. Project config outranks global config, so any repository can override it, while sessions
that fall outside this tree — notably the worktrees under `~/.local/share/opencode/worktree/` — still
get the intended subagent model instead of inheriting the parent session's.

## Current assignment scope
Master Data only — Member Management, Event Type Management, Venue Management (Admin & Officer).
Auth, Transactions, Reports, Administration are owned by other members.

## Gotchas
- SQL Server `timestamp` is rowversion, not a date/time; use the MVP's `datetime2(3)` timestamps.
- MVP integer primary keys use `IDENTITY(1,1)`.
- Filtered unique indexes must keep their `WHERE` predicates; pending-request email/student-number
  uniqueness is pending-only. The schema script sets required SQL Server options for filtered indexes.
- `new mvp.sql` has no standalone `username` column: the login identifier is `users.email`.
- Sign-in failures are deliberately generic: an unknown email, a wrong password and an inactive
  account all return the same message and no cookie. Never add per-case messages or lockout.
- The auth cookie is non-persistent, lasts 8 hours and does not slide. Data Protection keys live
  under `<Common:SetupRoot>/keys`; deleting them signs every user out.
- Venue photos are required by the MVP SQL. Existing `AsiBasecodeDB` has the legacy Master Data
  columns and is not the target for the disposable setup script.
