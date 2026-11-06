---
id: 13
type: story
title: "Member self profile: bring screen 4 up to DESIGN §6"
parent: epic-check-in
covers: [R1]
after: [10]
hitl: false
risk: low
tracker_id: "129"
remote: "https://github.com/rainyDayssss/AttendTa/issues/129"
tracker_status: backlog
---

# Member self profile: bring screen 4 up to DESIGN §6

## Description

Finish existing GitHub #129 only: #132/#134 already delivered the profile title, grouped read-only panels, badge, dark identity strip, amber notice and inactive-membership explanation. Add the compact bottom action to the usable My Passes route from 4.10, preserving all shipped work and active-account versus membership semantics. This residual passes-link story belongs here as a consumer of the pass screen; it creates no new profile editor.

## Requirements

Verify: A Member follows the compact profile action to My Passes without a dead link; profile identity/notices remain correct and read-only. Inactive membership does not imply a blocked login or loss of issued-certificate access. Close #129 only after this rendered flow works.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-check-in/epic-check-in.md
- docs/DESIGN.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Notes

- Existing issue #129 is reused and its shipped #132/#134 acceptance is preserved.
- Demo phase (2026-11-06 demo rescope): frontend only, real backend only; this ticket adds no demo data.
- Done check: `dotnet build`, `npm run css:build` when views or styles change, and one look at the changed screen with `DemoData__Enabled=true`. No new automated tests, no per-ticket Impeccable critique or polish and no separate code-review pass; the pull request keeps green existing CI and one peer approval. Critique, polish and the full Verify line run once in 8.15.
