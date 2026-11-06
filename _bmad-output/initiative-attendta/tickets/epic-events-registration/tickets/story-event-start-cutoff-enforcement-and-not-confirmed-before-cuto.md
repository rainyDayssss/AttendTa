---
id: 10
type: story
title: "Event start cutoff enforcement and Not confirmed before cutoff status"
parent: epic-events-registration
covers: [R11]
after: [8, 9]
hitl: false
risk: low
tracker_id: "39"
remote: "https://github.com/rainyDayssss/AttendTa/issues/39"
tracker_status: done
assignee: "FivePesos"
---

# Event start cutoff enforcement and Not confirmed before cutoff status

## Description

Proves the exact Event Start Cutoff boundary with a fake `TimeProvider` in `Services.Tests` across the cutoff guards already established in entries 7, 8 and 9 (`ParticipationFacts` Event Start Cutoff passed, refused with `CutoffPassed` at or after event start in UTC), and displays pending and waitlisted registrations as 'Not confirmed before cutoff' without marking them cancelled or no-shows.

## Requirements

Verify: Boundary tests in Services.Tests verify with a fake TimeProvider that approval, decline, promotion, and member cancellation succeed one tick before event start and are rejected with CutoffPassed at and after event start, while pending and waitlisted registrations display as 'Not confirmed before cutoff' on status badges.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-events-registration/epic-events-registration.md
