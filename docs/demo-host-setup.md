# Demo host provisioning and readiness

This is a host-specific procedure, not evidence that a host is ready. Use a dedicated,
isolated demo machine and SQL Server instance only; never use a shared or production system.
Do not reset, replace, or reuse an existing database. Stop if `AttendTa_Demo` already exists.
HTTPS phone trust and backup/restore are separate work. A successful online run is not evidence
of offline operation.

For coordinated persistence capture and restart/redeploy/restore verification, follow
[storage backup and restore](storage-backup-restore.md) and record real-host results in
[storage survival evidence](storage-survival-evidence.md). Obtain explicit host, downtime and
new disposable-target approval first; provisioning or a closed issue does not prove survival.

## Select and prepare the host

Record the host name, OS version, CPU, RAM, .NET 10 SDK/runtime version, SQL Server version,
application revision, and the exact dedicated Windows service account and SQL login to be used.
Install .NET 10, SQL Server and sqlcmd; build the application from the selected revision.
Choose a private absolute `Common__SetupRoot` outside both the deployment and web roots, and
restrict its ACLs to the runtime identity and authorized administrators. Create `keys`, `logs`,
`temporary`, `uploads/venues`, `uploads/events`, and `certificates` beneath it. Ensure the app
identity can write there and other users cannot read private files. Do not put browser files,
keys, secrets or private files under wwwroot. The readiness check requires `logs` to exist, then
creates and probes the logger's `logs/<yyyyMM>` directory; it never creates a missing `logs/`.
Keep this same external SetupRoot through restarts, redeploys and deployment rollbacks. Never
include it in a deployment replacement, clean/publish target or staging-directory cleanup.

Choose a dedicated SQL login and database user, not the administrator used for setup. Prefer
Windows integrated authentication under the service account; provision the login through your
host's approved security procedure. Do not grant server or database roles or schema-wide grants.
Ensure the runtime identity is not database owner and does not inherit permissions via groups.

## Create and seed a new database

As the authorized administrator, check `SELECT name FROM sys.databases WHERE name = N'AttendTa_Demo'`.
If a row exists, stop and investigate. Otherwise create `AttendTa_Demo` once on the isolated
instance. Apply `database/attendta-mvp-schema.sql` with sqlcmd `-b -d AttendTa_Demo` and stop on
any error. The schema enables read committed snapshot and must only be applied to a fresh database.
Create a dedicated user for the selected runtime login in that database using your approved DBA
procedure. Run `database/provision-demo-runtime.sql` in that database as an administrator using
`sqlcmd -b -d AttendTa_Demo -v RuntimeUser=<user> -i database/provision-demo-runtime.sql`.
Use a simple DBA-selected user name with no quotes or sqlcmd metacharacters. The script never
creates logins, denies no existing grants, and rolls its grants back on unsafe effective audit
permissions, including a column-level `UPDATE` grant on any `activity_logs` column. The separate
runtime-login validation below is mandatory: impersonation of a database user alone cannot
establish server-level safety.

Seed the first admin with `database/seed-local-admin.sql`, then run
`database/seed-event-types.sql`, both with `sqlcmd -b -d AttendTa_Demo`. sqlcmd reads its
`AdminName`, `AdminEmail` and `AdminPasswordHash` scripting variables from the environment;
set these in a private process environment, not command arguments or tracked files. Generate the
hash with the built app's `--hash-password-env` mode: set `ATTENDTA_HASH_SECRET` in that same
private process, capture only stdout into `AdminPasswordHash`, then immediately clear
`ATTENDTA_HASH_SECRET`. Keep the real administrator password in an approved secret store, never
in shell history, command-line arguments, logs or evidence. Clear `AdminPasswordHash`, `AdminName`
and `AdminEmail` from the environment after seeding. Protect the setup process environment from
other users. sqlcmd authentication secrets, if needed, likewise belong in protected environment
variables (e.g. `SQLCMDPASSWORD`), not command arguments. Clear them after use.

For example, in a protected PowerShell session using Windows authentication, with `$server`
set to the dedicated instance and `$appDll` to the built `ASI.Basecode.WebApp.dll`:

