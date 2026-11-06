---
id: 15
type: story
title: "Registration-owned participation cancellation service (AD-6)"
parent: epic-events-registration
after: [5]
hitl: false
risk: high
tracker_id: "35"
remote: "https://github.com/rainyDayssss/AttendTa/issues/35"
tracker_status: done
assignee: "wigglebop25"
---

# Registration-owned participation cancellation service (AD-6)

## Description

Creates `IRegistrationCancellationService` with `CancelRegistrationAsync`, `CancelAllForEventAsync`, `CancelUpcomingForUserAsync` and `PreviewUpcomingForUserAsync`, plus the `CancellationCause` enum (`MemberRequested`, `Declined`, `AdminCancelled`, `EventCancelled`, `MembershipDeactivated`, `AccountDeactivated`), as the only code that sets a registration to Cancelled: each cancelled row clears `qr_token` and records `cancelled_by`, `cancelled_at` and `cancellation_reason`, writes one `registration.cancelled` entry, leaves already-Cancelled rows unchanged, and promotes nothing; the deactivation pair treats Upcoming as not yet started with no Valid Attendance and locks affected events in ascending id order under the caller's `users` row, with the reason validated inside the transaction. Epic-administration entries 3 and 6 call PreviewUpcomingForUserAsync; entries 4 and 6 call CancelUpcomingForUserAsync.

## Requirements

Verify: Services.Tests with a fake `TimeProvider` cover the Upcoming rule and the preview list, and an IntegrationTests run cancels one account's upcoming registrations across two events, asserting ascending event lock order, cleared QR tokens, one `registration.cancelled` entry per row, unchanged earlier cancellations and no promoted waitlisted registration.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-events-registration/epic-events-registration.md
