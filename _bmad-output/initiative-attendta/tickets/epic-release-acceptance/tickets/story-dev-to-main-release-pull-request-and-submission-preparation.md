---
id: 9
type: story
title: "Dev to main release pull request and submission preparation"
parent: epic-release-acceptance
covers: [R6]
after: [8, 10, 11, 12, 13]
hitl: true
risk: low
tracker_id: "82"
remote: "https://github.com/rainyDayssss/AttendTa/issues/82"
tracker_status: backlog
---

# Dev to main release pull request and submission preparation

## Description

Prepares the release pull request from dev to main with attested Tier 1 local checks, green Tier 2 CI status, and the complete acceptance evidence package for team peer review and human merge.

## Requirements

Verify: A team member reviews the dev-to-main pull request, confirms that every PR merged to dev shows green CI and at least one peer approval (prd SM-3), verifies all release CI checks are green with required peer approvals recorded, and performs the manual merge to main.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-release-acceptance/epic-release-acceptance.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md

## Delivery (2026-11-06)

- Release preparation for the exact integrated commit; it does not build a second screen or change presentation, and prototype or fixture outcomes do not close the issue.
- This is the LAST finale step: 8.14 real-host evidence, 8.13 backend hardening, 8.11 mock removal/real integration, 8.12 final frontend, 8.10 cleanup before tests, 8.5 authoring, 8.4 execution, 8.6 manual, 8.7 demo and 8.8 signoff all precede it.
- Development check: build plus the focused release-readiness smoke; existing CI unchanged. Missing important test authoring is 8.5 (#78), combined final execution 8.4 (#77), manual 8.6–8.8.
- Release: no Impeccable requirement; frontend design lives only in the separate slices 6.12/6.13/7.4 and finale 8.12.
- Green CI and one peer approval on every dev PR, with the complete acceptance evidence, remain final acceptance for the human merge.