```powershell
$env:AdminName = Read-Host 'Initial administrator name'
$env:AdminEmail = Read-Host 'Initial administrator email'
$secure = Read-Host 'Initial administrator password' -AsSecureString
$ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try { $env:ATTENDTA_HASH_SECRET = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr); $secure.Dispose() }
try {
    $env:AdminPasswordHash = (dotnet $appDll --hash-password-env)
    if ($LASTEXITCODE -ne 0) { throw 'Hash generation failed' }
    Remove-Item Env:ATTENDTA_HASH_SECRET
    sqlcmd -S $server -E -b -d AttendTa_Demo -i database/seed-local-admin.sql
    if ($LASTEXITCODE -ne 0) { throw 'Admin seed failed' }
    sqlcmd -S $server -E -b -d AttendTa_Demo -i database/seed-event-types.sql
    if ($LASTEXITCODE -ne 0) { throw 'Event-type seed failed' }
} finally {
    'ATTENDTA_HASH_SECRET','AdminPasswordHash','AdminName','AdminEmail' | ForEach-Object {
        Remove-Item "Env:$_" -ErrorAction SilentlyContinue
    }
}
```

## Stage and run

Build `ASI.Basecode.WebApp/ASI.Basecode.WebApp.csproj`, then run its generated
`bin/Debug/net10.0/playwright.ps1 install chromium` using PowerShell while online. Set
`PLAYWRIGHT_BROWSERS_PATH` to an ACL-protected absolute browser directory accessible to the
service identity, both during installation and at runtime. Use the generated script from the
same build as the deployed app, and restage after a Playwright package change. Install required
OS browser dependencies while online. Do not substitute a system browser.

Under the actual app OS identity, set `Common__SetupRoot`, `PLAYWRIGHT_BROWSERS_PATH`, and
`ConnectionStrings__DefaultConnection` in its protected service environment. The connection
string must target `AttendTa_Demo` using the dedicated runtime login. Bind the app to a local
HTTP port as well as any other required bindings; start the app normally. From a process running
as the same OS identity with the same environment and deployment build, set
`ATTENDTA_RUNTIME_SQL_LOGIN` to the expected SQL `ORIGINAL_LOGIN()` and
`ATTENDTA_READINESS_LOGIN_URL` to `http://127.0.0.1:<port>/Account/Login`. Run the built app with
`--demo-readiness`. It must exit zero with all four PASS lines. The INFO line explicitly says the
command cannot verify offline status; it appears whether the host is connected or disconnected.
Failures do not print exception
details because connection errors may contain secrets. Investigate securely; do not publish
connection strings.

The four checks are, in order:

1. `runtime SQL identity and audit permissions` — connects with the runtime connection string,
   requires `AttendTa_Demo`, verifies the login identity and the absence of fixed server and
   database roles, rejects any effective server permission other than `CONNECT SQL` and
   `VIEW ANY DATABASE` (this covers direct grants and custom server roles that
   `IS_SRVROLEMEMBER` misses), rejects table-level and column-level `activity_logs` mutation,
   then inserts a system audit row in a transaction which is always rolled back; identity values
   can still advance.
2. `private storage` — requires the six SetupRoot areas, then creates and probes
   `logs/<yyyyMM>` and leaves no probe files behind.
3. `Playwright Chromium PDF rendering` — launches headless Chromium and renders a minimal
   non-empty PDF, proving the staged browser can produce certificate output.
4. `localhost login HTTP 200` — requests the exact localhost login URL without following
   redirects.

The validation checks writability, not ACL confidentiality: independently inspect ACLs and verify
the SetupRoot is outside the deployment tree. A PASS also cannot prove the running service uses
the same settings; the operator must establish that identity and environment equivalence.

Record each result in `docs/demo-host-readiness-evidence.md`. A closed tracking issue is not
proof that this host passed; only dated, host-specific evidence is.

## Benchmark and disconnected evidence

On the demo host, set `ATTENDTA_HASH_SECRET` to a newly generated disposable random value in a
protected process environment. Run the deployed app with `--hash-password-env`, discard stdout
without recording it, and record only the stderr elapsed time. The fixed Argon2id parameters are
`m=19456 KiB, t=2, p=1`. Clear the variable and do not save the disposable secret or hash. Record
the host details listed above, date, parameters, elapsed milliseconds and application revision.

After staging, disconnect the host from the internet, run the readiness command under the actual
service identity and environment while the app is running, and record the date, host identity,
revision, disconnection method, redacted PASS/FAIL output and exit code. Restore connectivity only
after recording the result. Never claim offline readiness from an online run. This repository change
does not itself provision a host or demonstrate offline acceptance.
