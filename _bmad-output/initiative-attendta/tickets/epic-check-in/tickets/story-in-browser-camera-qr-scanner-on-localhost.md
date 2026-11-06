---
id: 4
type: story
title: "In-browser camera QR scanner on localhost"
parent: epic-check-in
covers: [R4]
after: [2, 3, 11]
hitl: true
risk: high
tracker_id: "47"
remote: "https://github.com/rainyDayssss/AttendTa/issues/47"
tracker_status: backlog
---

# In-browser camera QR scanner on localhost

## Description

Points the camera adapter and decoder that 4.11 built at the 4.3 check-in endpoint, posting each decoded token with the RequestVerificationToken header from the layout meta tag (AD-8) and showing the JSON outcome and message (AD-9). The page also runs on the hotspot HTTPS origin in 8.2 (no hard-coded localhost); this entry verifies it on localhost with a laptop camera. The decoder stays served from wwwroot with no runtime CDN (AD-14). Scan payloads are not logged (delivering the camera scan interaction of R4).

## Requirements

Verify: An Officer opens the scanner on localhost on a laptop webcam, points it at a Confirmed Member's QR Pass, and sees name and student number on success; presenting the same pass again shows the original check-in time; the browser network log shows no request to any host other than localhost.

## References

- parent — _bmad-output/initiative-attendta/tickets/epic-check-in/epic-check-in.md
- docs/DESIGN.md
- _bmad-output/initiative-attendta/delivery-queue.md
- _bmad-output/initiative-attendta/finale-test-cases.md
- _bmad-output/initiative-attendta/demo-mock-register.md

## Delivery (2026-11-06)

- Frontend-facing integration: the scanner screen and its separate camera adapter are already delivered by 4.11. This issue wires the real camera adapter to the engine (4.3), does not rebuild the screen, and does not gate the manual adapter 4.5.
- No per-ticket Impeccable critique or polish; 8.12 covers it.
- Development check: build/CSS plus one real scan, one duplicate and the camera-denied fallback; existing CI unchanged. The full matrix and the phone/hotspot run are 8.5 (#78) and 8.6–8.8; the full suite runs once at 8.4 (#77).
- Final rules unchanged: no hard-coded localhost origin, assets served locally with no runtime CDN, antiforgery header read from the layout meta tag, and no scan payload in logs.

## Notes

- Wire 4.11's camera adapter and decoder; do not rebuild the screen or choose another decoder. Keep development testing to one real scan, a repeat and camera-denied fallback; the full matrix and phone test run at the finale.
- Removes no register row (2026-11-06 review): 4.3 removes M-05; this ticket verifies 4.11's camera adapter against the real engine.
