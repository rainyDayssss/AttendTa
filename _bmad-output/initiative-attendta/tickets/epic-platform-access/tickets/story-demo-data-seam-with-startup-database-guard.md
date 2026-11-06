---
id: 14
type: story
title: "Demo data seam with startup database guard"
parent: epic-platform-access
after: [9]
hitl: false
risk: medium
tracker_id: "156"
remote: "https://github.com/rainyDayssss/AttendTa/issues/156"
---

# Demo data seam with startup database guard

## Description

Build the demo data seam described in demo-mock-register.md (rows M-01, M-04 and M-09; rules 2, 4, 5, 7, 8, 9 and 10). Read DemoData:Enabled in any environment, off in committed settings. When it is on, refuse to start unless the connected database's name is AttendTa_Demo or exactly equals DemoData:AllowDatabase, and unless DemoData:TokenKey is set; log the reason and show nothing on screen. Create one in-memory demo store keyed by real record ids for the state several surfaces read: event check-in open and completed state, pass tokens (HMAC-SHA256 of the registration id under DemoData:TokenKey, stable across restarts), attendance records, certificate reviews and issues, and account and membership status. Apply the invalidation rule inside the store: invalidating an attendance turns its pending or approved review not eligible with the reason, and is refused when the certificate is issued. Create the demo data folder with one registration method that calls one registration file per surface. Seed scenarios at start-up from records already in the connected database: a published event with check-in open and confirmed registrations, a started event ready to complete, and a completed event with check-ins (one invalidated) and a pending, a declined and an issued certificate; write the seed's starting state into the register for demo-script.md. Provide the two event-state commands with the real refusal rules (open/close check-in M-04, complete event M-09), the shared open/close check-in control partial that the dossier and scanner use, and the overlay of the Completed status and check-in flag on the officer event list, member feed and event detail. Record each screen's real and demo sources and file boundaries in the register before the surface tickets start.

## Requirements

Verify: With DemoData__Enabled=true, AllowDatabase unset and a database other than AttendTa_Demo, the app refuses to start and logs why; the same with AllowDatabase naming the connected database starts, and so does AttendTa_Demo. A missing TokenKey also refuses startup. One automated integration test proves the database refusal. With the app running, the seeded event list, feed and detail show Completed and check-in state after the commands run; completing a cancelled event, completing before start and opening check-in on an unpublished event are refused; a pass token is the same after a restart. No demo command writes SQL rows, stored files or Activity Log entries. With the switch unset the screens show their normal state and the existing test suites pass.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-platform-access/epic-platform-access.md
- docs/DESIGN.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/demo-mock-register.md
- docs/demo-host-setup.md

## Notes

- Demo phase (2026-11-06 demo rescope): frontend only. Use the real backend where a service method exists and demo data per demo-mock-register.md (rows M-01, M-04, M-09) where it does not; nothing on screen may show that data is demo data. Add or fill this ticket's register rows in the same pull request.
- Done check: dotnet build and one run of the startup refusal with the switch on against a non-demo database. The one automated test is the database refusal, because a wrongly enabled switch on a live host would fake check-ins and certificates. No other new automated tests, no per-ticket Impeccable critique or polish and no separate code-review pass; the pull request keeps green existing CI and one peer approval. Critique, polish and the full Verify line run once in 8.15.
- Decision: 2026-11-06 — review of the demo rescope: split from 1.13 so the shell and the seam run in parallel; adds the startup database guard, stable pass tokens, the real refusal rules and the event-status overlay. No GitHub issue exists for this entry yet; the tracker sync creates it.
- 8.13 removes M-01 after every other register row is removed; 3.12 removes M-09 and 4.2 removes M-04.
