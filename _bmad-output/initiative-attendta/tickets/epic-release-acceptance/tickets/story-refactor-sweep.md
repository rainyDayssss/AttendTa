---
id: 10
type: story
title: "Refactor sweep"
parent: epic-release-acceptance
after: [11, 12, 13]
hitl: false
risk: low
tracker_id: "83"
remote: "https://github.com/rainyDayssss/AttendTa/issues/83"
tracker_status: backlog
---

# Refactor sweep

## Description

Pre-acceptance release cleanup only: remove enabled throwaway harnesses, confirm no demo data configuration remains in deployment settings, check setup/run documentation and secret hygiene, and retain useful evidence. Backend hardening is 8.13 and rendered frontend critique/polish is 8.12. Finish code/config cleanup BEFORE acceptance runs and the release PR, not after main is released.

## Requirements

Verify: The solution/CSS build succeeds, no temporary credentials or enabled demo data runtime configuration is committed, setup instructions match the integrated build and the acceptance run can use this exact commit without another planned code-changing sweep.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-release-acceptance/epic-release-acceptance.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md

## Delivery (2026-11-06)

- Pre-acceptance cleanup only; it does not build a second screen or change presentation, and demo data configuration does not close the issue.
- Runs BEFORE the acceptance tests and the release PR (after 8.13/8.11/8.12), not after main is released.
- Development check: solution/CSS build plus the focused cleanup smoke; existing CI unchanged. Missing important test authoring is 8.5 (#78), combined final execution 8.4 (#77), manual 8.6–8.8.
- Cleanup: no per-ticket design critique or polish requirement; backend hardening is 8.13 and frontend critique/polish is 8.12, with design only in the separate slices 6.12/6.13/7.4 and finale 8.12.
- Secret hygiene, setup/run documentation and the absence of enabled demo data runtime configuration stay final acceptance.
