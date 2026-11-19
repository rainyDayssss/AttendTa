# First-time Windows demo setup

This experimental package is not release-verified. Setup requires Windows, SQL Server
LocalDB and a dedicated private LocalDB instance. The standalone executable extracts
application files and normally opens the launcher; it does not collect passwords. The operator
must establish isolation and restrictive filesystem permissions; the software does not
verify ACLs. Do not use production data or send email to demo addresses.

## 1. Extract the application

In PowerShell, run the standalone executable with `--extract-only`. For example, from
the directory containing it:

```powershell
.\AttendTa-Demo-Standalone.exe --extract-only
```

Record the application directory printed by the program; it is a new temporary
directory. Do not delete it while the app is running. If using the Windows ZIP instead,
extract the entire ZIP to a directory accessible to the operator and runtime identity.
In the commands below, set `$appDir` to that extracted directory.

## 2. Prepare an isolated target and private credentials

Obtain host approval. Independently verify the LocalDB instance is owned by the
operator, unshared, dedicated to this demo, and contains no user databases. Create a
new instance if needed using `SqlLocalDB`; an instance name alone does not establish
isolation. The accepted name is `AttendTaDemo`, or `AttendTaDemo_` followed by exactly
32 hexadecimal characters.

Choose a **new, nonexistent** setup-root child under an existing private parent outside
the extracted application, source checkout, deployment tree and web root. Restrict
access to the operator and runtime identity. Setup refuses an existing root or an
instance containing any user database.

Receive or create a JSON object in a separate private file outside the application and
checkout, with access restricted to the authorized operator. It must contain exactly
these case-sensitive property names, each mapped to a different password string:

`admin`, `officer`, `gabutan`, `taguiam`, `talisic`, `sinco`, `pinca`, `member06`,
`member07`, `member08`, `member09`, `member10`, and `applicant`.

Each password must be 8–128 characters, contain an ASCII letter and a digit, and have
no control characters. Use a proper JSON editor or serializer to escape special
characters. Never put passwords or hashes in command arguments, logs, shared files,
the bundle, or source control. These keys identify credentials, not login addresses.
Store and deliver credentials separately using your approved secure process.

## 3. Provision once

In a protected PowerShell session, substitute paths and the exact instance name; do
not place credentials in these commands. The same instance name must be used for
provisioning and for the launcher later.

```powershell
$appDir = '<extracted application directory>'
$credentialFile = '<private JSON file path>'
$env:ATTENDTA_DEMO_LOCALDB_INSTANCE = '<dedicated instance name>'
$env:ATTENDTA_DEMO_SETUP_ROOT = '<new private setup-root child>'
Get-Content -Raw -LiteralPath $credentialFile | & (Join-Path $appDir 'ASI.Basecode.WebApp.exe') --setup-demo --confirm-dedicated-localdb
```

The confirmation asserts that you verified the dedicated instance; it is not proof
of isolation. A success message does not establish that sign-in, images or scenarios
work. On failure, **stop**: setup can leave a partial database or root, and refuses
retry while either exists. Obtain separate approval and verify the exact target before
any cleanup. The paired-marker uninstall described below does not support unmarked
or incomplete setups.

## 4. Launch and check

Run `AttendTa-Demo.exe` in the extracted directory. Enter the same setup root and the
same dedicated instance name when prompted. The app binds IPv4 loopback port 5000 and
opens `http://127.0.0.1:5000/Account/Login` (also reachable at
`http://localhost:5000` where localhost resolves to IPv4). If the port is occupied,
startup fails; stop or reconfigure the other service only with its owner's approval.
Keep the launcher window open; press Enter there to stop the app. The launcher does not
provision accounts or accept passwords.

When opening the standalone executable normally, or running `AttendTa-Demo.exe`,
the launcher offers options to start an already provisioned demo, identify an old
paired-marker installation for guarded clean reinstall, or see first-time setup
instructions. The launcher cannot discover every existing installation. The
first-time option requires the explicit `INVESTIGATED` acknowledgement after
independent investigation, then displays this guide's full path until Enter is
pressed; it does not provision automatically.
For guided reinstall choose option 2, explicitly select the old root and instance,
the separate credential JSON file, and new backup and journal directories. The
guarded application then displays the exact removal targets and requires its own
offline, target, old-app-removal and data-loss confirmations. Password values are
not passed in arguments or environment variables. If it fails, stop and follow its
recovery message; do not retry blindly. Restart the launcher after a successful
reinstall and choose option 1. A known legacy-path hint blocks startup and requires
independent identification and separately approved manual cleanup first.

The standalone extractor does not support the legacy `--uninstall` option. The
separate application command described below can remove a verified paired-marker
demo database and store after explicit confirmation. It never removes extracted
application files or the separately held credential file.

