---
id: 7
type: story
title: "Full functional demo script walkthrough across all 26 feature IDs"
parent: epic-release-acceptance
covers: [R2]
after: [6]
hitl: true
risk: low
tracker_id: "80"
remote: "https://github.com/rainyDayssss/AttendTa/issues/80"
tracker_status: backlog
---

# Full functional demo script walkthrough across all 26 feature IDs

## Description

Walk the complete request-to-report/certificate demonstration against real data, mapping all 26 functional IDs plus FR-35/FR-36 to the 8.6 acceptance evidence. Reuse verified steps and record only missing demo continuity/proof; no fixtures, browser storage workaround, direct SQL state editing or fake issued outcome qualifies.

## Requirements

Verify: An evaluator executes the full 26-feature-ID user journey demonstration script on the integrated dev build and verifies that every functional requirement from FR-1 through FR-36 succeeds without manual workarounds.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-release-acceptance/epic-release-acceptance.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md

## Delivery (2026-11-06)

- Demo walkthrough of the real integrated build; it does not build a second screen or change presentation, and fixtures or fake issued outcomes do not close the issue.
- Reuses the 8.6 acceptance evidence and records only missing demo continuity/proof.
- Development check: the demo script plus the focused end-to-end smoke; existing CI unchanged. Missing important test authoring is 8.5 (#78), combined final execution 8.4 (#77), manual 8.6–8.8.
- Test/demo: no Impeccable requirement; frontend design lives only in the separate slices 6.12/6.13/7.4 and finale 8.12.
- All 26 functional IDs plus FR-35/FR-36 stay final acceptance; no manual SQL state editing or browser-storage workaround qualifies.
