---
id: 14
type: story
title: "Verify real demo-host readiness and close provisioning evidence gaps"
parent: epic-release-acceptance
covers: [R4, R5]
after: [1]
hitl: true
risk: high
tracker_id: "150"
remote: "https://github.com/rainyDayssss/AttendTa/issues/150"
tracker_status: backlog
---

# Verify real demo-host readiness and close provisioning evidence gaps

## Description

Follow up closed #74/PR104 without rewriting its history: provisioning/validation tooling merged, but real-host readiness was not proven. On the intended demo host verify/fix server-level runtime SQL privilege rejection, table AND column-level activity_logs UPDATE/DELETE denial, logs and SetupRoot writability, offline Chromium/PDF operation, the Argon2 benchmark and runtime/secrets configuration. Prepare the host/address/certificate decision early with the technical contact; actual phone scanning and restore stay in 8.2/8.3. No destructive database reset or production changes.

## Requirements

Verify: A dated host checklist records exact host/runtime and evidence for each previously unverified row, including refused audit mutation and writable log/private directories; missing human/environment inputs are named blockers. Closed #74 alone is not used as readiness proof.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-release-acceptance/epic-release-acceptance.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md
