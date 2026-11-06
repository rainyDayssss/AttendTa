---
id: 9
type: story
title: "Refactor sweep"
parent: epic-master-data
after: [1, 2, 3, 4, 5, 6, 7, 8]
hitl: false
risk: low
tracker_id: "28"
remote: "https://github.com/rainyDayssss/AttendTa/issues/28"
tracker_status: done
assignee: "LucielCienAmir"
---

# Refactor sweep

## Description

Cleans up deferred review findings, legacy Bootstrap markup artifacts, and dead code across Master Data controllers, services, and views without adding new scope.

## Requirements

Verify: The solution builds with zero warnings, all Master Data tests pass, and no InvalidDataException, GetActorUserId, actorUserId parameter, DateTime.UtcNow or Roles-string authorization remains in Master Data code or views.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-master-data/epic-master-data.md
