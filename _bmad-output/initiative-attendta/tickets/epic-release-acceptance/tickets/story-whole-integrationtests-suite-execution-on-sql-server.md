---
id: 4
type: story
title: "Whole IntegrationTests suite execution on SQL Server"
parent: epic-release-acceptance
covers: [R3]
after: [5, 10, 1.1, 1.7, 3.7, 4.3, 5.2, 6.7]
hitl: false
risk: medium
tracker_id: "77"
remote: "https://github.com/rainyDayssss/AttendTa/issues/77"
tracker_status: backlog
---

# Whole IntegrationTests suite execution on SQL Server

## Description

Run the existing service and SQL integration suites plus the important cases authored/reused by 8.5 against the integrated real-data build. Use ATTENDTA_TEST_SQL pointing at local SQL master; each class owns AttendTa_IT_<guid>, never the demo/shared database. Include the existing JavaScript test:js and CSS build checks. Confirm Chromium and Poppler are installed on the local test host (CI already provisions both); environment failures are blockers, not passes. Record one execution manifest with commit, environment, commands and results; reuse it for 8.6–8.8 instead of repeating full runs without code changes. Existing PR CI remains enabled throughout development.

## Requirements

Verify: On the tested commit all existing and important new suites pass, with zero overbooking or duplicate approval/attendance/certificate and last-admin races safe. Environment failures and skipped critical cases are explicit blockers, not green results; commands and evidence are attached to #77.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-release-acceptance/epic-release-acceptance.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md

## Delivery (2026-11-06)

- Test-only execution of the combined suite after 8.5 authors the missing important cases; it does not build a second screen or change presentation, and prototype-only results do not close the issue.
- CI now does install Chromium; use the PDF test double for SQL race/lock tests and the real renderer for PDF checks. Existing CI unchanged.
- Missing important test authoring is 8.5 (#78); this entry is the combined final execution (#77) and its one manifest feeds manual 8.6–8.8.
- Test-only: no Impeccable requirement; frontend design lives only in the separate slices 6.12/6.13/7.4 and finale 8.12.
- Zero overbooking and zero duplicate approval/attendance/certificate/last-admin remain final acceptance; environment failures and skipped critical cases are blockers, not passes.
