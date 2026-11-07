# Storage survival evidence — 2026-11-08

**Issue #76 remains BLOCKED.** This is preparation, not a real-host acceptance record.
This computer is not the demo host; no backup, restore, stop, restart or redeploy was performed.
Follow [the runbook](storage-backup-restore.md). A closed #150 or local helper test does not
reconcile the still-blocked [host readiness record](demo-host-readiness-evidence.md).
Only the named operator may mark a stage PASS after recording actual redacted evidence.

Do not commit backups, manifests containing private filenames, private files, keys, row contents,
credentials, password hashes, cookies or connection strings. Keep exact destination paths,
key filenames, digests and full logs in an access-controlled evidence store; record aliases and
redacted result summaries here. A missing/failed step stays BLOCKED/FAIL, not N/A or inferred PASS.

## Host, approval and custody

| Field | Value |
|---|---|
| Record date | 2026-11-08 (preparation only) |
| Host / isolated SQL instance / OS / SQL version | BLOCKED — real host not supplied |
| Operator / approver / UTC maintenance window | BLOCKED — host operation not authorized |
| Readiness reconciliation | BLOCKED — no dated real-host output; #150 closure is not evidence |
| Source DB / runtime identity / external root alias | BLOCKED — actual service settings not verified |
| Source full deployed revision / artifact ID | BLOCKED — no deployed host revision recorded |
| Redeploy old/new full revisions / artifacts | BLOCKED — not run |
| Backup bundle alias / ACL and encryption inspection | BLOCKED — no destination approved |
| New disposable DB / empty root / new SQL file aliases | BLOCKED — destinations and restore permission not supplied |
| Key protector / identity recovery prerequisites | BLOCKED — host key protection not inspected |
| Restricted evidence location / custodian / retention deadline | BLOCKED — no operator custody record |
| Downtime, writer inventory and quiescence start/end UTC | BLOCKED — host exercise not scheduled |

## Baseline and comparison worksheet

For every stage record date/operator, exact revision, DB/root aliases and restricted evidence ID.
Record every table's aggregate count, certificate status counts, reference total/present/missing/
unsafe counts per area, private-tree file/directory counts, nonempty key count, manifest and `.bak`
SHA-256 custody references. Counts alone do not prove record contents: attach redacted representative
data/access outcomes. No issued PDF or image baseline means that area remains BLOCKED.

| Check | Captured baseline | Restart | Redeploy | Disposable restore before/after start |
|---|---|---|---|---|
| UTC / operator / full revision / evidence ID | BLOCKED | BLOCKED | BLOCKED | BLOCKED |
| All canonical table-name/count pairs | BLOCKED | BLOCKED | BLOCKED | BLOCKED |
| Certificate-status aggregates | BLOCKED | BLOCKED | BLOCKED | BLOCKED |
| Venue reference total / present / missing / unsafe | BLOCKED | BLOCKED | BLOCKED | BLOCKED |
| Event reference total / present / missing / unsafe | BLOCKED | BLOCKED | BLOCKED | BLOCKED |
| Certificate reference total / present / missing / unsafe | BLOCKED | BLOCKED | BLOCKED | BLOCKED |
| Complete-tree manifest comparison / digest custody reference | BLOCKED | BLOCKED | BLOCKED | BLOCKED |
| Uploads / certificates / keys checksum consistency | BLOCKED | BLOCKED | BLOCKED | BLOCKED |
| Nonempty key count / presence / decryptability | BLOCKED | BLOCKED | BLOCKED | BLOCKED |
| Authorized private image and issued PDF access | BLOCKED | BLOCKED | BLOCKED | BLOCKED |
| Representative pre-existing data / session decryption | BLOCKED | BLOCKED | BLOCKED | BLOCKED |
| Explained log/temporary/audit/rotation deltas and approval | BLOCKED | BLOCKED | BLOCKED | BLOCKED |

## Operator execution and redacted results

| Operation / required result | UTC / result / evidence / missing input |
|---|---|
| Approvals, source/destination safety, ACLs and rights verified | BLOCKED — no operator/host/approved destinations |
| All writers stopped; baseline captured consistently | BLOCKED — not run |
| COPY_ONLY SQL backup WITH CHECKSUM succeeds | BLOCKED — not run |
| VERIFYONLY succeeds (not restore proof) | BLOCKED — not run |
| Complete private tree captured; source and copy comparisons pass | BLOCKED — not run |
| Backup hash verified, protected bundle usable | BLOCKED — not run |
| Restart uses same external root and DB; checks completed | BLOCKED — not run |
| Redeploy excludes SetupRoot; checks completed | BLOCKED — not run |
| New DB/root/SQL paths confirmed absent/empty; restore approved | BLOCKED — not run |
| RESTORE WITH MOVE for every file and CHECKDB pass | BLOCKED — not run |
| Restored frozen tree, rows, references, keys match baseline | BLOCKED — not run |
| Isolated restored app permissions/access/decryption pass | BLOCKED — not run |
| Original host writer state recovered and healthy | BLOCKED — not run |
| Cleanup approved; retention and eventual secure disposal recorded | BLOCKED — not scheduled |
| Acceptance decision / owner / unresolved feature defects | BLOCKED — all real-host operations pending; do not close #76 |

## Local preparation verification (not host acceptance)

| Date | Check | Result |
|---|---|---|
| 2026-11-08 | `powershell -NoProfile -File tools/test-storage-manifest.ps1` | PASS — 18 isolated synthetic cases: create/match, no overwrite, altered/missing/unexpected file, missing empty directory, absent keys in both modes, inside-tree manifest, relative/drive-relative/drive-root/missing root, traversal manifest, nested/root reparse rejection, final match. No real private tree or DB accessed. |
| 2026-11-08 | Initial local smoke | FAIL — hashtable `keys` entry shadowed `.Keys`; fixed with `get_Keys()` and rerun passed. Synthetic failure evidence retained under local approved temp storage; no host data involved. |
| 2026-11-08 | `dotnet build` | PASS — full solution, 0 warnings / 0 errors. Initial 120-second invocation timed out; rerun with 300-second limit completed in 3m45s. No CI changes. |

The helper proves byte/tree comparison guards on fixtures only. It does not create SQL backups,
establish service permissions, prove key decryption, validate referenced DB paths, or demonstrate
restart/redeploy/restore acceptance. Failures on the host must identify the owning feature epic;
persistence defects are not fixed by weakening these checks.
