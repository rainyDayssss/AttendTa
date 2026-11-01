# AGENTS.md — AttendTa

Student Organization Event Registration & Attendance System (Group 7).
ASP.NET Core MVC (.NET 9) web app for org events, member registration,
attendance check-in, certificate eligibility, and participation reports.

## Stack
- .NET 9 / ASP.NET Core MVC, EF Core 9 (SQL Server), AutoMapper, LinqKit, Serilog
- Razor views + Bootstrap + jQuery (toastr for notifications)

## Build & Run
- .NET 9 target, but only the .NET 10 SDK/runtime is installed on this machine:
  set `$env:DOTNET_ROLL_FORWARD = "LatestMajor"` before running.
- `dotnet build` — build the full solution.
- `dotnet run --project "ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj"` — start the app.
  (Pass the full path: two `ASI - Backup.*.csproj` files make `dotnet run --project ASI.Basecode.WebApp` ambiguous.)
- Startup project: `ASI.Basecode.WebApp`. Login is a hardcoded stub (any credentials work).

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

### Master Data status toggle
Members use an active/inactive status toggle instead of hard delete
(`MemberController.ToggleStatus`).

## SPEC Reference
Full specifications live in `spec/` (git submodule, private repo
`github.com/wigglebop25/SPEC-ALLIANCE`):

| File | Content |
|------|---------|
| `spec/AttendTa-SPEC.md` | Master Data module spec — fields, business rules, acceptance criteria |

Reference by path only. The SPEC is the source of truth for planned features not yet implemented.

## Database
- SQL Server LocalDB: `(localdb)\MSSQLLocalDB`, database `AsiBasecodeDB`.
- DB-first: use `dotnet ef dbcontext scaffold` for schema changes.
- Only Master Data tables are modeled in code: `members`, `event_types`, `venues`.
  Basecode `User` + auth stay untouched; other ERD tables are owned by other members.

## Test Conventions
No test project exists yet — add conventions here once tests are introduced.

## Documentation
- `spec/AttendTa-SPEC.md` — Master Data spec (submodule).
- `bruno/` — Bruno API collection for manual CRUD testing (no browser needed).

## Current assignment scope
Master Data only — Member Management, Event Type Management, Venue Management (Admin & Officer).
Auth, Transactions, Reports, Administration are owned by other members.

## Gotchas
- ERD uses `timestamp` for dates, but SQL Server treats it as rowversion → use `datetime2`.
- ERD `integer PRIMARY KEY` has no IDENTITY → `member_id`/`type_id`/`venue_id` set to `IDENTITY(1,1)`.
- ERD FK `users.user_id → members.user_id` was inverted → DB corrected to
  `members.user_id → users.user_id` (not modeled in code; `Member.UserId` is a nullable column).
