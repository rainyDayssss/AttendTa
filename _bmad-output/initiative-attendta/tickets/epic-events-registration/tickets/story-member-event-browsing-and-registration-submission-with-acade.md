---
id: 5
type: story
title: "Member event browsing and registration submission with academic snapshot"
parent: epic-events-registration
covers: [R6, R15]
after: [3, 1.4]
hitl: false
risk: medium
tracker_id: "33"
remote: "https://github.com/rainyDayssss/AttendTa/issues/33"
tracker_status: done
assignee: "Wynnwrath"
---

# Member event browsing and registration submission with academic snapshot

## Description

Displays published events on the paged member feed and registers an Eligible Participant for a Published Event before its Signup Deadline under the owner's `users` row and then the event lock (registrations are written under the event lock only per AD-4 step 7), enforcing `ParticipationFacts` Signup open and Eligible Participant against the injected `TimeProvider` (refusing with `CutoffPassed`), creating a pending Registration with course, year and section snapshots, warning when Confirmed rows reach the participant limit, and refusing a second Registration for the same Member and Event through the `registrations(event_id, member_id)` unique key (`Duplicate`) even after cancellation.

## Requirements

Verify: An eligible member registers for a published event before the deadline, receives a Pending registration with academic snapshot saved, sees a full-event warning when capacity is reached, and a second registration attempt for the same event is refused with Duplicate; registration attempts when Signup open is false are refused with CutoffPassed.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-events-registration/epic-events-registration.md
