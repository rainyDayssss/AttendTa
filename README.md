# AttendTa

Student Organization Event Registration & Attendance System (Group 7). An ASP.NET Core MVC
(.NET 10) web app for organization events, member registration, attendance check-in,
certificate eligibility and participation reports.

## Prerequisites

- .NET 10 SDK
- Node.js 22 (builds the Tailwind CSS bundle)
- SQL Server (LocalDB is fine) and `sqlcmd`
- PowerShell for the commands below

Visual Studio 2022 cannot target `net10.0`; use the `dotnet` CLI or a .NET 10 capable IDE.

## Build

```powershell
dotnet build ASI.Basecode.sln
npm --prefix ASI.Basecode.WebApp ci
npm --prefix ASI.Basecode.WebApp run css:build
```

The CSS build is part of CI: it regenerates `ASI.Basecode.WebApp/wwwroot/css/app.css`, and
`git diff --exit-code -- ASI.Basecode.WebApp/wwwroot/css/app.css` must report no difference.

## Database

Create the disposable `AttendTaMVP_Test` database, apply the canonical schema, provision the
first Administrator and seed the starter event types. Follow
[docs/database-setup.md](docs/database-setup.md) — it is the authoritative setup guide and
names every script and `sqlcmd` variable.

There are no default credentials. Public requests never grant a privileged role.

## Run

Both settings come from the environment; neither is committed. `Common:SetupRoot` must be a
private directory outside `wwwroot` and the deployment tree, because Data Protection keys and
uploaded files are stored under it.

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=(localdb)\MSSQLLocalDB;Database=AttendTaMVP_Test;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
$env:Common__SetupRoot = "C:\attendta-private"
dotnet run --project "ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj"
```

The app listens on <http://localhost:5000> by default. It fails fast with a clear message when
`ConnectionStrings:DefaultConnection` or `Common:SetupRoot` is missing, or when `Common:SetupRoot`
points inside `wwwroot` or the deployment directory.

Sign in at `/Account/Login` with the `AdminEmail` and password you provisioned in
[docs/database-setup.md](docs/database-setup.md). Sign-in is by email, not username.

## Tests

These are the invocations CI uses. Build first.

```powershell
dotnet build ASI.Basecode.sln --configuration Release
dotnet test --project tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj --no-build --no-restore --configuration Release
```

That project is `xunit.v3.mtp-v2` with `OutputType=Exe`; if `dotnet test` reports `Zero tests ran`
and exits 5, run the test host directly instead:

```powershell
& "tests\ASI.Basecode.Services.Tests\bin\Release\net10.0\ASI.Basecode.Services.Tests.exe"
```

Integration tests create and drop their own `AttendTa_IT_<guid>` database from the canonical
schema, so `ATTENDTA_TEST_SQL` points at `master`:

```powershell
$env:ATTENDTA_TEST_SQL = "Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True"
dotnet tests/ASI.Basecode.IntegrationTests/bin/Release/net10.0/ASI.Basecode.IntegrationTests.dll -parallelMode none
```

Certificate PDF cases also need Playwright Chromium and Poppler; see
[docs/database-setup.md](docs/database-setup.md).

## Demo

From a fresh clone to the running demo build: see [docs/demo-quickstart.md](docs/demo-quickstart.md).

## Code structure

- `ASI.Basecode.Data` — entities, `AsiBasecodeDbContext`, repositories. Database access only;
  move additional logic into the service layer.
- `ASI.Basecode.Resources` — constants, messages and other static resources shared by the site.
- `ASI.Basecode.Services` — business logic, service interfaces and viewmodels.
- `ASI.Basecode.WebApp` — controllers, Razor views, `Startup.*.cs` (DI, AutoMapper, auth,
  logging) and `Program.cs`. Controllers stay thin; logic belongs in a service.

## Documentation

- [docs/database-setup.md](docs/database-setup.md) — LocalDB setup, schema, admin provisioning, event-type seed
- [docs/demo-quickstart.md](docs/demo-quickstart.md) — fresh clone to running demo build
- [docs/release-cleanup-evidence.md](docs/release-cleanup-evidence.md) - release sweep evidence
