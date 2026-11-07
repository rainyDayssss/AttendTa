---
id: 12
type: story
title: "Event completion closing check-in"
parent: epic-events-registration
covers: [R5, R14]
after: [11, 16]
hitl: false
risk: low
tracker_id: "41"
remote: "https://github.com/rainyDayssss/AttendTa/issues/41"
tracker_status: done
---

# Event completion closing check-in

## Description

Completes a Published Event at or after its start (injected `TimeProvider`, refused before start) under the event lock through `IEventLifecycleService`, setting status to Completed, clearing `is_check_in_open` (with `check_in_closed_by/at` as in entry 11), recording `completed_by` and `completed_at`, and writing an `event.completed` entry; this entry declares `IEventCompletionHandler` in Services and invokes every registered implementation (`IEnumerable`) inside the same transaction, with an empty list valid until epic-certificates registers one.

## Requirements

Verify: An officer completes an Event at or after its start time, observing status Completed, check-in closed and an Activity Log entry; an attempt before start is refused; and a test-double `IEventCompletionHandler` registered in IntegrationTests runs once inside the completion transaction, and its exception rolls completion back.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-events-registration/epic-events-registration.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Delivery (2026-11-06)

- Frontend already delivered by 3.16 (officer dossier, roster and completion dialog). This issue stays open for the real completion backend: the event lock, status and check-in closure, `completed_by/at` and the `IEventCompletionHandler` invocation. It adds no competing UI host or service.
- Issue #41 needs 3.16 merged first, so the delivered completion control calls this real command instead of the demo data.
- Development check: build plus one focused completion/rollback smoke (a throwing handler rolls the completion back); existing CI unchanged. Broad automation goes to 8.5 (#78), the full suite runs once at 8.4 (#77), final manual acceptance is 8.6–8.8.
- Final rules unchanged: completion only at or after start, check-in closed, an empty handler list valid until epic-certificates registers one, and every registered handler runs inside the completion transaction.

## Notes

- Removes demo data M-09 (2026-11-06 demo rescope): implement the real source behind the contract the demo provider serves including the Completed status overlay on event reads (register rule 7), delete the provider classes and their registration, and set the rows to `removed` with the commit in `demo-mock-register.md`. The screen should not change visibly. No per-ticket Impeccable critique or polish; 8.12 covers it.
