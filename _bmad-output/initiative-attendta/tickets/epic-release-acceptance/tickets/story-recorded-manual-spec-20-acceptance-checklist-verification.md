---
id: 6
type: story
title: "Recorded manual SPEC §20 acceptance checklist verification"
parent: epic-release-acceptance
covers: [R1]
after: [2, 4, 5, 12]
hitl: true
risk: medium
tracker_id: "79"
remote: "https://github.com/rainyDayssss/AttendTa/issues/79"
tracker_status: backlog
---

# Recorded manual SPEC §20 acceptance checklist verification

## Description

Complete and execute the SPEC §20 manual matrix using finale-test-cases.md and the 8.5 coverage map: every scenario has preconditions, steps, expected outcome and recorded evidence. Cover the non-automated remainder as well as phone camera/denial/manual fallback, stale forms, responsive keyboard/dialogs and retained PDF appearance. Reuse the automated manifest from 8.4 and the UI walkthrough evidence from 8.12; do not run three identical checklists for 8.6/8.7/8.8.

## Requirements

Verify: An evaluator steps through the recorded manual SPEC §20 checklist on the live integrated build and confirms each acceptance item passes with signed-off evidence.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-release-acceptance/epic-release-acceptance.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md

## Delivery (2026-11-06)

- Manual acceptance of the live integrated build; it does not build a second screen or change presentation, and prototype outcomes do not close the issue.
- Reuses the 8.4 automated manifest and the 8.12 UI walkthrough evidence instead of running three identical checklists.
- Development check: the recorded checklist plus the focused manual smoke; existing CI unchanged. Missing important test authoring is 8.5 (#78), combined final execution 8.4 (#77); this entry is the manual pass 8.6–8.8.
- Test/manual: no Impeccable requirement; frontend design lives only in the separate slices 6.12/6.13/7.4 and finale 8.12.
- Human inputs (evaluator, phones, signed-off evidence) remain required; every SPEC §20 scenario keeps a concrete automated or manual case.
