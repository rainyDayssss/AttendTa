---
id: 2
type: story
title: "Fixed-template landscape A4 Razor certificate rendering with embedded fonts"
parent: epic-certificates
covers: [R5]
after: [1, 1.4]
hitl: false
risk: high
tracker_id: "62"
remote: "https://github.com/rainyDayssss/AttendTa/issues/62"
tracker_status: done
assignee: "Wynnwrath"
---

# Fixed-template landscape A4 Razor certificate rendering with embedded fonts

## Description

Implements the fixed landscape A4 Razor certificate template in WebApp/Rendering with locally embedded Cinzel fonts, institutional branding, bindings for recipient name, Event title, Event start, issue date and the certificate number, dates formatted through the shared Asia/Manila formatter with its zone label (AD-11), and a visible development watermark until entry 3, delivering R5's fixed-template PDF part.

## Requirements

Verify: Rendering a certificate from the Razor template produces a landscape A4 PDF with embedded Cinzel fonts, the snapshot values, the certificate number and dates shown in Asia/Manila with a label, a visible development watermark, and no external network requests.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-certificates/epic-certificates.md
