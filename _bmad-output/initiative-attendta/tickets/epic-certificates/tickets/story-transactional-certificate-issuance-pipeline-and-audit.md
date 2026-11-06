---
id: 7
type: story
title: "Transactional certificate issuance pipeline and audit"
parent: epic-certificates
covers: [R3, R5, R7, R8]
after: [2, 3, 5, 6, 1.3, 1.6, 1.9, 2.2]
hitl: false
risk: high
tracker_id: "67"
remote: "https://github.com/rainyDayssss/AttendTa/issues/67"
tracker_status: backlog
---

# Transactional certificate issuance pipeline and audit

## Description

Wire 6.12's 25-row paged issuance queue/control to the real query and use case: pre-check issued state outside locks; render using the existing fixed template with a unique number; write/promote through IPrivateFileStore; then in one caller-owned transaction lock owner user, event and certificate, recheck approval, Valid Attendance and Completed status, and save certificate_number, retained snapshots, issuer_name_snapshot, relative file_path, issuer/time and certificate.issued audit. Rendering/file I/O stays outside locks. Failed commit deletes the new file; retry never replaces an issued PDF. Implement the invalidation guard from 6.5 before enabling issuance. Critical real-SQL race authoring is centralized in 8.5, execution in 8.4; use a PDF test double for lock tests and the installed Chromium for renderer checks.

## Requirements

Verify: Focused development check: one eligible issuance persists the retained PDF and snapshots/audit, and a duplicate attempt leaves its bytes unchanged. Finale cases CERT-01/02 in finale-test-cases.md must prove exactly one issuance under concurrent requests and a safe invalidation-versus-issuance outcome before release.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-certificates/epic-certificates.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Delivery (2026-11-06)

- Real issuance consumes 6.12's issuance control; it does not construct a second screen or change presentation. Demo issuance does not close the functional issue.
- Issuance must wait for the 6.5 invalidation guard and the existing 6.3 signatory before it is enabled.
- Development check: build plus a focused issuance/duplicate guard smoke. CI now installs Chromium; use the PDF test double for SQL race/lock tests and the real renderer for the renderer smoke. Existing CI unchanged.
- Missing important test authoring is 8.5 (#78), combined final execution 8.4 (#77), manual 8.6–8.8.
- Backend-only: no per-ticket design critique or polish requirement; frontend design lives only in the separate slices 6.12/6.13/7.4 and finale 8.12.
- The transaction/lock order, outside-lock rendering, failed-commit cleanup, no-replace retry and one-issuance race stay final acceptance even while test authoring is deferred.

## Notes

- Removes demo data M-11a (2026-11-06 demo rescope): implement the real source behind the contract the demo provider serves, delete the provider classes and their registration, and set the rows to `removed` with the commit in `demo-mock-register.md`. The screen should not change visibly. No per-ticket Impeccable critique or polish; 8.12 covers it.