The launcher checks for the documented v1 location `%TEMP%\AttendTa\app` and blocks
startup if that path exists. The path alone does not establish
that it belongs to AttendTa or which version it contains. Absence of this hint does
not establish that there is no previous installation. This release does not
overwrite or upgrade the old installation. Before considering a manual uninstall,
verify the exact legacy executable and target, back up any needed database, uploads,
keys and credentials, and inspect the old release's uninstall instructions and effects.
Do not run a legacy `--uninstall` until its exact target and consequences are approved.
Extract-only and setup modes do not run this launcher check. Before provisioning, the
operator must independently investigate previous installations; do not use a fresh
setup as a side-by-side replacement. The guarded clean reinstall described below
does not identify or remove old application binaries.

The launcher also checks TCP listeners on port 5000 before startup. A listener might
belong to an unrelated service; identify its owner (for example, with
`Get-NetTCPConnection -State Listen -LocalPort 5000` and `Get-Process -Id <OwningProcess>`)
and obtain approval before stopping or reconfiguring it. The check is advisory and
another process can take the port afterward; startup can still fail. No existing
installation or conflicting process is removed or stopped by this check.
### Read-only paired-marker removal preview

For a v1.1.0 setup with paired ownership markers, an operator can explicitly select
the existing root, LocalDB instance and separate credential handover file. Stop the
app first and independently confirm the instance is dedicated and unshared. In a
protected PowerShell session:

```powershell
$env:ATTENDTA_DEMO_SETUP_ROOT = '<existing private root>'
$env:ATTENDTA_DEMO_LOCALDB_INSTANCE = '<exact dedicated instance name>'
$env:ATTENDTA_DEMO_CREDENTIAL_FILE = '<existing private JSON file outside root and application>'
& (Join-Path $appDir 'ASI.Basecode.WebApp.exe') --preview-demo-removal --confirm-dedicated-localdb
```

The command validates the credential contents without printing them and inspects the
paired markers, database files, and a bounded store inventory. It holds the SQL
application lock during inspection and inventory, but this lock does not prevent
uncooperative processes or filesystem changes. A refusal makes no changes. A successful
snapshot is **not** proof of exclusive ownership or permission to delete; it can go
stale immediately. Credential ACLs and hard-link aliases are not verified. No backup,
uninstall, reinstall, or cleanup is performed. Preserve credentials separately and
obtain an independently verified backup and recovery plan before any manual action.

### Offline backup (experimental)

Close the app and all other writers and arrange an offline maintenance window. The
operator is responsible for confirming the instance is dedicated and unshared and
that no credential handover or other unrelated secrets are in the source store:
the program cannot identify secrets by content. The selected credential file must
be outside both the source and destination. Select a new destination child under an
existing local private parent outside the source, application, and credential file.
The destination is created with a protected ACL for the current Windows user and
LocalSystem. The operator and administrators are trusted; this is not protection
against a malicious process running as either.

```powershell
$env:ATTENDTA_DEMO_BACKUP_ROOT = '<new absolute backup directory>'
& (Join-Path $appDir 'ASI.Basecode.WebApp.exe') --backup-demo --confirm-dedicated-localdb
```

Use the same explicitly selected root, instance and credential file environment
variables shown for preview. At the interactive prompts type `OFFLINE` and the
exact instance name. The command checks the paired markers and holds the setup SQL
application lock while it creates a `COPY_ONLY, CHECKSUM` database backup, runs
`RESTORE VERIFYONLY`, and copies supported store files. It checks source reparse
points and file link counts, compares file checksums, and writes `manifest.json`.
It includes the ownership marker, keys, uploads, certificates and known application
logs under `logs/yyyyMM` (including daily and size-rolled files). Unknown log paths
and any contents in `temporary` cause refusal. Logs may contain sensitive information;
protect the entire backup and do not share unredacted logs. The database backup contains
the old test changes and can contain sensitive data, including password hashes. The
separately selected credential file is not copied. Preserve it separately.

Only a destination with `COMPLETE` is a completed backup; on any failure or
interruption, a new incomplete destination may remain and is never automatically
removed. Never overwrite an existing destination. `RESTORE VERIFYONLY` is not a
test restore. Retain the original installation until recovery into an isolated
target has been verified,
including access to Data Protection keys under the original Windows identity.
The command does not uninstall, reset, or reinstall anything.

### Recover a backup to a new target

Do not recover over the original installation. First independently provision and
verify a **new, unshared, dedicated and empty** LocalDB instance named `AttendTaDemo`
or `AttendTaDemo_` followed by 32 hexadecimal digits. Confirm that it has no user
databases. Choose a **new, nonexistent local** private-root child under an existing
private parent, outside the backup and application. Close the app and all writers;
keep the separately held credentials and the backup in their protected locations.

