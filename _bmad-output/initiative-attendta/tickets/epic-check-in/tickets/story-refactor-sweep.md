---
id: 9
type: story
title: "Refactor sweep"
parent: epic-check-in
after: [1, 2, 3, 4, 5, 6, 7, 10, 11, 12, 13]
hitl: false
risk: low
tracker_id: "51"
remote: "https://github.com/rainyDayssss/AttendTa/issues/51"
tracker_status: backlog
---

# Refactor sweep

## Description

Finale backend-only cleanup of concrete deferred QR/check-in service, query and endpoint findings, without new scope or a second frontend audit. Shared scanner/history visuals are polished once in 8.12.

## Requirements

Verify: A non-builder checks the affected diff: no temporary scan/token logging, dead backend code or schema drift remains; build and a focused affected check pass. 8.4 records the full regression once.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-check-in/epic-check-in.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md

## Delivery (2026-11-06)

- Finale backend-only sweep of concrete deferred QR/check-in service, query and endpoint findings. It keeps the shipped check-in engine, invalidation and pass rendering, adds no new scope and carries no Impeccable requirement; shared scanner/history visuals are polished once in 8.12.
- Development check: build plus the focused affected check; existing CI unchanged. The full regression is recorded once by 8.4 (#77), broad automation is 8.5 (#78), final manual acceptance is 8.6–8.8.
- Final rules unchanged: no temporary scan/token logging, no dead backend code, no schema drift, and all existing check-in and attendance tests still pass.
