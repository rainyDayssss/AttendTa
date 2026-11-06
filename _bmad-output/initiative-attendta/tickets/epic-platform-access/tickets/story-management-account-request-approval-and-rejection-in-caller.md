---
id: 7
type: story
title: "Management Account Request approval and rejection in caller transaction with audit"
parent: epic-platform-access
covers: [R2]
after: [1, 2, 3, 4, 5, 6, 11]
hitl: false
risk: high
tracker_id: "18"
remote: "https://github.com/rainyDayssss/AttendTa/issues/18"
tracker_status: done
assignee: "rainyDayssss,wigglebop25"
---

# Management Account Request approval and rejection in caller transaction with audit

## Description

Management approves or rejects a pending request inside ExecuteInTransactionAsync. Locks the account_requests row through LockAccountRequestAsync, then the users row, and re-checks email and student-number uniqueness. Approval creates the user and calls MemberService.CreateMemberFromApprovedAccountRequest (epic-master-data owner) with the hash moved over. Rejection requires a reason and clears the hash. Each decision is audited through IActivityLogWriter. A second decision on a finalized request, or a losing concurrent decision, throws Stale and rolls back. Concurrency tests live in tests/ASI.Basecode.IntegrationTests.

## Requirements

Verify: An officer approves a pending request and the applicant logs in as a Member with their original password. Scripted SQL Server concurrency integration tests run parallel approvals on the same request: exactly one Account is created, and the losing attempts roll back with no audit row and throw Stale. Rejection with a mandatory reason clears the hash. A rejected Applicant submits a new request that is accepted; and parallel approvals that race an Account with the same email or student number leave one outcome (Stale or Duplicate) and no second Account.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-platform-access/epic-platform-access.md
