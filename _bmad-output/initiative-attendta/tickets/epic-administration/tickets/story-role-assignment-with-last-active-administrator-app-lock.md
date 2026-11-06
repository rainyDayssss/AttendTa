---
id: 2
type: story
title: "Role assignment with last-active-Administrator app lock"
parent: epic-administration
covers: [R2, R4]
after: [1, 1.1, 1.2, 1.3]
hitl: false
risk: high
tracker_id: "53"
remote: "https://github.com/rainyDayssss/AttendTa/issues/53"
tracker_status: done
assignee: "Wynnwrath"
---

# Role assignment with last-active-Administrator app lock

## Description

Enables an Administrator to assign the Member, Officer, or Administrator Role to an Account with a mandatory reason (users.role_id, roles lookup) under the Administrator policy, in one ExecuteInTransactionAsync. When the target is an active Administrator and the new role is not Administrator, it first takes sp_getapplock 'attendta:last-admin' (Exclusive, owner Transaction, bounded @LockTimeout; a negative return throws Stale), then locks the target's users row, then re-reads the target and counts active Administrators; rejecting demotions of the last active Administrator with explanatory feedback. Each change appends one user.role_assigned Activity Log entry with old and new role codes in the same transaction.

## Requirements

Verify: An integration test in ASI.Basecode.IntegrationTests (AD-17), with parallel connections against ATTENDTA_TEST_SQL, has two connections each demote one of two active Administrators; exactly one succeeds, the other is refused with explanatory feedback, and one Administrator stays active. A valid role change updates users.role_id, appends one activity_logs row in the same transaction, and the Administrator sees the new role badge on the Console Accounts list.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-administration/epic-administration.md
