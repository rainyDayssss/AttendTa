---
id: 5
type: story
title: "Active Event Type and Venue read queries for event lifecycle"
parent: epic-master-data
covers: [R3, R4]
after: [1, 3]
hitl: false
risk: low
tracker_id: "24"
remote: "https://github.com/rainyDayssss/AttendTa/issues/24"
tracker_status: done
assignee: "wigglebop25"
---

# Active Event Type and Venue read queries for event lifecycle

## Description

Exposes Data-layer read queries that return only active Event Types and active Venues. epic-events-registration entry 1 calls them to refuse inactive references on new or draft Events; existing references are kept. The Eligible Participant fact is not built here (AD-5).

## Requirements

Verify: An IntegrationTests run shows the active Event Type and active Venue read queries return only active rows, and an inactive type or venue is still returned by its own lookup so existing references resolve.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-master-data/epic-master-data.md
