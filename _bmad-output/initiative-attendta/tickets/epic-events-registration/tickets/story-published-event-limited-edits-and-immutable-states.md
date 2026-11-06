---
id: 4
type: story
title: "Published event limited edits and immutable states"
parent: epic-events-registration
covers: [R3, R14]
after: [3]
hitl: false
risk: low
tracker_id: "32"
remote: "https://github.com/rainyDayssss/AttendTa/issues/32"
tracker_status: done
assignee: "wigglebop25"
---

# Published event limited edits and immutable states

## Description

Restricts updates on Published Events to title, description and Cover replacement through `IEventLifecycleService` under the event type → venue → event lock order, logging old and new title and description values and recording a cover replacement with no file path in `event.updated`, while locking Completed and Cancelled Events as read-only.

## Requirements

Verify: An officer updates the title and description of a Published Event and sees the change with old and new values in the Activity Log, a cover replacement appears there without any file path, and server validation rejects changes to schedule, venue, limit or deadline.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-events-registration/epic-events-registration.md