```powershell
$env:ATTENDTA_DEMO_BACKUP_ROOT = '<existing COMPLETE backup root>'
$env:ATTENDTA_DEMO_RECOVERY_INSTANCE = '<exact empty dedicated LocalDB instance>'
$env:ATTENDTA_DEMO_RECOVERY_ROOT = '<new absolute local private root>'
& (Join-Path $appDir 'ASI.Basecode.WebApp.exe') --recover-demo-backup
```

At the prompts type `OFFLINE`, the exact instance name and the exact new root shown.
The command verifies the backup's inventory, ACLs and hashes, creates the protected
root, restores `AttendTa_Demo`, copies the ownership marker, keys, uploads,
certificates and backed-up application logs, and checks the restored marker and
referenced files. It does not copy or replace the separate credentials.

If the command fails or is interrupted, stop: a partial new database or root may
remain, possibly with `.attendta-recovery-incomplete`. The command does not clean
up partial resources or delete the backup. Inspect the exact reported targets before
any separately approved cleanup or retry. A successful synthetic integration test
does not prove the operator's recovered app works: verify sign-in and required
scenarios under the original Windows identity, including Data Protection key access,
before relying on recovery. Recovery is not an uninstall and does not authorize
deletion of the original installation.

For a read-only check of the backup file inventory, ACLs and manifest checksums,
set `ATTENDTA_DEMO_BACKUP_ROOT` to that backup and run:

```powershell
& (Join-Path $appDir 'ASI.Basecode.WebApp.exe') --verify-demo-backup-files
```

This check does **not** restore SQL or validate that database references resolve.

To test-restore the selected backup, keep the same environment variable and run:

```powershell
& (Join-Path $appDir 'ASI.Basecode.WebApp.exe') --verify-demo-backup-restore
```

This validates the backup files, creates a uniquely named disposable LocalDB instance
and private temporary directory, restores the database, checks the paired marker and
the venue, event and certificate file references against the checked manifest, and
rechecks the backup files. On success it stops and removes only its own instance and
its expected temporary database files. On failure or uncertain cleanup it reports
the instance name and temporary path for inspection; do not delete them without
verifying ownership. It requires the English SqlLocalDB success messages and refuses
to guess if they are not available. A successful restore check is not authorization
to remove the old installation, nor does it verify application behavior, credentials
or whether Data Protection keys are usable under another Windows identity.

### Non-destructive uninstall preflight

With the source, credential and backup environment variables set as above, an
operator can run `ASI.Basecode.WebApp.exe --preflight-demo-uninstall`. The command
shows the selected database, root and backup and requires the exact instance and
root plus `OFFLINE`. It test-restores the selected backup, compares its stored
private files against the current source inventory, checks the paired ownership
markers and checks for other sessions whose current database is the demo database.

**This is not uninstall and does not authorize deletion.** The source SQL lock is
not held throughout backup creation and restore verification, so the preflight
cannot prove that the database backup is current. Its session check cannot detect
every possible client using a different database context. No source files or
database are removed, and extracted application directories are never removed.
If verification cannot clean up its disposable resources, follow the reported
instance and temporary path only after independently verifying their ownership.

For a fresh backup and restore check under one continuously held source ownership
lock, choose a **new** backup destination and run
`ASI.Basecode.WebApp.exe --locked-demo-backup-check` with the same environment
variables. Confirm `OFFLINE`, the exact instance and the exact source root. The
command checks the dedicated instance, paired marker, database paths and current
demo-database sessions before backup; it then creates and test-restores the backup
and rechecks those source conditions and private-file hashes before releasing the
lock. It still performs no source deletion.

The SQL application lock coordinates only cooperating commands, not arbitrary
database clients or filesystem writers. The operator-confirmed offline window is
required throughout. The session check cannot detect every client using another
database context. `COMPLETE` indicates the backup-writing stage completed; it does
not by itself prove that the later restore or locked recheck succeeded. A failure
can leave that backup or reported disposable resources for inspection. This check
alone does not authorize deletion.

### Guarded uninstall of a paired-marker demo

**This permanently deletes the selected database and private root.** Use only for
the v1.1.0 paired-marker setup with a verified dedicated, unshared instance. Close
the app and all writers for the entire operation. The SQL application lock only
coordinates cooperating commands; it does not stop other programs. The operator
and administrators are trusted, and credential files must remain separately held.

Choose two **new, nonexistent** directories under existing private parents, each
outside the source, application and credential file and outside each other:

