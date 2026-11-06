---
id: 1
type: story
title: "Playwright .NET setup and certificate PDF issuance tracer"
parent: epic-certificates
covers: [R5, R6]
after: [1.1, 1.2, 2.2]
hitl: true
risk: high
tracker_id: "61"
remote: "https://github.com/rainyDayssss/AttendTa/issues/61"
tracker_status: done
assignee: "Wynnwrath"
---

# Playwright .NET setup and certificate PDF issuance tracer

## Description

Pins Microsoft.Playwright 1.64.1 in ASI.Basecode.WebApp, with Chromium installed on the dev host, and adds the Services-side certificate renderer interface with its WebApp Playwright adapter; implements a minimal issuance path that renders the A4 landscape PDF to bytes, saves them to the temporary area, promotes them to certificates/ through IPrivateFileStore and streams the promoted file back through an authorized action that calls a Services use case, delivering the initial tracer path for R5 and R6 on the existing certificates mapping from epic-platform-access (1.2); as the tracer it intentionally skips the certificates row write, lock and audit, which arrive in entry 7.

## Requirements

Verify: An Officer triggers the tracer action for a Confirmed Registration row inserted by a test SQL fixture; the action calls the issuance service, and the browser receives a landscape A4 PDF streamed as application/pdf from a relative certificates/ path whose bytes start with %PDF-.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-certificates/epic-certificates.md
