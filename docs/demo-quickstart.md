# Demo quick start (fresh clone)

From a clean clone to the running demo build in about ten minutes. The full presenter
walkthrough is in `_bmad-output/initiative-attendta/demo-script.md`; this page is only the
setup. Never run the reset or seed scripts against a shared or production database.

## 0. Prerequisites

- .NET 10 SDK
- SQL Server (LocalDB is fine) and `sqlcmd`
- Node.js 22 for the CSS build
- Optional: a webcam for camera scanning; HTTPS for phones

## 1. Clone, restore, build

```powershell
git clone <repository-url>
cd AttendTaBMAD
dotnet build ASI.Basecode.sln
cd ASI.Basecode.WebApp
npm install
npm run css:build
cd ..
```

## 2. Create the disposable database

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -i database/reset-local-mvp-test-db.sql
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -d AttendTaMVP_Test -i database/attendta-mvp-schema.sql
```

`reset-local-mvp-test-db.sql` drops and recreates only `AttendTaMVP_Test`. On the demo host,
use `AttendTa_Demo` and follow `docs/demo-host-setup.md` instead.

## 3. Generate the three password hashes

Only the PHC hash goes to stdout; the timing note goes to stderr.

```powershell
dotnet run --project "ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj" --no-build -- --hash-password "<admin secret>"
dotnet run --project "ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj" --no-build -- --hash-password "<officer secret>"
dotnet run --project "ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj" --no-build -- --hash-password "<member secret>"
```

## 4. Seed the starting state

Create a wrapper outside the repository (for example `%TEMP%\attendta-seed.sql`) so PowerShell
does not split the hashes:

```sql
:setvar AdminName "Maria Clara Dizon"
:setvar AdminEmail "maria.dizon@uc.edu.ph"
:setvar AdminPasswordHash "<admin hash>"
:setvar OfficerName "Rafael Santos"
:setvar OfficerEmail "rafael.santos@uc.edu.ph"
:setvar OfficerPasswordHash "<officer hash>"
:setvar MemberName "Liza Fernandez"
:setvar MemberEmail "liza.fernandez@uc.edu.ph"
:setvar MemberPasswordHash "<member hash>"
:r C:\path\to\AttendTaBMAD\database\seed-local-admin.sql
:r C:\path\to\AttendTaBMAD\database\seed-event-types.sql
:r C:\path\to\AttendTaBMAD\database\seed-demo-scenario.sql
```

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -d AttendTaMVP_Test -i "$env:TEMP\attendta-seed.sql"
```

The scenario seed is idempotent: re-running keeps the same accounts, members, venues, events and
registrations, and re-anchors the event dates around the current UTC time.

## 5. Copy the private images

Create the setup root outside the repository and `wwwroot`, then copy the tracked prototype
assets into it. The event covers must use the 32-character GUID names the cover service accepts;
the exact commands are in `demo-script.md` §2.3.

```powershell
$root = "<private setup root>"
New-Item -ItemType Directory -Force "$root\keys","$root\logs","$root\temporary","$root\uploads\venues","$root\uploads\events","$root\certificates" | Out-Null
```

## 6. Start the demo build

Keep secrets in the process environment, never in a tracked file.

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:DemoData__Enabled = "true"
$env:DemoData__AllowDatabase = "AttendTaMVP_Test"   # or AttendTa_Demo
$env:DemoData__TokenKey = "<local random secret>"
$env:ConnectionStrings__DefaultConnection = "Server=(localdb)\MSSQLLocalDB;Database=AttendTaMVP_Test;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
$env:Common__SetupRoot = "<private setup root>"
$env:ASPNETCORE_URLS = "http://localhost:5089"
dotnet run --project "ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj" --no-build
```

The startup log must say:

```text
Demo data active (M-01, M-04, M-05, M-06, M-07). Check-in open event 3; ready to complete event 1.
```

## 7. Sign in

Use the emails above with the passwords you hashed. The Administrator sees the management
shortcuts, the Officer sees the event and check-in surfaces, and the Member sees the event feed,
passes, My Events and the profile.

## 8. What to try

Follow the beats in `demo-script.md` §4–§6: account request and approval, master data, event
create and publish, signup and review, My Events and the pass, the door scan, completion,
certificate review/issue/download, the report, access changes and the Activity Log.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `Demo data refuses to start` | The connected database is not `AttendTa_Demo` and not `DemoData__AllowDatabase`, or `DemoData__TokenKey` is missing | Fix the database name or set the key |
| `Demo data seeded nothing` | The connected database has no published events | Re-run the scenario seed |
| Camera shows "not available" | No camera, permission denied, or the page is not on `https://` or `localhost` | Use the Pass code or Manual lookup tab; serve HTTPS for phones |
| `/CertificateTracer` returns 404 | Expected outside Development | Nothing to fix |
| A demo change disappeared | The in-memory store resets on restart | Restart the scenario or re-run the seed |

## Reset

Stop the app, then repeat steps 2 and 4. The reset script is destructive only to
`AttendTaMVP_Test`.

## Where the details live

- `_bmad-output/initiative-attendta/demo-script.md` — pre-flight, starting state and presenter beats
- `_bmad-output/initiative-attendta/demo-rehearsal-findings.md` — rehearsal evidence and open items
- `docs/demo-host-setup.md` — real demo-host provisioning
- `docs/database-setup.md` — database setup and admin provisioning
