# Release cleanup evidence (refactor sweep)

Record of the pre-acceptance release sweep for ticket 10 (issue #83). Baseline revision
`d675a86b3bb47d21a5c74cfb16d0e014f58e8736`; the sweep is commit
`9bfc27aef0eb3e58b3b5c4eb021fcee0fe7e2439`. Date: 2026-11-10. The scans, build, CSS and
startup-smoke results below were produced on that commit.

## What was removed or changed

| Item | Before | After | Reason |
|---|---|---|---|
| `ASI.Basecode.WebApp/ASI - Backup.Basecode.WebApp.csproj` | Tracked throwaway duplicate | Deleted | Not in `ASI.Basecode.sln`; made `dotnet run --project ASI.Basecode.WebApp` ambiguous |
| `ASI.Basecode.Services/ASI - Backup.Basecode.Services.csproj` | Tracked throwaway duplicate | Deleted | Same |
| `ASI.Basecode.WebApp/ASI.Basecode.WebApp.csproj` | `<None Remove="appsettings.json.rej" />` | Line removed | Dead MSBuild item; `appsettings.json.rej` does not exist |
| `ASI.Basecode.WebApp/appsettings.json` | `"SetupRoot": "C:\\setup\\projects\\ASI.Basecode\\root"` | `"SetupRoot": ""` | Developer-machine path in deployment settings; the existing fail-fast now owns the value |
| `README.md` | BaseCode template ("C# BaseCode / ASI Bridge/JumpStart", edit `appsettings.json`, Visual Studio 2022) | AttendTa prerequisites, build, database, run, test, demo | Template told the reader to edit a key that does not exist and to use an IDE that cannot target `net10.0` |
| `docs/database-setup.md` | §6 run block omitted `Common__SetupRoot`; §7 had no URL | §6 sets both env vars and lists all three fail-fast messages; §7 names `http://localhost:5000` | Setup instructions must match the integrated build |
| `AGENTS.md` | Backup-csproj workaround, no `Common__SetupRoot`, no `Policies.Member`, test commands diverging from CI | Run block includes `Common__SetupRoot`; workaround dropped; `Policies.Member` added; both test commands match `ci.yml`; missing doc paths added | Keep agent instructions true to this commit |
| `.gitignore` | Covered `*.pfx`, `hotspot-certificates/`, `launchSettings.json`, `docs-local/` | Added `/uploads/`, `/keys/`, `/certificates/`, `*.cer`, `*.crt`, `*.key`, `*.pem`, `*.p12`, `*.bak`, `*.sqlite`, `*.db`, `.env`, `appsettings.*.local.json` | Private storage and secret hygiene |

`/uploads/`, `/keys/` and `/certificates/` are root-anchored deliberately. An unanchored
`certificates/` pattern matches the tracked source folder `ASI.Basecode.WebApp/Certificates/`
on case-insensitive filesystems (Windows), which would silently ignore new renderer files. The
private store itself lives under `<Common:SetupRoot>`, outside the repository.

`Common:SetupRoot` was blanked rather than deleted so the key stays discoverable in
`appsettings.json` while the value comes from the environment — the same shape as
`ConnectionStrings:DefaultConnection`, which is absent from the file, documented as an env var,
and guarded by a fail-fast throw.

## Configuration and secret scan

| Scan | Command | Result |
|---|---|---|
| Demo switch | `git grep -n "DemoData" -- "ASI.Basecode.WebApp/appsettings*.json"` | No match (exit 1) |
| Mock/prototype/fixture keys | `git grep -n -E '"(Mock\|Prototype\|Fixture\|DemoData)"' -- 'ASI.Basecode.WebApp/appsettings*.json'` | No match (exit 1) |
| Backup project files | `git ls-files \| Select-String "ASI - Backup"` | 0 matches |
| Key material, certs, databases, env files | `git ls-files \| Select-String "\.(pfx\|pem\|key\|crt\|cer\|p12\|snk\|mdf\|ldf\|sqlite\|db)$\|^\.env"` | 0 matches |
| Committed connection string | `appsettings.json` | No `ConnectionStrings` section |
| Committed setup root | `appsettings.json` | `Common:SetupRoot` is `""` |

No plaintext credentials, password hashes, connection strings or machine-specific paths remain in
committed settings.

## Build and CSS results

| Gate | Command | Result |
|---|---|---|
| Build | `dotnet build ASI.Basecode.sln --configuration Release` | Build succeeded — 0 warnings, 0 errors |
| CSS install | `npm --prefix ASI.Basecode.WebApp ci` | 28 packages added, no errors |
| CSS build | `npm --prefix ASI.Basecode.WebApp run css:build` | Tailwind v4.3.4, done in 174 ms |
| CSS gate | `git diff --exit-code -- ASI.Basecode.WebApp/wwwroot/css/app.css` | No difference (exit 0) |

## Startup smoke

Both runs used `--configuration Release --no-build` with
`ConnectionStrings__DefaultConnection` set to the disposable `AttendTaMVP_Test` LocalDB.

| State | Expected | Observed |
|---|---|---|
| `Common__SetupRoot` unset | Fail fast on configuration | `System.InvalidOperationException: Common:SetupRoot must be configured so Data Protection keys can be stored under its keys folder.` at `Startup.cs:173`, no partial start |
| `Common__SetupRoot` set to a private directory outside the deployment tree | Pass configuration and start | `Now listening on: http://127.0.0.1:5199`, `Application started`, and a `GET /` returned `200`; Data Protection keys were created under `<SetupRoot>/keys` |

The second run bound `ASPNETCORE_URLS` to `http://127.0.0.1:5199` and used the same reachable
`AttendTaMVP_Test` LocalDB, so it proves configuration passes and the host serves traffic from a
clean environment-only configuration — the shape the acceptance host uses.

## What remains intentionally

- `docsss/` — epic source of truth.
- `bruno/` — manual CRUD collection.
- `tools/` — repository tooling.
- `database/seed-demo-scenario.sql`, `database/provision-demo-runtime.sql` — still needed by the
  8.7 demo walkthrough and 8.14 host evidence.
- `.github/workflows/ci.yml` — unchanged; it remains the real gate.
- `_bmad-output/`, `docs/` and `newdocs/` planning and evidence documents — not deleted.
- Razor, CSS, JS and every presentation surface — untouched; 8.12 owns them.
- Tests and the database schema — unchanged, except two additive guard tests (below).

## Guard tests added

The sweep makes `Common:SetupRoot` environment-only, so the fail-fast it relies on is now a
tested contract rather than only a runtime observation.

| Test | Class | Proves |
|---|---|---|
| `RejectsAMissingSetupRoot` | `PrivateFileStoreTests` | An absent key throws `Common:SetupRoot must be configured for private file storage.` |
| `RejectsASetupRootInsideTheWebOrContentRoot` (web, content) | `PrivateFileStoreTests` | A root inside `wwwroot` or the deployment tree throws `Common:SetupRoot must be outside wwwroot and the application deployment directory.` |
| `RefusesToStartWithoutASetupRoot` | `StartupConfigurationGuardTests` | Building the real host with an empty key throws `Common:SetupRoot must be configured so Data Protection keys can be stored under its keys folder.` |

Both classes ran green on `2026-11-10` against the disposable LocalDB:

```
dotnet tests\ASI.Basecode.IntegrationTests\bin\Release\net10.0\ASI.Basecode.IntegrationTests.dll -class "ASI.Basecode.IntegrationTests.PrivateFileStoreTests" -parallelMode none
  Total: 6, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0

dotnet tests\ASI.Basecode.IntegrationTests\bin\Release\net10.0\ASI.Basecode.IntegrationTests.dll -class "ASI.Basecode.IntegrationTests.StartupConfigurationGuardTests" -parallelMode none
  Total: 1, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0
```

## Not covered by this sweep

- No UI critique or polish was run: the sweep touches no Razor, CSS or JS, and the
  demo/integration exception in `AGENTS.md` applies.
- The whole integration suite was not executed; only the two guard classes above were. The suite is
  unchanged, and its combined execution is 8.4 (#77) with `ATTENDTA_TEST_SQL` set.
