---
id: 12
type: story
title: "Shared centered record dialog and Event Type editor tracer"
parent: epic-master-data
covers: [R3, R6]
after: [9, "1.13"]
hitl: false
risk: medium
tracker_id: "143"
remote: "https://github.com/rainyDayssss/AttendTa/issues/143"
tracker_status: done
---

# Shared centered record dialog and Event Type editor tracer

## Description

Deliver the reusable centered-record-dialog interaction and convert Event Type create/edit as the tracer for #128, using the real existing services. Preserve required markers and grouping shipped in #132. Cover fetch or row hydration, retained input and field errors inside the dialog, submitting feedback, focus containment/restoration, Escape/Cancel and separate status actions. Publish the reusable partial/JavaScript contract for 2.13/2.14; do not rewrite the backend.

## Requirements

Verify: An Officer creates and edits an Event Type from the list in a centered dialog, corrects a duplicate-name error without losing entered values, and can open/save/cancel with keyboard focus returned to the trigger. The dialog fits a 390px viewport.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-master-data/epic-master-data.md
- docs/DESIGN.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Notes

- Demo phase (2026-11-06 demo rescope): frontend only, real backend only; this ticket adds no demo data.
- Done check: `dotnet build`, `npm run css:build` when views or styles change, and one look at the changed screen with `DemoData__Enabled=true`. No new automated tests, no per-ticket Impeccable critique or polish and no separate code-review pass; the pull request keeps green existing CI and one peer approval. Critique, polish and the full Verify line run once in 8.15.
