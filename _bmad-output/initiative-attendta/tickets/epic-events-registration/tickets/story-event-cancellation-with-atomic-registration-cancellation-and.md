---
id: 11
type: story
title: "Event cancellation with atomic registration cancellation and check-in closure"
parent: epic-events-registration
covers: [R4, R14]
after: [4, 7, 15]
hitl: false
risk: medium
tracker_id: "40"
remote: "https://github.com/rainyDayssss/AttendTa/issues/40"
tracker_status: done
assignee: "FivePesos"
---

# Event cancellation with atomic registration cancellation and check-in closure

## Description

Cancels a Draft or Published Event with a reason under the event lock through `IEventLifecycleService`, refusing (`Invalid`) when `ParticipationFacts` Valid Attendance holds or a Data-layer read of certificates finds an issued row (AD-2 allows reads); then calls `IRegistrationCancellationService.CancelAllForEventAsync` (`EventCancelled`) for open registrations, sets `is_check_in_open = 0` (closing `check_in_closed_by/at` when the flag was open), records `cancelled_by`, `cancelled_at` and `cancellation_reason`, and writes one `event.cancelled` entry plus one `registration.cancelled` entry per cancelled registration in the same transaction.

## Requirements

Verify: An officer cancels a Published Event with a reason, observing all pending, confirmed and waitlisted registrations cancelled with QR tokens cleared, check-in closed, and one `event.cancelled` entry plus one `registration.cancelled` entry per registration recorded; cancellation is refused when valid attendance or an issued certificate exists in the database.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-events-registration/epic-events-registration.md
