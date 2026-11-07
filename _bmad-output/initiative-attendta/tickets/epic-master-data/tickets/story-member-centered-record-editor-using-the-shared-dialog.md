---
id: 13
type: story
title: "Member centered record editor using the shared dialog"
parent: epic-master-data
covers: [R2, R6]
after: [12]
hitl: false
risk: medium
tracker_id: "147"
remote: "https://github.com/rainyDayssss/AttendTa/issues/147"
tracker_status: done
---

# Member centered record editor using the shared dialog

## Description

Apply 2.12's reusable record dialog to Member edit using the current real Member service. Preserve read-only email, academic fields, membership type, student-number correction reason and errors; do not add standalone creation or implement membership-status mutations. Leave the access-action slot partial that 1.13 placed in each Members list row (5.11 fills it) untouched; change only the Edit trigger and the edit form.

## Requirements

Verify: An Officer edits a Member in a centered dialog, sees a required correction reason and inline server errors, cancels without saving and returns focus to the original row. Phone-width content and actions remain reachable.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-master-data/epic-master-data.md
- docs/DESIGN.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Notes

- Demo phase (2026-11-06 demo rescope): frontend only, real backend only; this ticket adds no demo data.
- Done check: `dotnet build`, `npm run css:build` when views or styles change, and one look at the changed screen with `DemoData__Enabled=true`. No new automated tests, no per-ticket Impeccable critique or polish and no separate code-review pass; the pull request keeps green existing CI and one peer approval. Critique, polish and the full Verify line run once in 8.15.
