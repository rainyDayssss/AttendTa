---
id: 8
type: story
title: "Member read-only self profile with ownership authorization"
parent: epic-master-data
covers: [R1]
after: [1.1, 1.9]
hitl: false
risk: low
tracker_id: "27"
remote: "https://github.com/rainyDayssss/AttendTa/issues/27"
tracker_status: done
assignee: "rainyDayssss"
---

# Member read-only self profile with ownership authorization

## Description

Delivers a read-only Profile view for logged-in Members that reads the profile for ICurrentActor.UserId with no id route parameter, showing account, academic, and membership details with no editable controls; a request naming another member's profile is refused with the generic Forbidden outcome.

## Requirements

Verify: A logged-in Member opens their profile and sees account, academic, and membership details with no editable controls; a Member request to a Management member route is refused with the generic Forbidden outcome.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-master-data/epic-master-data.md
