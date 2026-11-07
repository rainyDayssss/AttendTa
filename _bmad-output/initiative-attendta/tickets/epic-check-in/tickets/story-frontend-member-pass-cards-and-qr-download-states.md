---
id: 10
type: story
title: "Frontend member pass cards and QR download states"
parent: epic-check-in
covers: [R1, R2]
after: ["1.13", "1.14"]
hitl: false
risk: low
tracker_id: "144"
remote: "https://github.com/rainyDayssss/AttendTa/issues/144"
tracker_status: done
assignee: "FivePesos"
---

# Frontend member pass cards and QR download states

## Description

Fill the _PassPartial slot that 1.13 placed on each My Events registration card with the DESIGN screen 13 pass cards: active and past sections, event and holder identity, a QR with an intact quiet zone, and a PNG download. Until 4.1 exists the QR encodes the register rule 10 token (M-03) and is drawn by a locally served browser library, and the download saves that image. Pending, waitlisted, cancelled or unavailable registrations show no usable pass. Own the pass partial and its scoped assets, not MyEventsController or the host card. The 4.11 scanner matches these tokens.

## Requirements

Verify: A Member opens their passes in My Events, sees a card per confirmed registration with a QR that encodes that registration's token, downloads it as PNG, and sees no usable pass for a pending, waitlisted or cancelled registration; cards read well in one and two columns on phone and desktop; nothing on screen says the data is demo data.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-check-in/epic-check-in.md
- docs/DESIGN.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Notes

- Demo phase (2026-11-06 demo rescope): frontend only. Use the real backend where a service method exists and demo data per `demo-mock-register.md` (rows M-03) where it does not; nothing on screen may show that data is demo data. Add or fill this ticket's register rows in the same pull request.
- Done check: `dotnet build`, `npm run css:build` when views or styles change, and one look at the changed screen with `DemoData__Enabled=true`. No new automated tests, no per-ticket Impeccable critique or polish and no separate code-review pass; the pull request keeps green existing CI and one peer approval. Critique, polish and the full Verify line run once in 8.15.
