---
id: 3
type: story
title: "Database and persistent file storage backup, restore, and redeploy survival"
parent: epic-release-acceptance
covers: [R5]
after: [1, 14, 6.7]
hitl: true
risk: medium
tracker_id: "76"
remote: "https://github.com/rainyDayssss/AttendTa/issues/76"
tracker_status: backlog
---

# Database and persistent file storage backup, restore, and redeploy survival

## Description

An operator captures the SQL database and the SetupRoot tree (uploads, certificates, keys) in one backup bundle, verifies the data survives an application restart and redeploy, and restores the bundle into a disposable location; persistence failures are filed as bugs back to feature epics.

## Requirements

Verify: An operator takes one backup bundle holding the SQL backup and SetupRoot (uploads, certificates, keys); restarts and redeploys the app with SetupRoot intact; restores the bundle into a disposable location; and verifies database rows, file checksums and the presence of the Data Protection keys.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-release-acceptance/epic-release-acceptance.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md

## Delivery (2026-11-06)

- Operations verification of real storage backup/restore/redeploy; it can run in parallel with 8.2 once the host is ready, and human inputs (host access, disposable restore location) remain required.
- 8.14 is the new real-host follow-up to closed #74; this entry consumes its host/storage evidence rather than treating tooling delivery as acceptance.
- This is real persistence acceptance, not a UI build; prototype or in-place-only results do not close the issue.
- Development check: backup/restore checklist plus a focused checksum/key-presence smoke; existing CI unchanged. Missing important test authoring is 8.5 (#78), combined final execution 8.4 (#77), manual 8.6–8.8.
- Operations: no Impeccable requirement; frontend design lives only in the separate slices 6.12/6.13/7.4 and finale 8.12.
- Database rows, file checksums and Data Protection keys surviving restart, redeploy and restore stay final acceptance.
