---
id: 2
type: story
title: "Hotspot network binding and trusted HTTPS mobile camera scanning"
parent: epic-release-acceptance
covers: [R4]
after: [1, 14, 4.4, 4.5]
hitl: true
risk: high
tracker_id: "75"
remote: "https://github.com/rainyDayssss/AttendTa/issues/75"
tracker_status: backlog
---

# Hotspot network binding and trusted HTTPS mobile camera scanning

## Description

Decides the HTTPS mechanism and host address for the hotspot with Technical Manager Gabutan (Q2) and records the decision in this epic's Notes; then configures Kestrel to serve HTTPS directly on the hotspot LAN interface, with a certificate the officers' phones trust and no IIS or reverse proxy (AD-16); then verifies phone Chrome camera QR scanning where the 4.4 scanner page is served over the hotspot HTTPS origin here, not only localhost, filing scanner defects as bugs back to feature epics.

## Requirements

Verify: With the host disconnected from the internet, an officer opens the hotspot HTTPS URL in mobile Chrome with no certificate warning, grants camera access, scans a valid attendee QR Pass and sees the attendee's name and student number, and finds the same attendee through manual lookup on that phone.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-release-acceptance/epic-release-acceptance.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md

## Delivery (2026-11-06)

- Operations verification of the real phone scan over trusted HTTPS; it can run in parallel with 8.3 once the host is ready, and human inputs (host address, certificate, officer phones) remain required.
- 8.14 is the new real-host follow-up to closed #74; this entry consumes its host/HTTPS evidence rather than treating tooling delivery as acceptance.
- This is real host acceptance, not a UI build; prototype or localhost-only results do not close the issue.
- Development check: host checklist plus a focused scan/manual-lookup smoke; existing CI unchanged. Missing important test authoring is 8.5 (#78), combined final execution 8.4 (#77), manual 8.6–8.8.
- Operations: no Impeccable requirement; frontend design lives only in the separate slices 6.12/6.13/7.4 and finale 8.12.
- Q2 stays open until the recorded host/HTTPS/phone evidence exists; trust from the certificate chain on a second phone stays final acceptance.
