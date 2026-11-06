---
id: 11
type: story
title: "Refactor sweep"
parent: epic-certificates
after: [1, 2, 3, 4, 5, 6, 7, 8, 9, 12, 13]
hitl: false
risk: low
tracker_id: "70"
remote: "https://github.com/rainyDayssss/AttendTa/issues/70"
tracker_status: backlog
---

# Refactor sweep

## Description

Finale backend-only cleanup of concrete certificate service, transaction, storage and endpoint findings, including retiring the development tracer without replacing the retained-PDF pipeline. Frontend critique/polish belongs to 8.12; no imaginary lint command or extra full-app audit.

## Requirements

Verify: Build and the focused affected issuance/download check pass; no agreed backend finding or enabled tracer remains. 8.4 records the full existing service, SQL and PDF regression once.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-certificates/epic-certificates.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md

## Delivery (2026-11-06)

- Finale backend-only cleanup of concrete findings; it retires the development tracer without replacing the retained-PDF pipeline and does not build a second screen or change presentation.
- Development check: build plus the focused affected issuance/download smoke; existing CI unchanged. Missing important test authoring is 8.5 (#78), combined final execution 8.4 (#77), manual 8.6–8.8.
- Backend-only: no per-ticket design critique or polish requirement; frontend critique/polish is 8.12 and design lives only in the separate slices 6.12/6.13/7.4 and finale 8.12.
- No imaginary lint command and no extra full-app audit; the real service, SQL and PDF regression is recorded once by 8.4.
