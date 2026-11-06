---
id: 8
type: story
title: "Refactor sweep"
parent: epic-platform-access
after: [1, 2, 3, 4, 5, 6, 7, 9, 10, 11]
hitl: false
risk: low
tracker_id: "19"
remote: "https://github.com/rainyDayssss/AttendTa/issues/19"
tracker_status: done
assignee: "Wynnwrath,wigglebop25"
---

# Refactor sweep

## Description

Cleans up technical debt, obsolete stubs, and temporary scaffolds across auth, schema, and UI, with scope set at its start from plans and review findings the epic deferred.

## Requirements

Verify: The solution builds cleanly with zero warnings, all test suites pass in GitHub Actions CI including SQL Server integration and concurrency tests, and no SessionManager, MemberAccountSummary, PathManager, PasswordManager, BCrypt, JwtBearer, CustomJwtDataFormat, TokenAuthentication or InvalidDataException remains in e1-owned code.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-platform-access/epic-platform-access.md
