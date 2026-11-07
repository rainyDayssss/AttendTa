---
id: 11
type: story
title: "Master Data forms: centered record dialogs and required markers"
parent: epic-master-data
covers: [R2, R3, R4, R5, R6]
after: [13, 14]
hitl: false
risk: low
tracker_id: "128"
remote: "https://github.com/rainyDayssss/AttendTa/issues/128"
tracker_status: in-progress
---

# Master Data forms: centered record dialogs and required markers

## Description

Finale acceptance check for existing GitHub #128: the Member, Event Type and Venue editors from 2.12–2.14 meet docs/DESIGN.md (centered dialogs, focus entering/contained/restored, separate status versus Cancel/Save actions, retained validation, Venue photo preview). Fix only findings; required markers, fieldsets and constrained forms already shipped in #132. Runs with the 8.12 whole-app Impeccable pass.

## Requirements

Verify: All three editors satisfy docs/DESIGN.md: centered dialogs, keyboard focus entering/contained/restored, separate status versus Cancel/Save actions, retained validation and Venue photo preview. Close #128 only after 2.12–2.14 are complete and this combined walkthrough passes.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-master-data/epic-master-data.md
- docs/DESIGN.md
- _bmad-output/initiative-attendta/delivery-queue.md

## Notes

- Existing issue #128 is reused, not closed or weakened by the split.
- Moved to the finale by the 2026-11-06 demo rescope: it is an acceptance check, not demo work. Impeccable critique and polish apply here as part of 8.12.
