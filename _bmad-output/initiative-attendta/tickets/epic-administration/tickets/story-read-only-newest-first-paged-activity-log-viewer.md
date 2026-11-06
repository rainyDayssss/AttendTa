---
id: 8
type: story
title: "Read-only newest-first paged Activity Log viewer"
parent: epic-administration
covers: [R4, R5, R6]
after: [1.3, 1.4, 1.6, 1.9]
hitl: false
risk: low
tracker_id: "59"
remote: "https://github.com/rainyDayssss/AttendTa/issues/59"
tracker_status: done
assignee: "LucielCienAmir"
---

# Read-only newest-first paged Activity Log viewer

## Description

Builds the read-only Activity Log screen for the Administrator policy, paged with PageRequest and the shared _Pager at 25 rows (AD-13), ordered occurred_at DESC, log_id DESC. Each row shows occurred time through the shared Asia/Manila formatter (AD-11, epic-platform-access entry 1.4), actor name and type, action, affected entity type and id, and reason. The screen has no edit or delete control.

## Requirements

Verify: Given at least 26 Activity Log rows seeded in the database, an Administrator opens the Activity Log at /admin/auditlogs and sees 25 rows per page, newest first with ties broken by log_id, Asia/Manila timestamps with a zone label, actor, action, affected record and reason, and no edit or delete controls. An Officer requesting the same route is refused.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-administration/epic-administration.md
