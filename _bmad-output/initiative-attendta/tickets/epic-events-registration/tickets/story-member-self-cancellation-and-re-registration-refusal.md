---
id: 9
type: story
title: "Member self-cancellation and re-registration refusal"
parent: epic-events-registration
covers: [R10, R14]
after: [7, 15]
hitl: false
risk: low
tracker_id: "38"
remote: "https://github.com/rainyDayssss/AttendTa/issues/38"
tracker_status: done
assignee: "FivePesos"
---

# Member self-cancellation and re-registration refusal

## Description

Enables a member to cancel their own pending, confirmed or waitlisted registration on a Published Event before the cutoff and before Valid Attendance, after an explicit confirmation warning that cancellation is final, under the owner's `users` row and then the event lock; `IRegistrationCancellationService.CancelRegistrationAsync` (`MemberRequested`) clears the QR token, releases the seat without promotion and records metadata, and re-registration is refused by the unique key.

## Requirements

Verify: A confirmed member cancels their registration after confirming the warning dialog, observing their seat released and QR token cleared with an Activity Log entry, and a subsequent registration attempt is refused.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-events-registration/epic-events-registration.md
