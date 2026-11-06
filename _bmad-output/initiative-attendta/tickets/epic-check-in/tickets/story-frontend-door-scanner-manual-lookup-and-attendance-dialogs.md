---
id: 11
type: story
title: "Frontend door scanner manual lookup and attendance dialogs"
parent: epic-check-in
covers: [R3, R4, R5, R6, R8, R9]
after: ["1.13", "1.14", 10]
hitl: false
risk: medium
tracker_id: "138"
remote: "https://github.com/rainyDayssss/AttendTa/issues/138"
tracker_status: backlog
---

# Frontend door scanner manual lookup and attendance dialogs

## Description

Build DESIGN screen 14 on the 1.13 shell: event selection, open/closed check-in control, live camera decoding with a locally served browser QR decoder (no CDN), persistent success, repeat, invalid, closed and camera-unavailable outcomes, manual name/student-number lookup with an explicit Check in, and attendance invalidation with reason and confirmation. The camera and manual adapters are separate files that post to the check-in contract; until 4.2, 4.3, 4.5 and 4.6 exist that contract is served by demo data (M-05, M-06, M-07; the open/close control is 1.14's shared partial for M-04), and a pass from 4.10 is matched against register rule 10 tokens. Each demo check-in reloads the real registration status and applies register rule 8, so a registration cancelled through the real service is refused; invalidation changes the review through the 1.14 store rule and is refused when the certificate is issued. 4.3 and 4.5 later replace the demo providers behind the same contract, and 4.4 verifies the camera against the real engine. Searching or viewing never records attendance. Serve all assets locally. The 4.12 attendance history and 7.4 report read the check-ins this scanner writes to the demo store. The event selector is 1.13's shared partial and the open/close check-in control is 1.14's.

## Requirements

Verify: On localhost an Officer opens the scanner for a published event with check-in open, scans a seeded member's pass from 4.10 with the laptop camera and sees the member's name and student number; a second scan shows the original check-in time; an unknown code, a closed window and a denied camera each show their outcome, a pass whose registration was cancelled through the real service shows the invalid outcome; manual lookup finds a confirmed participant and checks them in; invalidation needs a reason and is refused for an issued certificate; nothing on screen says the data is demo data; the page works by keyboard at desktop and 390px width.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-check-in/epic-check-in.md
- docs/DESIGN.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Notes

- Demo phase (2026-11-06 demo rescope): frontend only. Use the real backend where a service method exists and demo data per `demo-mock-register.md` (adds rows M-05, M-06, M-07; uses M-04 from 1.14) where it does not; nothing on screen may show that data is demo data. Add or fill this ticket's register rows in the same pull request.
- Done check: `dotnet build`, `npm run css:build` when views or styles change, and one look at the changed screen with `DemoData__Enabled=true`. No new automated tests, no per-ticket Impeccable critique or polish and no separate code-review pass; the pull request keeps green existing CI and one peer approval. Critique, polish and the full Verify line run once in 8.15.
