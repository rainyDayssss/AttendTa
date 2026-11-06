---
id: 7
type: story
title: "Member membership reactivation restoring eligibility only"
parent: epic-administration
covers: [R1, R4]
after: [6]
hitl: false
risk: medium
tracker_id: "58"
remote: "https://github.com/rainyDayssss/AttendTa/issues/58"
tracker_status: backlog
---

# Member membership reactivation restoring eligibility only

## Description

Reactivates a member's membership status with a reason under the Management policy in one ExecuteInTransactionAsync: locks the users row, sets members.membership_status_id to active, and appends one member.membership_reactivated Activity Log entry. It restores Eligible Participant status only, read through ParticipationFacts (AD-5). Cancelled Registrations stay Cancelled and users.is_active is unchanged.

## Requirements

Verify: Reactivating an inactive member: after reactivation the member's profile shows membership status Active, and an officer can again approve one of their Pending Registrations, while their login works throughout and their previously cancelled Registrations stay Cancelled.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-administration/epic-administration.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Delivery (2026-11-06)

- Backend command only; the membership reactivation dialog is already delivered by 5.11. This issue wires the real command and adds no competing dialog.
- Development check: build plus one focused reactivate/eligibility smoke; existing CI unchanged. Broad automation goes to 8.5 (#78), the full suite runs once at 8.4 (#77), final manual acceptance is 8.6–8.8.
- Final rules unchanged: eligibility only is restored, cancelled Registrations stay Cancelled, login works throughout and `users.is_active` is unchanged.
- Serialization: 5.4 (#55) and 5.5 (#56) land before 5.6 (#57) and 5.7 (#58) because all four change the shared administration source.

## Notes

- Removes demo data M-15b (2026-11-06 demo rescope): implement the real source behind the contract the demo provider serves, delete the provider classes and their registration, and set the rows to `removed` with the commit in `demo-mock-register.md`. The screen should not change visibly. No per-ticket Impeccable critique or polish; 8.12 covers it.
