---
id: 4
type: story
title: "Venue paged list with photo thumbnails, active toggle, and Tailwind views"
parent: epic-master-data
covers: [R4, R6, R7]
after: [1.6, 2, 3]
hitl: false
risk: low
tracker_id: "23"
remote: "https://github.com/rainyDayssss/AttendTa/issues/23"
tracker_status: done
assignee: "FivePesos"
---

# Venue paged list with photo thumbnails, active toggle, and Tailwind views

## Description

Renders the Venue management list with the 25-row shared pager ordered by room name with a venue_id tie-break, photo thumbnails loaded only through the authorized photo route, and an active toggle that takes the venues row lock via LockVenueAsync then reads linked events and appends an audit entry with old and new values, ported to the Tailwind shell under the Management policy.

## Requirements

Verify: An Officer views the 25-row paged Venue list with thumbnails from the authorized route, toggles a venue's active state, observes the updated status and an activity_logs entry, and a page past the end shows the last page.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-master-data/epic-master-data.md
