---
id: 3
type: story
title: "Paged participant detail rows with full-dataset totals"
parent: epic-participation-reports
covers: [R1, R2]
after: [2, "1.4", "1.6", "4.6"]
hitl: false
risk: low
tracker_id: "73"
remote: "https://github.com/rainyDayssss/AttendTa/issues/73"
tracker_status: backlog
---

# Paged participant detail rows with full-dataset totals

## Description

Extends the report view with server-side 25-row paged participant detail rows with FR-30 fields (name, student number, snapshot course, registration status, attendance result, Manila check-in time), ordered by participant name, then registration_id, so page boundaries are stable (AD-13), while preserving full-dataset summary totals and entry 2 attendee distributions across pages; needs the shared 25-row pager component from epic-platform-access.

## Requirements

Verify: Navigate through 25-row pages of participant detail rows on a completed event, verifying columns show name, student number, snapshot course, status, attendance result, and Manila check-in time, with stable paging across boundaries, while full-dataset totals and distributions remain unchanged.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-participation-reports/epic-participation-reports.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Delivery (2026-11-06)

- Extends the real 7.1/7.2 report reads behind the 7.4 frontend contract; it does not construct a second report screen or change presentation, and mock figures do not close the functional issue.
- Detail paging uses the shared 25-row pager and a stable unique-id tiebreak while totals and distributions stay full-dataset; 8.11 proves the real data end to end.
- Development check: build plus a focused paging/totals smoke; existing CI unchanged. Missing important test authoring is 8.5 (#78), combined final execution 8.4 (#77), manual 8.6–8.8.
- Backend-only: critique and polish belong to 8.15 and 8.12; frontend design lives only in the separate slices 6.12/6.13/7.4 and finale 8.12.
- SPEC v2.1 precedence over the unresolved R30 rate wording is preserved; do not invent a PM override. The stable-paging and full-dataset-total rules stay final acceptance even while test authoring is deferred.

## Notes

- Summary/distribution query work starts earlier; this final real-detail integration verifies actual invalidation effects from 4.6, pinning the epic's attendance handoff without gating the frontend or initial report reads.
- Removes demo data M-13c (2026-11-06 demo rescope): implement the real source behind the contract the demo provider serves, delete the provider classes and their registration, and set the rows to `removed` with the commit in `demo-mock-register.md`. The screen should not change visibly. No per-ticket Impeccable critique or polish; 8.12 covers it.
