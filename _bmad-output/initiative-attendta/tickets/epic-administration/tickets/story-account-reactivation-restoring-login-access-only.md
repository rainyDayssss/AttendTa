---
id: 5
type: story
title: "Account reactivation restoring login access only"
parent: epic-administration
covers: [R3, R4]
after: [4]
hitl: false
risk: low
tracker_id: "56"
remote: "https://github.com/rainyDayssss/AttendTa/issues/56"
tracker_status: backlog
---

# Account reactivation restoring login access only

## Description

Reactivates a deactivated Account with a mandatory reason under the Administrator policy in one ExecuteInTransactionAsync: locks the users row, sets users.is_active to true and appends one user.reactivated Activity Log entry. It restores login access only: cancelled Registrations stay Cancelled with no seats or QR tokens restored, and issued Certificates are untouched.

## Requirements

Verify: Reactivating a deactivated account with a mandatory reason allows the user to log in again, their earlier cancelled Registrations stay Cancelled with no seats restored, and a pre-existing seeded certificate row in the database remains untouched.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-administration/epic-administration.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Delivery (2026-11-06)

- Backend command only; the account reactivation dialog is already delivered by 5.11. This issue wires the real command and adds no competing dialog.
- Development check: build plus one focused reactivate/login smoke; existing CI unchanged. Broad automation goes to 8.5 (#78), the full suite runs once at 8.4 (#77), final manual acceptance is 8.6–8.8.
- Final rules unchanged: login access only is restored; cancelled Registrations stay Cancelled with no seats or QR tokens restored and issued Certificates are untouched.
- Serialization: 5.4 (#55) and 5.5 (#56) land before 5.6 (#57) and 5.7 (#58) because all four change the shared administration source.

## Notes

- Removes demo data M-14b (2026-11-06 demo rescope): implement the real source behind the contract the demo provider serves, delete the provider classes and their registration, and set the rows to `removed` with the commit in `demo-mock-register.md`. The screen should not change visibly. No per-ticket Impeccable critique or polish; 8.12 covers it.
