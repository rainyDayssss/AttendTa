---
id: 6
type: story
title: "Attendance invalidation with confirmation and reason"
parent: epic-check-in
covers: [R6, R8]
after: [3, 5]
hitl: false
risk: medium
tracker_id: "49"
remote: "https://github.com/rainyDayssss/AttendTa/issues/49"
tracker_status: backlog
---

# Attendance invalidation with confirmation and reason

## Description

Implements Management invalidation of an Attendance Record: a POST under the Management policy with antiforgery, in one IUnitOfWork.ExecuteInTransactionAsync that takes the owner's users row then the event (AD-4). The mandatory reason is validated inside the transaction. The use case declares IAttendanceInvalidationHandler in Services and calls every registered implementation before the write, so a Forbidden throw rolls back. It then sets invalidated_at and invalidation metadata on the retained row and appends one attendance.invalidated entry with the reason, with epic-participation-reports (and epic-certificates via the hook) as consumers of invalidated attendance. Refusal of later check-in is entry 3's rule (delivering R6 and the invalidation audit part of R8).

## Requirements

Verify: An Officer invalidates an Attendance Record after entering a reason and confirming the warning; the row remains with invalidated_at set and one attendance.invalidated entry exists; a later check-in for that Registration is refused; with a test-registered IAttendanceInvalidationHandler that throws Forbidden, the row stays valid and no audit entry is written; with no handler registered (before e6 lands), invalidation commits.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-check-in/epic-check-in.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Delivery (2026-11-06)

- Backend command only; the invalidation dialog with reason and confirmation is already delivered by 4.11. This issue wires the real invalidation after the manual/controller handoff (4.5) and adds no second dialog.
- Development check: build plus one focused invalidate/refuse-recheck-in smoke; existing CI unchanged. Broad automation goes to 8.5 (#78), the full suite runs once at 8.4 (#77), final manual acceptance is 8.6–8.8.
- Final rules unchanged: the row is retained with `invalidated_at` and metadata, one `attendance.invalidated` entry with the reason, a throwing handler rolls back, and no registered handler still commits.

## Notes

- Removes demo data M-07 (2026-11-06 demo rescope): implement the real source behind the contract the demo provider serves, delete the provider classes and their registration, and set the rows to `removed` with the commit in `demo-mock-register.md`. From this commit invalidation is real; 6.5's handler later replaces the store's invalidation-to-review rule (register rule 12). The screen should not change visibly. No per-ticket Impeccable critique or polish; 8.12 covers it.
