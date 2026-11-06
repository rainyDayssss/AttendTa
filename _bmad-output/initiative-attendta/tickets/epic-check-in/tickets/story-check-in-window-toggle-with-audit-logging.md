---
id: 2
type: story
title: "Check-in window toggle with audit logging"
parent: epic-check-in
covers: [R3, R8]
after: [3.1, 3.12, 3.16, 11]
hitl: false
risk: low
tracker_id: "45"
remote: "https://github.com/rainyDayssss/AttendTa/issues/45"
tracker_status: backlog
---

# Check-in window toggle with audit logging

## Description

Adds SetCheckInWindowAsync to e3's IEventLifecycleService (with e3's review) and a Management command that opens or closes Check-in for a Published Event at any time as a row action on the Management event list from epic-events-registration entry 1 (3.1). The command runs in one IUnitOfWork.ExecuteInTransactionAsync under the Event lock (AD-4), sets is_check_in_open and its actor columns, and appends one audit entry per toggle (event.check_in_opened / event.check_in_closed, AD-7) (delivering R3 and the check-in toggle part of R8).

## Requirements

Verify: An Officer opens Check-in on a Published Event and sees it Open with actor and time shown, plus one event.check_in_opened Activity Log entry; closing it records one event.check_in_closed entry; a toggle on an unpublished, cancelled or completed event is refused and leaves the flag and the log unchanged.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-check-in/epic-check-in.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Delivery (2026-11-06)

- Frontend control already delivered by 4.11 (scanner window open/paused control) with 3.16's dossier row action. This issue stays open for the real `SetCheckInWindowAsync` command and its audit entries; it wires the delivered control and adds no second toggle surface.
- Development check: build plus one focused open/close/refused smoke; existing CI unchanged. Broad automation goes to 8.5 (#78), the full suite runs once at 8.4 (#77), final manual acceptance is 8.6–8.8.
- Final rules unchanged: one audit entry per toggle, and a toggle on an unpublished, cancelled or completed event is refused with the flag and log unchanged.

## Notes

- Removes demo data M-04 (2026-11-06 demo rescope): implement the real source behind the contract the demo provider serves, including the shared check-in control partial and the open-flag overlay on event reads, delete the provider classes and their registration, and set the rows to `removed` with the commit in `demo-mock-register.md`. The screen should not change visibly. No per-ticket Impeccable critique or polish; 8.12 covers it.
