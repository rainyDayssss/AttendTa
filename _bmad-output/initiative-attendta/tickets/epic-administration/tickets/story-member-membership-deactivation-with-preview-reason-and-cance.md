---
id: 6
type: story
title: "Member membership deactivation with preview, reason and cancellation cascade"
parent: epic-administration
covers: [R1, R3, R4]
after: [3, 5, 11, 2.6, 3.15]
hitl: false
risk: low
tracker_id: "57"
remote: "https://github.com/rainyDayssss/AttendTa/issues/57"
tracker_status: backlog
---

# Member membership deactivation with preview, reason and cancellation cascade

## Description

Wires 5.11's membership deactivation action to the real command; it adds no new action UI. The dialog shows the preview from entry 3 and requires a non-empty reason. Submitting runs one ExecuteInTransactionAsync under the Management policy: it locks the member's users row (which also protects the members row, AD-4), sets members.membership_status_id to inactive, calls IRegistrationCancellationService.CancelUpcomingForUserAsync(userId, MembershipDeactivated, reason) from epic-events-registration (AD-6), and appends one member.membership_deactivated Activity Log entry. It takes no app lock and leaves users.is_active unchanged. It replaces 5.11's demo membership commands.

## Requirements

Verify: Management opens a Member; the deactivation modal lists that member's upcoming Registrations, and submission with an empty reason is refused. With a reason entered, the membership status reads inactive, those Registrations show Cancelled with QR tokens cleared, the user can still log in, and the Activity Log holds one member.membership_deactivated entry.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-administration/epic-administration.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Delivery (2026-11-06)

- Backend command only; the membership deactivation dialog with the real preview is already delivered by 5.11 on 5.3's preview. This issue wires the real command and adds no competing dialog.
- Development check: build plus one focused preview/reason/cascade smoke; existing CI unchanged. Broad automation goes to 8.5 (#78), the full suite runs once at 8.4 (#77), final manual acceptance is 8.6–8.8.
- Final rules unchanged: an empty reason is refused, membership reads inactive, the affected Registrations are cancelled with QR tokens cleared, login still works and `users.is_active` is unchanged.
- Serialization: 5.4 (#55) and 5.5 (#56) land before 5.6 (#57) and 5.7 (#58) because all four change the shared administration source.

## Notes

- Removes demo data M-15a (2026-11-06 demo rescope): implement the real source behind the contract the demo provider serves, delete the provider classes and their registration, and set the rows to `removed` with the commit in `demo-mock-register.md`. The screen should not change visibly. No per-ticket Impeccable critique or polish; 8.12 covers it.
