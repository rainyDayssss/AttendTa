---
id: 2
type: story
title: "Completed-event no-shows and signup snapshot attendee distributions"
parent: epic-participation-reports
covers: [R1]
after: [1, "3.11", "3.12"]
hitl: false
risk: medium
tracker_id: "72"
remote: "https://github.com/rainyDayssss/AttendTa/issues/72"
tracker_status: backlog
---

# Completed-event no-shows and signup snapshot attendee distributions

## Description

Extends the entry 1 report view to calculate Completed-only no-shows from ParticipationFacts (excluding pending, waitlisted, and cancelled registrations from expected attendance and no-shows; for a cancelled Event, no-shows are not shown because the No-show fact requires Completed and the rate follows AD-5 as N/A, while status counts remain visible), and derive actual-attendee course and year distributions strictly from signup snapshots while preserving inactive member history.

## Requirements

Verify: View a completed event and verify the no-show count equals the Confirmed registrations with no Valid Attendance (no-show fact from ParticipationFacts), with pending, waitlisted and cancelled rows excluded; view a cancelled event to confirm no-shows are omitted and rate follows AD-5; and verify course and year distributions reflect actual attendees using their signup snapshots, verifying inactive accounts/members from seeded inactive users/members rows in the test database without waiting on epic-administration.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-participation-reports/epic-participation-reports.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Delivery (2026-11-06)

- Extends the real 7.1 report reads behind the 7.4 frontend contract; it does not construct a second report screen or change presentation, and mock figures do not close the functional issue.
- No-shows and distributions derive from canonical ParticipationFacts and signup snapshots; 8.11 proves the real data end to end.
- Development check: build plus a focused completed/cancelled no-show and distribution smoke; existing CI unchanged. Missing important test authoring is 8.5 (#78), combined final execution 8.4 (#77), manual 8.6–8.8.
- Backend-only: critique and polish belong to 8.15 and 8.12; frontend design lives only in the separate slices 6.12/6.13/7.4 and finale 8.12.
- SPEC v2.1 precedence over the unresolved R30 rate wording is preserved; do not invent a PM override. The Completed-only no-show and inactive-history rules stay final acceptance even while test authoring is deferred.

## Notes

- Removes demo data M-13b (2026-11-06 demo rescope): implement the real source behind the contract the demo provider serves, delete the provider classes and their registration, and set the rows to `removed` with the commit in `demo-mock-register.md`. The screen should not change visibly. No per-ticket Impeccable critique or polish; 8.12 covers it.
