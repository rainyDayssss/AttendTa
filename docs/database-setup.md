# Database setup (MVP LocalDB)

AttendTa runs on one SQL Server database built from the canonical script
`database/attendta-mvp-schema.sql`. This guide sets up the disposable local database
`AttendTaMVP_Test` and provisions the first Administrator.

Do not point these scripts at a shared or production database. The schema, reset and seed
scripts refuse any database other than `AttendTaMVP_Test`, `AttendTa_IT_<guid>` or
`AttendTa_Demo`.

## Prerequisites

- .NET 10 SDK
- SQL Server LocalDB (`sqllocaldb info` should list `MSSQLLocalDB`)
- `sqlcmd` (ships with SQL Server and the ODBC client tools)

The examples below use LocalDB and PowerShell.

## 1. Reset the disposable database

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -i "database\reset-local-mvp-test-db.sql"
```

This drops and recreates only `AttendTaMVP_Test`.

## 2. Apply the canonical schema

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -d "AttendTaMVP_Test" -i "database\attendta-mvp-schema.sql"
```

The script creates all ten business tables, the eight lookup tables, the filtered unique
indexes, the append-only `activity_logs` trigger and `READ_COMMITTED_SNAPSHOT ON`.

## 3. Generate the Administrator password hash

Never write a plaintext password or a precomputed hash into a tracked file. Build once, then run
the hasher without rebuilding so stdout contains only the PHC string (the timing note goes to
stderr).

```powershell
dotnet build "ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj"
$hash = dotnet run --project "ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj" --no-build -- --hash-password "<choose-a-strong-password>"
```

`$hash` is exactly one `$argon2id$v=19$m=19456,t=2,p=1$...` line. Keep it in the current shell
session only.

## 4. Provision the first Administrator

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -d "AttendTaMVP_Test" `
  -i "database\seed-local-admin.sql" `
  -v AdminName="AttendTa Local Admin" `
     AdminEmail="admin@example.edu" `
     AdminPasswordHash="$hash"
```

What the script does:

- refuses a missing name, a malformed email, or a hash that is not a `$argon2id$` PHC string;
- inserts one active Administrator (`role_id = 1`, `is_active = 1`) with `created_by = NULL`;
- appends exactly one system audit row to `activity_logs`
  (`actor_type_id = 2`, `action = 'user.provisioned'`, `entity_type = 'users'`);
- never stores or prints the plaintext password, and never writes the hash into the audit row;
- is idempotent: re-running it with the same email leaves the existing active Administrator and
  its audit row untouched.

There are no default credentials. Public requests never grant a privileged role.

## 5. Seed the starter event types

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -d "AttendTaMVP_Test" -i "database\seed-event-types.sql"
```

The script selects the active Administrator by role (not by a hard-coded email) for the required
`created_by` field, and is idempotent.

## 6. Run the app

