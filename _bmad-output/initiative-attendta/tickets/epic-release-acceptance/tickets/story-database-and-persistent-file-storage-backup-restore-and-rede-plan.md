---
title: 'Database and persistent storage survival verification'
type: 'chore'
ticket: '3'
created: '2026-11-08'
status: 'blocked'
blocked_reason: 'Preparation implemented and reviewed; real-host matrix acceptance deferred by user. Requires demo host, operator approvals and new disposable restore targets.'
baseline_revision: '8582f2f9b7969628be3fa4f4778862a301fb7fce'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: ['{project-root}/docs/demo-host-setup.md', '{project-root}/docs/demo-host-readiness-evidence.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Issue #76 requires real evidence that database rows, private uploads, certificates and Data Protection keys survive restart, redeploy and a coordinated restore. Provisioning tooling and closed issues alone do not establish persistence acceptance.

**Approach:** Prepare a guarded operator runbook, checksum/key-presence checks and dated evidence checklist. The actual demo-host backup, restart, redeploy and restore exercise is deferred to the host operator; this preparation does not complete issue #76.

**Decision (2026-11-08):** The user confirmed this computer is not the demo host and approved preparing instructions and checks here while real-host verification remains pending. Do not run database backup/restore or stop/redeploy an application in this session. Host identifiers, downtime permission and disposable destinations are prerequisites for the later operator run, not blockers to preparation.

## Boundaries & Constraints

**Always:** Obtain explicit host, maintenance-window and disposable-target approval before operations. Keep SetupRoot outside the deployment tree. Quiesce application writes for the database/file capture; retain the same external SetupRoot through restart and redeploy. Protect the entire bundle as sensitive because it contains account data and Data Protection keys. Record exact deployed revisions, operator, times and redacted outcomes. Preserve append-only audit restrictions.

**Never:** Reset, overwrite or restore over an existing database or populated directory; operate on shared/production databases or the legacy fallback database; commit backups, private files, key material, row contents, credentials or password hashes; treat a local fixture as real-host acceptance; redesign the UI or change CI. Persistence defects belong to their feature epics.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Coordinated capture | Approved host with app writes stopped | One protected bundle contains verified SQL backup and complete SetupRoot tree with checksum manifest | Stop on any SQL or copy error; do not label a partial bundle usable |
| Restart / redeploy | Recorded baseline, same external storage | Rows and referenced private files remain accessible; checksums and key presence match | Record mismatch and file a scoped defect |
| Disposable restore | New approved database and empty external root | Restored row evidence and checksums match captured baseline; keys present | Refuse an existing destination; preserve source and failed evidence |
| Missing inputs | Host, rights, destination or readiness evidence absent | Run remains blocked with the missing input named | No destructive workaround or substitute acceptance |

</frozen-after-approval>

## Code Map

- `docs/demo-host-setup.md` — existing isolated AttendTa_Demo provisioning, private-root layout, identity and secrets rules; reuse rather than reprovision or reset.
- `docs/demo-host-readiness-evidence.md` — all real-host fields and checks currently BLOCKED despite #150 being closed; requires reconciliation with actual operator evidence.
- `database/attendta-mvp-schema.sql` — canonical tables and relative private-file references; use read-only aggregate evidence, not exported personal data.
- `_bmad-output/initiative-attendta/tickets/epic-release-acceptance/tickets/story-demo-host-runtime-environment-provisioning-plan.md` — predecessor explicitly separates tooling from real host verification and leaves host operation unproven.
- Ticket 8.3 — requires host and disposable-location human inputs and real restart/redeploy/restore evidence; no Impeccable requirement.

## Tasks & Acceptance

**Execution:**
- [x] `docs/storage-backup-restore.md` — add explicit preflight, quiescence, SQL BACKUP/VERIFYONLY, complete private-tree capture, protected bundle/manifest, restart/redeploy, new-target RESTORE WITH MOVE, comparison, recovery and cleanup procedures. SQL service access and administrator backup rights must be separate from runtime rights. Do not infer that VERIFYONLY proves a successful restore.
- [x] `docs/storage-survival-evidence.md` — record dated host/operator/revision, approved destinations, baseline row aggregates and referenced-file coverage, checksums, keys, each operation and redacted results. Missing operations remain BLOCKED.
- [x] `docs/demo-host-setup.md` — link the persistence runbook and clarify that external SetupRoot must not be included in deployment replacement.
- [x] `tools/storage-manifest.ps1` — add create/verify modes for a relative-path SHA-256 manifest, requiring key files, refusing reparse points and reporting missing/changed/unexpected files without printing private contents. Keep the manifest outside the scanned tree; never overwrite an existing manifest. This is a file comparison helper, not backup automation.
- [x] `tools/test-storage-manifest.ps1` — run a focused smoke using only newly created temporary fixtures: matching tree, altered/missing/unexpected file, absent keys and unsafe input; preserve failure evidence and report a nonzero exit on failure.
- [x] `docs/storage-survival-evidence.md` — record local smoke results separately from the pending real-host exercise; do not close #76 before all host acceptance checks pass.

**Acceptance Criteria:**
- Given an approved quiesced host, when a bundle is captured, then it contains a verified SQL backup and the complete captured SetupRoot with a checksum manifest and recorded key presence.
- Given recorded baseline rows and private files, when the application restarts and is redeployed without replacing SetupRoot, then database evidence, referenced files, checksums and key presence remain consistent.
- Given a new disposable restore location, when the bundle is restored, then restored database evidence and private-file checksums match the captured baseline and Data Protection keys are present.
- Given missing host evidence or failed comparison, when results are recorded, then acceptance remains blocked and failures identify their owning feature scope.

## Implementation Notes

- User requested global OpenCode subagent settings rather than project model pins. Removed only project model overrides and corrected AGENTS.md accordingly; implementation and quick review used the configured global general model openai/gpt-6.1-sol#medium.
- Orchestrator independently reran the 18-case smoke and full build: both passed, zero build warnings/errors. Matrix audit: helper guards are tested; coordinated SQL capture, restart/redeploy and disposable-restore rows have no executed host test by the approved deferral. Preparation is complete, but the overall story plan remains blocked rather than claiming those criteria are built.

- Prepared the guarded operator runbook, dated blocked host checklist and external-root deployment warning. No host backup/restore or application stop/restart/redeploy was performed; #76 remains pending.
- Added SHA-256 create/verify helper with empty-directory coverage, required nonempty key files, reparse/unsafe manifest guards and atomic no-overwrite creation. Hashes are comparisons only, not ACL, key decryption or database validation.
- Local smoke passed 18 isolated synthetic cases. An initial smoke exposed PowerShell hashtable property shadowing by the `keys` entry; corrected enumeration to `get_Keys()`, preserved failure fixtures and reran successfully. Full `dotnet build` passed with 0 warnings/errors after increasing the timeout from 120 to 300 seconds. Results are recorded separately from host acceptance in the evidence document.
- Operator matrix scenarios involving SQL, restart/redeploy and restored app access remain unexecuted by the approved preparation-only decision. Actual host/operator/destinations, downtime approval, referenced-file coverage and decryptability are still required; no local test substitutes for those acceptance checks.

## Plan Change Log

## Review Triage Log

- Quick review: no findings by inspection; 0 high, 0 medium, 0 low, 0 false, 0 maybe-false. Real-host acceptance remains explicitly deferred, not passed. Other review lenses skipped under the pinned quick configuration.

## Verification

- `dotnet build` — existing solution remains buildable; CI unchanged.
- `powershell -NoProfile -File tools/test-storage-manifest.ps1` — isolated fixtures verify matching files and failure guards; no real private tree or database is accessed.
- Deferred operator exercise: approved source backup, restart, redeploy and restore to a new disposable target; compare row aggregates, referenced-file coverage, SHA-256 checksums and key presence at each stage. Tool success alone does not pass these checks. Real-host acceptance stays pending after preparation.
