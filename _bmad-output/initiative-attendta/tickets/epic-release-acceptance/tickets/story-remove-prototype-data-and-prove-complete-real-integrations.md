---
id: 11
type: story
title: "Remove prototype data and prove complete real integrations"
parent: epic-release-acceptance
covers: [R1, R2, R3]
after: [13, "2.10", "2.11"]
hitl: false
risk: high
tracker_id: "152"
remote: "https://github.com/rainyDayssss/AttendTa/issues/152"
tracker_status: backlog
---

# Remove prototype data and prove complete real integrations

## Description

Verify only; this ticket changes no code. Confirm every row of `demo-mock-register.md`, M-01 included, is `removed` with its commit; an open row sends the finding back to that row's removing ticket instead of being fixed here. Then prove the demo seam is gone with checks that fail if any part remains: start the published build with DemoData__Enabled=true, no DemoData__TokenKey and a database other than AttendTa_Demo, which the startup guard refuses while the seam exists; and search the source and the published output for every register id, the demo data folder and the DemoData configuration keys. Finally verify My Events, passes, attendance, scanner, completion, certificate review, issue and download, access changes and reports against one coherent database. Keep only isolated test fixtures in the test projects. This is a functional integration gate, not a frontend redesign or a claim that the final test suite already passed.

## Requirements

Verify: Every register row shows `removed` with its commit; the published build starts normally with DemoData__Enabled=true, no token key and a non-demo database; the search finds no register id, demo data folder or DemoData key; a real member goes from approved account through registration, pass, check-in and completion to a retained certificate and matching report and audit, with no manual SQL state workaround.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-release-acceptance/epic-release-acceptance.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Notes

- Verify-only (2026-11-06 review): 8.13 removes M-01; findings return to the owning ticket.