Neither the connection string nor the setup root is committed. Set both for the session.
`Common:SetupRoot` must be a private directory outside `wwwroot` and the deployment tree; Data
Protection keys, uploads and generated certificates live under it.

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=(localdb)\MSSQLLocalDB;Database=AttendTaMVP_Test;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
$env:Common__SetupRoot = "C:\attendta-private"
dotnet run --project "ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj"
```

The app fails fast with a clear message when configuration is missing or unsafe:

- `ConnectionStrings:DefaultConnection is not configured` when the connection string is unset.
- `Common:SetupRoot must be configured so Data Protection keys can be stored under its keys folder.`
  when `Common__SetupRoot` is unset or empty.
- `Common:SetupRoot must be outside wwwroot and the application deployment directory.` when the
  setup root points inside `wwwroot` or the deployment tree.

## 7. Sign in

Open <http://localhost:5000> (the default Kestrel address) and sign in with the `AdminEmail` and
password you provisioned. Sign-in is by email, not username.

- The cookie is named `AttendTa.Auth`, is non-persistent, lasts 8 hours and does not slide.
- `OnValidatePrincipal` reloads the account on every request: deactivating the account or
  changing its role in the database rejects the session on its next request.
- Every failure (unknown email, wrong password, inactive account) shows one generic message.
- Logout is POST-only and requires an antiforgery token; a GET to `/Account/Logout` returns 405.

Data Protection keys are stored under `<Common:SetupRoot>/keys` so cookies survive restarts.
Deleting that folder signs every user out.

## Certificate signatory asset

Official certificate rendering uses the approved, non-secret settings in
`ASI.Basecode.WebApp/appsettings.json` under `Certificates:Signatory`: the name
`Christian Kyle Ducay`, the title `Organization President`, and the packaged signature asset
`img/christian-kyle-ducay-signature.png`. The fixed template validates these values against the
approved identity and rejects a missing or invalid signature asset before returning a PDF. Keep
the image at `ASI.Basecode.WebApp/wwwroot/img/christian-kyle-ducay-signature.png` in deployed
packages; it is embedded into the PDF, so certificate rendering makes no external image request.
Do not replace the approved details or signature with placeholders. Every rendered certificate is
an official certificate; there is no sample or watermarked rendering.

## Existing venue photo paths

The older app stored venue keys as `venues/<name>` relative to `<Common:SetupRoot>/uploads`.
The shared private store reads `uploads/venues/<name>` relative to `<Common:SetupRoot>`; the
physical photos stay in the same directory. Fresh databases already use the new path form.

For an existing local database, schedule a brief app outage and follow this order so no running
version reads keys using the wrong root:

1. Stop the app version that still uses the venue-only store.
2. Back up the intended local database and `<Common:SetupRoot>/uploads` together.
3. Run the migration against the disposable local `AttendTaMVP_Test` database:

   ```powershell
   sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -d "AttendTaMVP_Test" -i "database\migrate-venue-image-paths.sql"
   ```

4. Deploy and start the version that reads `uploads/venues/<name>` from `Common:SetupRoot`.

The script is idempotent and does not move files. It checks every legacy key against the 500-character
column limit before updating any row. If a key would be too long, it prints the affected venue IDs and
stops with all paths unchanged. Do not run it against `AsiBasecodeDB`, shared databases, or production.

## Integration tests

The integration test project creates and drops its own `AttendTa_IT_<guid>` database for each
test class, from the same canonical script. Point it at a local SQL Server **master** database:

```powershell
$env:ATTENDTA_TEST_SQL = "Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True"
dotnet build ASI.Basecode.sln
pwsh "ASI.Basecode.WebApp\bin\Debug\net10.0\playwright.ps1" install chromium
dotnet tests\ASI.Basecode.IntegrationTests\bin\Debug\net10.0\ASI.Basecode.IntegrationTests.dll -parallelMode none
```

The principal in `ATTENDTA_TEST_SQL` must be allowed to create and drop databases on a local
instance. The runner's `-parallelMode none` option serializes test classes because each fixture
creates its own SQL database. Certificate PDF integration tests launch headless Chromium through
Microsoft.Playwright and inspect its actual output with Poppler's `pdftotext`, `pdfinfo` and
`pdffonts` commands. Install a Poppler distribution and make all three commands available on
`PATH`; or point the test runner at their full paths in the current PowerShell session:

```powershell
$env:ATTENDTA_PDFTOTEXT = "C:\tools\poppler\Library\bin\pdftotext.exe"
$env:ATTENDTA_PDFINFO = "C:\tools\poppler\Library\bin\pdfinfo.exe"
$env:ATTENDTA_PDFFONTS = "C:\tools\poppler\Library\bin\pdffonts.exe"
```

The assertions extract the supplied text, confirm one landscape A4 page, and check that Cinzel is
embedded. The matching Playwright Chromium revision is installed by the command above. The Cinzel
regular and semibold files are bundled under `ASI.Basecode.WebApp/wwwroot/fonts/cinzel/` with their
SIL Open Font License at `ASI.Basecode.WebApp/wwwroot/fonts/cinzel/OFL.txt`. Any CI job that runs
the integration suite must install Poppler and Chromium before running it. The renderer creates
the official PDF during issuance, promotes it into private `certificates/` storage, and the
authorized download streams those retained bytes without regenerating them.
