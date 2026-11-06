---
id: 14
type: story
title: "Refactor sweep"
parent: epic-events-registration
after: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 15, 16, 17, 18]
hitl: false
risk: low
tracker_id: "43"
remote: "https://github.com/rainyDayssss/AttendTa/issues/43"
tracker_status: backlog
---

# Refactor sweep

## Description

Finale backend-only cleanup of concrete deferred event/registration controller, service and contract findings. Preserve the existing cancellation, seat-allocation and cutoff implementations; no feature rewrite or repeated frontend audit. Frontend walkthrough/polish belongs to 8.12.

## Requirements

Verify: The solution builds with zero warnings, the focused affected regression check passes and no agreed backend finding remains unresolved. Full-suite execution is recorded once by 8.4, not repeated as new test authoring here.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-events-registration/epic-events-registration.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md

## Delivery (2026-11-06)

- Finale backend-only sweep: it closes concrete deferred event/registration controller, service and contract findings and keeps the shipped cancellation, seat-allocation and cutoff implementations. No feature rewrite and no repeated frontend audit; critique and polish belong to 8.12.
- Frontend is already delivered by 3.16–3.18; the single UI walkthrough is 8.12 and backend hardening is 8.13.
- Development check: build with zero warnings plus the focused affected regression; existing CI unchanged. Full-suite execution is recorded once by 8.4 (#77), broad automation is 8.5 (#78), final manual acceptance is 8.6–8.8.
- Final rules unchanged: no agreed backend finding may stay unresolved and no existing guard test is removed.
