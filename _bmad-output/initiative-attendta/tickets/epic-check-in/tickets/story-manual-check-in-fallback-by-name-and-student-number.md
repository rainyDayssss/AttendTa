---
id: 5
type: story
title: "Manual check-in fallback by name and student number"
parent: epic-check-in
covers: [R5, R8, R9]
after: [3, 11]
hitl: false
risk: medium
tracker_id: "48"
remote: "https://github.com/rainyDayssss/AttendTa/issues/48"
tracker_status: backlog
---

# Manual check-in fallback by name and student number

## Description

Allows Management to search Confirmed participants by name or student number with 25-row pagination (AD-13) in the scanner window (EXPERIENCE.md: manual lookup lives in the Scanner window) and execute check-in recording manual_search or student_number under the entry 3 validation and lock rules (AD-4), appending an attendance.recorded Activity Log entry (delivering R5, the manual audit part of R8, and the search paging part of R9).

## Requirements

Verify: An Officer searches confirmed participants by student number or name on an open event, sees a 25-row paged list, selects an attendee and clicks Check in, creating an Attendance Record with check_in_method manual_search or student_number; searching or viewing alone never inserts an attendance row.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-check-in/epic-check-in.md
- docs/DESIGN.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Delivery (2026-11-06)

- Frontend-facing integration: the manual-lookup surface and its separate manual adapter are already delivered by 4.11. This issue wires the real manual adapter to the engine (4.3) and does not depend on the camera adapter 4.4.
- No per-ticket Impeccable critique or polish; 8.12 covers it.
- Development check: build/CSS plus one lookup and one explicit check-in; searching or viewing must not write. Existing CI unchanged; broad cases are 8.5 (#78), the full suite runs once at 8.4 (#77), final manual acceptance is 8.6–8.8.
- Final rules unchanged: 25-row paging, `manual_search`/`student_number` methods, and the same engine validations as a scan.

## Notes

- Wire the separate manual adapter that 4.11 built; it does not depend on 4.4. Verify one lookup and explicit check-in; searching never writes. Broad cases belong to the finale.
- Removes demo data M-06 (2026-11-06 demo rescope): implement the real source behind the contract the demo provider serves, delete the provider classes and their registration, and set the rows to `removed` with the commit in `demo-mock-register.md`. The screen should not change visibly. No per-ticket Impeccable critique or polish; 8.12 covers it.
