---
id: 14
type: story
title: "Venue centered record editor with photo and validation feedback"
parent: epic-master-data
covers: [R4, R5, R6]
after: [12]
hitl: false
risk: medium
tracker_id: "148"
remote: "https://github.com/rainyDayssss/AttendTa/issues/148"
tracker_status: done
---

# Venue centered record editor with photo and validation feedback

## Description

Apply 2.12's reusable record dialog to Venue create/edit using the existing real service and private photo store. Keep the current photo, upload preview, required/format guidance, capacity guard errors and separate status action; retain entered values on failed save. Own only Venue presentation files and its scoped assets.

## Requirements

Verify: An Officer creates/edits a Venue inside the centered dialog, sees its existing or selected photo, corrects an invalid capacity/upload without a blank panel and can cancel by keyboard. The dialog and footer fit phone width.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-master-data/epic-master-data.md
- docs/DESIGN.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Notes

- Demo phase (2026-11-06 demo rescope): frontend only, real backend only; this ticket adds no demo data.
- Done check: `dotnet build`, `npm run css:build` when views or styles change, and one look at the changed screen with `DemoData__Enabled=true`. No new automated tests, no per-ticket Impeccable critique or polish and no separate code-review pass; the pull request keeps green existing CI and one peer approval. Critique, polish and the full Verify line run once in 8.15.
