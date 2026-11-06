---
id: 7
type: story
title: "Member attendance view on My Events"
parent: epic-check-in
covers: [R1, R7, R9]
after: [1, 6, 12, 3.13]
hitl: false
risk: low
tracker_id: "50"
remote: "https://github.com/rainyDayssss/AttendTa/issues/50"
tracker_status: backlog
---

# Member attendance view on My Events

## Description

Replace 4.12's demo attendance source with a real owner-scoped query and fill the agreed attendance fields/partial contract of the existing My Events host. Show valid/invalidated states and Asia/Manila times with independent 25-row history paging. Do not create another card or host, add self-check-in/correction actions or rewrite presentation (R7, attendance part of R1, history paging part of R9).

## Requirements

Verify: A Member opens My Events and sees their valid attendance records with Asia/Manila check-in timestamps; an invalidated record is badged Invalidated with no check-in or correction actions; the attendance history pages at 25 rows with its own page control, independent of the registrations list.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-check-in/epic-check-in.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Delivery (2026-11-06)

- Frontend already delivered by 4.12 (attendance-history partial with its own pager). This issue stays open for the real owner-scoped attendance query and the agreed fields; it fills the delivered contract and adds no second card, host or partial.
- Development check: build plus one focused owner-scope/invalidated-state smoke; existing CI unchanged. Broad automation goes to 8.5 (#78), the full suite runs once at 8.4 (#77), final manual acceptance is 8.6–8.8.
- Final rules unchanged: invalidated records stay visible and badged, no self-check-in or correction action exists, and history paging stays independent of the registrations list.

## Notes

- Removes demo data M-08 (2026-11-06 demo rescope): implement the real source behind the contract the demo provider serves, delete the provider classes and their registration, and set the rows to `removed` with the commit in `demo-mock-register.md`. The screen should not change visibly. No per-ticket Impeccable critique or polish; 8.12 covers it.
