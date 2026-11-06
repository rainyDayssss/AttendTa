---
id: 8
type: story
title: "Explicit waitlist promotion across full queue"
parent: epic-events-registration
covers: [R9, R14]
after: [7]
hitl: false
risk: medium
tracker_id: "37"
remote: "https://github.com/rainyDayssss/AttendTa/issues/37"
tracker_status: done
assignee: "FivePesos"
---

# Explicit waitlist promotion across full queue

## Description

Implements the officer action to promote the earliest Waitlisted Registration whose owner is still an Eligible Participant (`ParticipationFacts` Waitlist priority across the whole event queue) before the cutoff and when a seat is free, using discover, then lock (`users` row → event), then recheck, throwing `Stale` on mismatch; assigns confirmation and a 64-hex QR token, keeps `reviewed_at`, and appends `registration.promoted` in the same transaction, serialized with competing approvals.

## Requirements

Verify: When a seat is released, an officer clicks Promote and confirms the earliest eligible waitlisted member with a new QR token and an Activity Log entry; an IntegrationTests run of concurrent promotion and approval requests against SQL Server yields exactly one outcome per registration with no overbooking.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-events-registration/epic-events-registration.md
