---
id: 16
type: story
title: "Frontend officer event dossier roster and lifecycle dialogs"
parent: epic-events-registration
covers: [R1, R2, R3, R4, R5, R8, R9, R14, R15]
after: ["1.13", "1.14"]
hitl: false
risk: medium
tracker_id: "141"
remote: "https://github.com/rainyDayssss/AttendTa/issues/141"
tracker_status: backlog
---

# Frontend officer event dossier roster and lifecycle dialogs

## Description

Complete the officer path from the event list to an event dossier with metadata, capacity and a status-aware registration roster that uses the ready real review, promotion and cancellation services. Replace the Event ID filter workflow with contextual navigation from the event list into the dossier. Add the Complete event and Open/Close check-in controls with their dialogs; they call the open/close check-in and complete event commands, which run on demo data (M-04, M-09, provided by 1.14) until 3.12 and 4.2 exist, and the dossier shows the resulting state for the session. Own Event/RegistrationReview presentation and scoped assets, not My Events. DESIGN screens 8–10. The open/close check-in control is the shared partial from 1.14.

## Requirements

Verify: An Officer goes from Events to a dossier, sees metadata, capacity and the roster with status-aware actions, approves, declines and cancels through the real services, opens and closes check-in and completes a started event, and sees each new state on the dossier with no sign that any of it is demo data. Required-reason, validation and blocked-action states work on desktop and phone width. Completing a cancelled event or an event before its start time is refused.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-events-registration/epic-events-registration.md
- docs/DESIGN.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Notes

- Demo phase (2026-11-06 demo rescope): frontend only. Use the real backend where a service method exists and demo data per `demo-mock-register.md` (uses rows M-04, M-09 from 1.14; adds no rows) where it does not; nothing on screen may show that data is demo data.
- Done check: `dotnet build`, `npm run css:build` when views or styles change, and one look at the changed screen with `DemoData__Enabled=true`. No new automated tests, no per-ticket Impeccable critique or polish and no separate code-review pass; the pull request keeps green existing CI and one peer approval. Critique, polish and the full Verify line run once in 8.15.
