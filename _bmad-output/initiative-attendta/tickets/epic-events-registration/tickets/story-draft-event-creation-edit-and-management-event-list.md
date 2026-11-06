---
id: 1
type: story
title: "Draft event creation, edit, and management event list"
parent: epic-events-registration
covers: [R1, R15]
after: [2.5, 1.3, 1.4, 1.6]
hitl: true
risk: low
tracker_id: "29"
remote: "https://github.com/rainyDayssss/AttendTa/issues/29"
tracker_status: done
assignee: "FivePesos"
---

# Draft event creation, edit, and management event list

## Description

Creates and edits draft Events through `IEventLifecycleService` with active type and venue validation under the event type → venue → event lock order (adds `EventRepository.LockEventAsync(id)` here per AD-4), a positive limit within venue capacity and a Signup Deadline no later than start, appending `event.updated` Activity Log entries on edits, showing draft Events on the paged management list while hiding them from members; uses the canonical EF mappings, the audit writer, the pager and the local-time helper from epic-platform-access, and active types and venues from epic-master-data.

## Requirements

Verify: The user runs the dev build with the setup-script officer account and walks the verify: creates a draft event with an active type and venue, sees it appear on the paged management events list, edits its title with an event.updated audit entry recorded, observes that creating or editing a draft with an inactive Event Type or Venue is refused via 2.5 active read queries, and verifies a non-officer cannot access or view the draft.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-events-registration/epic-events-registration.md
