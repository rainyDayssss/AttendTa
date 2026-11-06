---
id: 6
type: story
title: "Management certificate review queue and approval decisions"
parent: epic-certificates
covers: [R4, R7, R8]
after: [4, 5, 12, 1.3, 1.6, 1.9]
hitl: false
risk: medium
tracker_id: "66"
remote: "https://github.com/rainyDayssss/AttendTa/issues/66"
tracker_status: backlog
---

# Management certificate review queue and approval decisions

## Description

Implements the 25-row paged review queue on the shared PageRequest and _Pager (AD-13), and the approve and decline decisions: each discovers its candidate, locks the owner's users row and then the event, rechecks pending_approval status and Valid Attendance through ParticipationFacts, and refuses as Stale a candidate that changed; decline displays the SPEC §14.2 warning that the decision cannot be reopened in this release and requires a non-blank reason validated inside the transaction; each decision appends a certificate.approved or certificate.declined entry, delivering R8, R4 and R7.

## Requirements

Verify: An Officer approves a pending review to mark it eligible and declines another with a required reason to make it not_eligible; both append Activity Log entries, a decline without a reason is refused and the queue shows 25 rows per page. A focused fixture-backed guard check proves a changed/non-pending review is refused as Stale without mutation; the actual invalidation-versus-decision flow is verified after 6.5 at the finale.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-certificates/epic-certificates.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Delivery (2026-11-06)

- Real review commands consume the 6.12 ledger contract rather than constructing a second screen; demo decisions do not close the functional issue. Work can start after 6.4 and the 6.12 frontend contract.
- Development check: build plus a focused approve/decline/stale smoke; existing CI unchanged. Missing important test authoring is 8.5 (#78), combined final execution 8.4 (#77), manual 8.6–8.8.
- Backend-only: no per-ticket design critique or polish requirement; frontend design lives only in the separate slices 6.12/6.13/7.4 and finale 8.12.
- Lock order, Valid Attendance recheck, Stale refusal and the final-decline rule stay final acceptance even while test authoring is deferred.

## Notes

- Wire the 6.12 ledger contract with real read/decision sources, not a second screen. Review commands recheck Valid Attendance; 6.5 protects invalidation and must be present before issuance in 6.7.
- Removes demo data M-10b (2026-11-06 demo rescope): implement the real source behind the contract the demo provider serves, delete the provider classes and their registration, and set the rows to `removed` with the commit in `demo-mock-register.md`. The screen should not change visibly. No per-ticket Impeccable critique or polish; 8.12 covers it.
