---
id: 6
type: story
title: "Signup deadline enforcement at exact local boundary"
parent: epic-events-registration
covers: [R7]
after: [5]
hitl: false
risk: low
tracker_id: "34"
remote: "https://github.com/rainyDayssss/AttendTa/issues/34"
tracker_status: done
assignee: "wigglebop25"
---

# Signup deadline enforcement at exact local boundary

## Description

Proves the Signup Deadline at its exact boundary for stale submissions: `ParticipationFacts` Signup open at the instant of the deadline is tested in Services.Tests with a fake `TimeProvider`, and the closure shows the deadline in Asia/Manila; earlier registrations remain intact and the Event stays Published in the feed.

## Requirements

Verify: A member attempting to register one tick before the deadline succeeds, and at or after the deadline receives an explicit closure refusal; earlier registrations remain intact and the Event remains Published in the feed.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-events-registration/epic-events-registration.md
