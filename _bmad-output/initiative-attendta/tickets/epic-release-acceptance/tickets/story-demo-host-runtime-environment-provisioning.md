---
id: 1
type: story
title: "Demo host runtime environment provisioning"
parent: epic-release-acceptance
covers: [R4, R5]
after: [1.1, 1.2, 1.3, 6.1]
hitl: true
risk: medium
tracker_id: "74"
remote: "https://github.com/rainyDayssss/AttendTa/issues/74"
tracker_status: done
assignee: "wigglebop25"
---

# Demo host runtime environment provisioning

## Description

An operator provisions the demo host to AD-16: the .NET 10 runtime (AD-18); a local SQL Server instance with the AttendTa_Demo database built from the canonical script (AD-10 allow-list); the runtime SQL login with SELECT and INSERT only on activity_logs (AD-7); the SetupRoot tree with uploads/venues, uploads/events, certificates, temporary and keys (AD-12, AD-16); secrets supplied only as environment variables (AD-16); Playwright Chromium installed during staging (AD-16); and the Argon2id cost parameters e1 decided, benchmarked on this host (spine Deferred); writes the host validation script executed in verification (delivers the demo-host part of R4 and the database and SetupRoot provisioning part of R5).

## Requirements

Verify: An operator runs the host validation script on the demo machine and observes: a SQL connection to AttendTa_Demo as the runtime login; INSERT into activity_logs succeeding while UPDATE and DELETE on it are refused; the SetupRoot directories present and writable; Playwright Chromium launching with the host disconnected from the internet; and the login page returning HTTP 200 on localhost.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-release-acceptance/epic-release-acceptance.md
