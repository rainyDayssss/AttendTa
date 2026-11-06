---
id: 7
type: story
title: "Registration review and transactional seat allocation with SQL Server concurrency test"
parent: epic-events-registration
covers: [R8, R14, R15]
after: [6, 15]
hitl: false
risk: high
tracker_id: "36"
remote: "https://github.com/rainyDayssss/AttendTa/issues/36"
tracker_status: done
assignee: "wigglebop25"
---

# Registration review and transactional seat allocation with SQL Server concurrency test

## Description

Implements officer review of Pending Registrations before the Event Start Cutoff under the owner's `users` row and then the event lock, rechecking that the Event remains Published and the member remains an Eligible Participant (SPEC §9.2, §10). Approval confirms with a 64-lowercase-hex QR token generated from 32 cryptographically random bytes when `ParticipationFacts` Seat held is below `participant_limit` and no earlier eligible Waitlisted Registration has priority (`reviewed_at ASC, registration_id ASC`), otherwise it waitlists, writing a `registration.approved` Activity Log entry; decline and administrative cancellation of Confirmed or Waitlisted rows without Valid Attendance validate reasons in-transaction, call `IRegistrationCancellationService` (`Declined`, `AdminCancelled`), and write their own caller audit entries (`registration.declined` or `registration.admin_cancelled`) alongside the service's per-row cancellation log; `reviewed_by` and `reviewed_at` are stamped; the review queue is paged at 25 rows; and an integration test proves concurrent last-seat approvals never overbook.

## Requirements

Verify: An IntegrationTests run in `tests/ASI.Basecode.IntegrationTests` executes concurrent approval requests for the final seat against SQL Server with zero overbooking; an officer in the browser approves a registration to confirm it with a 64-hex QR token and sees registration.approved recorded, declines another with a required reason recording caller registration.declined plus service registration.cancelled audit entries, and administrative cancellation records registration.admin_cancelled.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-events-registration/epic-events-registration.md
