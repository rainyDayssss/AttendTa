# Demo storage survival: guarded operator runbook

**Preparation only — issue #76 remains open.** This computer is not the demo host. Do not
execute host operations until a named operator obtains the approvals below. Read
[host setup](demo-host-setup.md), reconcile [readiness evidence](demo-host-readiness-evidence.md)
with the actual host, and fill [survival evidence](storage-survival-evidence.md).
Closed issues (#150 included), a build, local fixtures and `RESTORE VERIFYONLY` are not
proof of a successful restore or persistence acceptance.

## 1. Preflight and authorization (STOP if any item is missing)

- Record dedicated isolated host/SQL instance, operator, UTC maintenance window, approver,
  downtime permission, exact service name/identity, source database **AttendTa_Demo**, deployed
  full Git revision and deployment artifact identity. Never use production/shared instances,
  `AsiBasecodeDB`, or reset/provision scripts. This procedure reuses the existing demo DB.
- Record the **actual running service's** protected connection settings and external absolute
  `Common__SetupRoot` privately. In tracked evidence use approved redacted aliases; never put
  connection strings, credentials, password hashes, row contents or private paths in logs.
  Confirm SetupRoot is outside wwwroot, deployment, staging and replacement/rollback trees.
- Obtain separate approval for a uniquely named, **nonexistent** protected backup bundle,
  a **new** disposable database (`AttendTa_Restore_<approved-run-id>`), and a **new** empty
  external restore root and new SQL data/log file paths. Record all exact destinations in
  restricted operator notes, with aliases in tracked evidence. Existing DB or populated root:
  **STOP**, choose new approved destinations; never overwrite or empty them.
- Confirm free disk space for database backup, full tree, restore and failed evidence; ACLs,
  encryption at rest, secure transfer, retention expiry and authorized custodians. The entire
  bundle, manifests and operator logs are sensitive (account data, names and key material).
  Do not commit them or upload them to issues. Check inherited ACLs, not just directory names.
- DBA backup/restore authority is distinct from the restricted application SQL login.
  Use an authorized DBA session for BACKUP/RESTORE, never elevate the runtime identity.
  The **SQL Server service OS identity** needs write access to the backup destination and read
  access for restore; the restore instance's service identity needs its new data/log directories.
  The operator needs private-tree read/copy rights. Do not grant these via broad Everyone ACLs.
  Restore service-account database mappings carefully; never grant db_owner to the app.
- Confirm the original deploy artifact, environment and service configuration are recoverable
  from approved secret/config stores. Record key protection mechanism: keys may be encrypted
  with DPAPI/certificate or external providers. File presence alone cannot establish use on
  another machine/account; arrange the same identity/protector or approved recovery material.
  Do not export private keys into tracked evidence. Host or protector uncertainty blocks restore.
- Reconcile readiness checks with actual operator output. Identify all writers: web service,
  workers, schedulers, other instances, maintenance tasks and DB clients. Disable their writes
  for coordinated capture; a SQL snapshot alone cannot freeze uploads/certificates.

## 2. Quiesce and record the baseline

Record UTC start, obtain the go-ahead, stop the **recorded** service and all writers using the
host's approved service procedure, and confirm no process is writing DB or SetupRoot. Keep them
stopped until both captures and comparisons finish. Do not delete temporary files, logs, old
keys, uploads or certificates to make the bundle smaller. If writes cannot be stopped, STOP.

In a private read-only DBA session against `AttendTa_Demo`, collect counts for **every** canonical
table (including lookups), plus status aggregates and reference totals. Example aggregate query:

```sql
IF DB_NAME() <> N'AttendTa_Demo' THROW 51000, 'Wrong source database', 1;
SELECT N'users' AS area, COUNT_BIG(*) AS rows FROM dbo.users
UNION ALL SELECT N'members', COUNT_BIG(*) FROM dbo.members
UNION ALL SELECT N'events', COUNT_BIG(*) FROM dbo.events
UNION ALL SELECT N'registrations', COUNT_BIG(*) FROM dbo.registrations
UNION ALL SELECT N'attendance', COUNT_BIG(*) FROM dbo.attendance
UNION ALL SELECT N'certificates', COUNT_BIG(*) FROM dbo.certificates
UNION ALL SELECT N'activity_logs', COUNT_BIG(*) FROM dbo.activity_logs;
SELECT certificate_status_id, COUNT_BIG(*) AS rows
FROM dbo.certificates GROUP BY certificate_status_id;
SELECT N'venue references' AS area, COUNT_BIG(*) AS references_count FROM dbo.venues
UNION ALL SELECT N'event references', COUNT_BIG(*) FROM dbo.events WHERE image_path IS NOT NULL
UNION ALL SELECT N'certificate references', COUNT_BIG(*) FROM dbo.certificates WHERE file_path IS NOT NULL;
```

Supplement the example with `COUNT_BIG(*)` for `account_requests`, `event_types`, `venues` and all
lookup tables in `database/attendta-mvp-schema.sql`; record table-name/count pairs only. Counts
are not row identity/content proof: also record redacted results of approved representative
member/event/registration/attendance views and authorized venue/event image and issued PDF
access, without screenshots containing personal data. No certificate data/issued PDF? Mark that
coverage BLOCKED and arrange approved representative data through its owning feature, not here.

For referenced-file coverage, read `venues.image_path`, non-null `events.image_path` and
non-null `certificates.file_path` in a private read-only process; **do not export the path list**.
Resolve each against the actual SetupRoot, reject absolute paths, `..`, alternate streams and
any escape from the root, and require respectively `uploads/venues/`, `uploads/events/`, and
`certificates/` prefixes. Require a regular existing file for every reference (duplicate references
count separately). Record only each area's total, present, missing and unsafe counts. Any missing
or unsafe reference blocks capture acceptance; route the defect to its feature owner. Repeat
this exact coverage check against each later stage's DB/root, not just the backup files.

## 3. Capture a coordinated protected bundle

Create the approved new bundle once, restricted **before** copying. Use subdirectories
`sql/` and `SetupRoot/`, with manifests at the bundle top level (outside both scanned trees).
Keep the source quiesced throughout. No source paths may be reparse points; the helper refuses
them including ancestors, rather than following links outside the protected tree.

In the authorized DBA session, replace the reviewed literal below with the new **SQL-service-
visible** absolute path. Check the file does not exist from the SQL service's perspective
immediately before running; use a unique filename and no concurrent backup job there. With
remote SQL Server, the SQL path is not the operator workstation's path.

```sql
IF DB_NAME() <> N'AttendTa_Demo' THROW 51000, 'Wrong source database', 1;
BACKUP DATABASE [AttendTa_Demo]
  TO DISK = N'<approved-new-bundle-sql-path>\AttendTa_Demo.bak'
  WITH COPY_ONLY, CHECKSUM, STOP_ON_ERROR;
RESTORE VERIFYONLY
  FROM DISK = N'<same-approved-new-backup-path>' WITH CHECKSUM, STOP_ON_ERROR;
```

Use `sqlcmd -b` (or a DBA client configured to stop on errors), check its nonzero exit immediately,
and retain redacted completion/VERIFYONLY results. No `INIT`, `FORMAT`, `CONTINUE_AFTER_ERROR`
or `REPLACE`. COPY_ONLY avoids disturbing the normal backup chain. VERIFYONLY validates backup
readability/structure, **not** an actual usable restore, referential file coverage or keys.

While still quiesced, create a source manifest **outside SetupRoot and outside its copy**:

```powershell
# All variables below must be absolute, operator-approved paths, not repository paths.
powershell -NoProfile -File tools/storage-manifest.ps1 -Mode Create -Root $sourceRoot -Manifest $sourceManifest
if ($LASTEXITCODE -ne 0) { throw 'Source manifest failed; STOP' }
robocopy $sourceRoot $bundleRoot /E /COPY:DAT /DCOPY:DAT /R:0 /W:0 /XJ /NFL /NDL /NJH /NJS /NP
if ($LASTEXITCODE -ge 8) { throw 'Copy failed; STOP' }
powershell -NoProfile -File tools/storage-manifest.ps1 -Mode Verify -Root $sourceRoot -Manifest $sourceManifest
if ($LASTEXITCODE -ne 0) { throw 'Source changed; STOP' }
powershell -NoProfile -File tools/storage-manifest.ps1 -Mode Verify -Root $bundleRoot -Manifest $sourceManifest
if ($LASTEXITCODE -ne 0) { throw 'Copy mismatch; STOP' }
```

Here `$bundleRoot` means the new bundle's `SetupRoot/`, NOT its top level. Robocopy's 0–7 codes
are nonfatal but do not prove equivalence: both manifest comparisons must pass. Preserve the
manifest in the protected bundle and record its own SHA-256 in restricted custody notes.
Hash the completed `.bak` with `Get-FileHash -Algorithm SHA256` and preserve that digest alongside
it; recheck after transfer and before restore. Confirm the bundle contains the SQL backup and
**all** SetupRoot areas: `keys`, `logs`, `temporary`, venue/event uploads and certificates,
including empty directories/hidden files. Enforce destination ACLs explicitly; `/COPY:DAT` does
not copy source ACLs. The helper hashes regular file bytes, not ACLs, alternate data streams,
key decryptability or DB rows. Use only ordinary local files, no EFS/cloud-placeholder/ADS storage
unless the DBA separately validates a full fidelity recovery procedure; otherwise STOP.

Record number of nonempty `keys/key-*.xml` files and comparison success, never XML contents.
Do not call a partial bundle usable. On any copy/SQL/hash failure preserve the failed bundle,
record FAIL, and use a new approved bundle for another attempt. Do not overwrite a manifest.

## 4. Restart and redeploy checks (same source DB and external root)

Only after capture success, start the original revision with unchanged protected settings and
identities. Record UTC times, exact revision, startup/health result, baseline aggregates,
referenced-file coverage and authorized image/PDF access. Confirm pre-existing data is visible.
Confirm a pre-restart disposable authorized session can still be decrypted (within its normal
expiry), without recording cookie values. Stop writes again and compare the root to the baseline.

Deploy the approved artifact using the approved downtime procedure. **Replace only deployment
files, never SetupRoot or SQL data**. Record old/new full revisions and artifact IDs. Keep
connection string, external root, service identity and key protector unchanged. Restart, repeat
the same DB/reference/access/session checks, quiesce and compare again.

Run the helper's full comparison at each stopped stage and retain redacted outcomes. Logs and
temporary files may legitimately change after starting; record every such difference privately
with cause and approval, not as an unqualified checksum PASS. Compare all baseline uploads,
certificates and keys exactly; missing/changed private artifacts block acceptance. Unexpected
keys (rotation) require explanation and proof old protected data still decrypts; never delete
old keys to get a match. If a full-tree exact match is claimed, it must really pass; retain original
and stage manifests rather than silently rebasing/ignoring failures. Row-count changes from audit
or planned probes likewise require recorded, explained deltas, not edits to append-only logs.

## 5. Restore to new disposable targets only

Obtain explicit restore go-ahead. Preserve source DB/root and bundle unchanged. In the isolated
instance verify the exact approved `AttendTa_Restore_<run-id>` DB is **absent** in `sys.databases`.
Inspect `RESTORE HEADERONLY` and `RESTORE FILELISTONLY` for the captured `.bak` (DB name must
be `AttendTa_Demo`, expected backup set/time). Plan a unique new `MOVE` destination for **every**
returned data/log file; require every physical file absent and no directory link. Do not assume
there are just two files. Have the DBA verify service access/free space and absence immediately
before restore. No races with another operator/restore job are permitted.

Reviewed DBA template (replace every placeholder; do not run unchanged):

```sql
USE master;
IF N'<approved-new-db>' NOT LIKE N'AttendTa[_]Restore[_]%'
   OR DB_ID(N'<approved-new-db>') IS NOT NULL
   THROW 51000, 'Destination not new/approved', 1;
RESTORE DATABASE [<approved-new-db>]
 FROM DISK = N'<approved-backup-path>'
 WITH MOVE N'<logical-data-name>' TO N'<new-data-file-path>',
      MOVE N'<logical-log-name>' TO N'<new-log-file-path>',
      CHECKSUM, STOP_ON_ERROR, RECOVERY;
-- Add MOVE for every additional FILELISTONLY file before running.
DBCC CHECKDB ([<approved-new-db>]) WITH NO_INFOMSGS;
```

Check every SQL exit/result; no `WITH REPLACE`, existing DB, reset, trigger disabling, or audit
UPDATE/DELETE. A failed restore is retained for investigation, not overwritten on retry.

Create the approved new empty external restore root (refuse any existing populated directory).
Apply restrictive ACLs, copy the bundle's full SetupRoot with the same copy/error procedure,
then verify it against the captured source manifest **before starting an app**; all files and
directories must match, keys included. Verify backup SHA-256, restored counts/status aggregates
and referenced-file coverage against the quiesced baseline. Run CHECKDB successfully.

Use a separate isolated app instance/port and restricted disposable runtime identity configured
for **only the new DB/root**, same deployed revision and compatible key protector. Keep background
writers disabled; do not point it at the source. A same-host restore must not reconfigure the live
service. Validate permissions using the provisioning guide's effective-login/audit-denial checks
(the readiness CLI deliberately requires AttendTa_Demo and cannot simply target the restore name).
Do not broaden privileges or rename the DB merely to satisfy that CLI. Verify authorized private
images/issued certificate PDF and Data Protection decryption in the restored app, then record all
legitimate audit/log deltas after startup. Key presence without a successful decrypt is BLOCKED.

## 6. Recovery, retention and cleanup

On mismatch, stop the disposable instance or attempted redeploy, preserve failed output privately,
record the missing input/FAIL and feature owner. Do not modify baseline records, audit history,
source DB/root, backup or keys to force agreement. If restart/redeploy fails, obtain approval to
roll back **deployment files only** to the recorded artifact using the same DB/root; incompatible
schema changes require a separate DBA recovery decision, never an in-place restore here.

Restore the original service/writer state only with operator permission; record time and health.
Keep acceptance BLOCKED until each real-host stage passes and deviations are resolved. Cleanup
is a separate approval: name exact disposable DB/root/SQL files/bundles, verify nothing uses them,
and follow the authorized DBA/secure-file retention procedure. Never broad-delete paths, drop the
source, delete key material from the running root or remove failure evidence to hide a mismatch.
Retain the protected successful bundle until restore acceptance and the agreed retention deadline;
then record authorized disposal. Only redacted aggregate outcomes belong in the repository.