```powershell
$env:ATTENDTA_DEMO_SETUP_ROOT = '<existing private source root>'
$env:ATTENDTA_DEMO_LOCALDB_INSTANCE = '<exact dedicated source instance>'
$env:ATTENDTA_DEMO_CREDENTIAL_FILE = '<separately held credential JSON>'
$env:ATTENDTA_DEMO_BACKUP_ROOT = '<new protected backup directory>'
$env:ATTENDTA_DEMO_UNINSTALL_JOURNAL_ROOT = '<new protected journal directory>'
& (Join-Path $appDir 'ASI.Basecode.WebApp.exe') --uninstall-demo
```

Review every listed target before typing `OFFLINE`, the exact instance, the exact
displayed source root, and `DELETE DEMO DATA`. A decline makes no uninstall changes.
The command creates a fresh backup and test-restores it while holding the source
ownership lock, then rechecks the source and deletes only the strictly inventoried
database and store. It refuses unknown or changed entries, links, other user
databases and detected active sessions. Only known application log names in
`logs/yyyyMM` are accepted and backed up; unknown log paths and any contents in
`temporary` cause refusal. Do not remove unknown entries without separately verifying
ownership and obtaining approval. It does not force source sessions closed. The isolated
test-restore stops and deletes only the temporary instance it created.

The backup and journal are preserved. The journal records each intended deletion;
an interruption after `DatabaseDropStarting` may mean the database was already
removed even if `DatabaseDropped` is absent. On any failure, **stop and inspect**
the exact reported targets and journal; do not retry blindly. Use the recovery
command above with that backup and a **new** empty dedicated instance and **new**
root, then verify the recovered app. No automatic rollback occurs.

Extracted application binaries and directories are never removed by this command.
They require separately approved manual cleanup after independently verifying
their ownership. A successful uninstall is not an automatic reinstall.

### Guarded clean reinstall

First independently identify the old application binaries and processes, stop them,
and arrange separately approved removal of the old binaries. Run the command from
the **new** extracted application directory. The command refuses the known
`%TEMP%\AttendTa\app` hint and processes named `AttendTa-Demo`,
`AttendTa-Demo-Standalone`, or `ASI.Basecode.WebApp` other than itself and its
verified launcher ancestry. These checks
are not exhaustive: absence of a hint does not prove no old installation or process
exists. Do not attest removal unless you have independently verified it. Unknown
application directories are never automatically deleted.

Choose a **new** protected backup directory and a **new** separate protected journal
directory outside the existing private root, app and credential file. With the same
source, credential, backup and journal environment variables shown for uninstall:

```powershell
& (Join-Path $appDir 'ASI.Basecode.WebApp.exe') --reinstall-demo
```

Review the exact database, root and private-store entries. Enter `OFFLINE`, the
exact instance and root, `OLD APP REMOVED`, and `DELETE DEMO DATA` only after the
checks and approvals above. The same dedicated LocalDB instance and root are reused
**only after** guarded removal succeeds and fresh setup confirms the instance is
empty and root is absent. The command validates the separate credential file and
all bundled images before removal. It creates and restore-verifies a fresh backup,
preserves that backup and the journal, removes the old database/store, and installs
the clean seed. Old test changes remain in the backup; they are **not migrated**.

After seeding, it checks 40 members, eight events, five venues, the 12 presenter
password hashes and applicant hash against the separately held credential file,
and all 13 seeded image files against the bundled images. These are not browser
sign-in or startup tests; launch the application and check the presenter scenarios
before use. If any stage fails, stop and inspect the reported backup, journal,
instance and root. Do not retry blindly: use `--recover-demo-backup` into a new
empty dedicated instance and new root if recovery is required. The original
instance/root may be absent or partially re-created.

Authenticate using the separately supplied credentials and these login addresses:

| Credential key | Login email |
|---|---|
| `admin` | `alexmercado@gmail.com` |
| `officer` | `jamievillareal@gmail.com` |
| `gabutan` | `jerhongabutan@gmail.com` |
| `taguiam` | `jaspertaguiam@gmail.com` |
| `talisic` | `jhontalisic@gmail.com` |
| `sinco` | `johnpaulsinco@gmail.com` |
| `pinca` | `sethpinca@gmail.com` |
| `member06` | `rileysarmiento@gmail.com` |
| `member07` | `drewalonzo@gmail.com` |
| `member08` | `caseyvaldez@gmail.com` |
| `member09` | `julesnavarro@gmail.com` |
| `member10` | `noelsoriano@gmail.com` |

`applicant` belongs to the seeded pending request `applicant@gmail.com` and can sign
in only after approval. Validate logins, images and the presenter scenarios before use.
All identities are fictional; their addresses may belong to real people. Never email
them. Setup does not fabricate issued certificate PDFs; issuance requires the app's
certificate configuration and Chromium.
