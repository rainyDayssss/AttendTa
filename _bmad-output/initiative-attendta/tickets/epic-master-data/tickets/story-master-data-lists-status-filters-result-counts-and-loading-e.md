---
id: 10
type: story
title: "Master Data lists: status filters, result counts, and loading/error states"
parent: epic-master-data
covers: [R7]
after: [9, "1.13", 12, 13, 14]
hitl: false
risk: low
tracker_id: "127"
remote: "https://github.com/rainyDayssss/AttendTa/issues/127"
tracker_status: done
---

# Master Data lists: status filters, result counts, and loading/error states

## Description

Finish existing GitHub #127 only: result counts shipped in #132 and status filters in #134. Preserve those real integrations and finish meaningful submitting/loading feedback and shell-preserving error/retry states on Member, Event Type and Venue lists. Use the 1.13 shared conventions; do not rebuild the filters or require an AJAX conversion merely to display a loading state. Runs after 2.12–2.14 so their list triggers are in place.

## Requirements

Verify: An Officer sees the shipped filters and counts still working across pagination; a delayed or failed list interaction has truthful feedback and a usable retry inside the shell, and no records versus no matches remain distinct. Check keyboard and phone-width behavior manually.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-master-data/epic-master-data.md
- docs/DESIGN.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Notes

- Existing issue #127 is reused; #132/#134 shipped work remains historical acceptance evidence.
- Demo phase (2026-11-06 demo rescope): frontend only, real backend only; this ticket adds no demo data.
- Done check: `dotnet build`, `npm run css:build` when views or styles change, and one look at the changed screen with `DemoData__Enabled=true`. No new automated tests, no per-ticket Impeccable critique or polish and no separate code-review pass; the pull request keeps green existing CI and one peer approval. Critique, polish and the full Verify line run once in 8.15.
