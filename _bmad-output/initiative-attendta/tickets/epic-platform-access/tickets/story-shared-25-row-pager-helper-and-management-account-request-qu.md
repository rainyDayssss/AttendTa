---
id: 6
type: story
title: "Shared 25-row pager helper and Management Account Request queue"
parent: epic-platform-access
covers: [R6]
after: [1, 2, 4, 5, 9]
hitl: false
risk: medium
tracker_id: "17"
remote: "https://github.com/rainyDayssss/AttendTa/issues/17"
tracker_status: done
assignee: "LucielCienAmir"
---

# Shared 25-row pager helper and Management Account Request queue

## Description

Delivers PageRequest, a non-generic IPagedResult implemented by PagedResult<T>, and one _Pager partial, at a fixed 25 rows with a unique-id tiebreak, filters and authorization applied before Count, page fallbacks, pager links that keep the current query string, navigation hidden for a single page, and an empty state. Applies it to the Management Account Request queue.

## Requirements

Verify: An authenticated officer navigates the Account Request queue and observes 25 items per page ordered by request timestamp then request id, the request time on the queue shows in Asia/Manila with its label, working previous and next controls that keep the active filter in the query string, hidden pagination on single pages, page 1 for an invalid page, the last page for an out-of-range page, and an empty state when no requests exist.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-platform-access/epic-platform-access.md
