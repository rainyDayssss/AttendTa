---
id: 1
type: story
title: "Event Type management with required description, audit logging, and paged Tailwind list"
parent: epic-master-data
covers: [R3, R6, R7]
after: [1.1, 1.3, 1.6, 1.11]
hitl: false
risk: low
tracker_id: "20"
remote: "https://github.com/rainyDayssss/AttendTa/issues/20"
tracker_status: done
assignee: "FivePesos"
---

# Event Type management with required description, audit logging, and paged Tailwind list

## Description

Brings Event Type create, edit, and active toggle to the canonical schema with a required non-blank description and unique trimmed name, adding LockEventTypeAsync on EventTypeRepository and taking the event_types row lock for each write (deactivation takes the event_types row lock then reads linked events), reading the actor from ICurrentActor under the Management policy, mapping the DomainException outcomes that platform-access entry 11 introduced to field errors, stamping times from TimeProvider, logging old and new values through IActivityLogWriter, and displaying the list with the 25-row shared pager in the Tailwind shell.

## Requirements

Verify: An Officer creates an Event Type with a required description, sees it in the 25-row paged Tailwind table ordered by name with an id tie-break, edits its name and description, toggles its status, and verifies that each action appends an activity_logs entry with old and new values; a duplicate name or blank description is refused with a field error.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-master-data/epic-master-data.md
