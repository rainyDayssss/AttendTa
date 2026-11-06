---
id: 5
type: story
title: "Important automated acceptance cases and SPEC coverage mapping"
parent: epic-release-acceptance
covers: [R1]
after: [10, 1.8, 2.9, 3.14, 4.9, 5.10, 6.11, 7.3]
hitl: false
risk: medium
tracker_id: "78"
remote: "https://github.com/rainyDayssss/AttendTa/issues/78"
tracker_status: backlog
---

# Important automated acceptance cases and SPEC coverage mapping

## Description

Author only the missing important automated acceptance cases in the two existing test projects, reusing current auth, seat/cutoff, cancellation, audit and PDF coverage. Use finale-test-cases.md for ownership/antiforgery, duplicate/refused attendance, completion rollback, access cascades/last admin, duplicate issuance and invalidation races, retained/authorized files and report formulas. Do not automate every cosmetic state or duplicate an existing passing case. Map every remaining SPEC §20 scenario to an existing automated case or a concrete manual case in 8.6; no scenario is silently removed. 8.4 owns the final combined full-suite execution.

## Requirements

Verify: The acceptance matrix identifies an existing or added automated test for each high-risk case and a manual case for every remaining SPEC §20 scenario; targeted new cases pass and real-SQL race checks prove SM-4. No new test project or large prototype-only suite is introduced.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-release-acceptance/epic-release-acceptance.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md

## Delivery (2026-11-06)

- Test-only authoring of the missing important cases; it does not build a second screen or change presentation, and prototype-only coverage does not close the issue.
- #78 is not "automate every cosmetic scenario": full SPEC §20 coverage stays through the automated/manual mapping, with the remainder assigned to concrete 8.6 manual cases.
- CI now does install Chromium; use the PDF test double for SQL race/lock tests and the real renderer for PDF checks. Existing CI unchanged.
- Missing important test authoring is this entry (#78); combined final execution is 8.4 (#77), manual 8.6–8.8.
- Test-only: no Impeccable requirement; frontend design lives only in the separate slices 6.12/6.13/7.4 and finale 8.12.
- The high-risk lifecycle/storage/race rules stay final acceptance; no new test project or large prototype-only suite.
