# Review log: demo-rehearsal-8-15

Target: commit `bc12082` on `feature/135-demo-rehearsal` — "feat(demo): [BMAD 8.15] rehearse the demo surface and record findings"

## 1 — session — start

Session: unavailable · Timestamp: 2026-11-07T12:30:14+08:00

- Action: Opened the review; created the narrative and this log.
- Result: Review started. Target is commit `bc12082`; ticket is `_bmad-output/initiative-attendta/tickets/epic-release-acceptance/tickets/story-demo-rehearsal-with-impeccable-critique-and-polish.md` (plan: matching `-plan.md`).
- Evidence: `walkthrough-demo-rehearsal-8-15/walkthrough-demo-rehearsal-8-15.md`
- Open: No block reviewed yet; block 1 (Intent) is current.

## 2 — Intent — second opinion

Session: unavailable · Timestamp: 2026-11-07T12:47:13+08:00

- Action: A fresh subagent, with no review context, checked the Intent block against the ticket and `git show --stat bc12082`.
- Result: One coherent goal; the changed files match its shape. It raised scope questions: the Home cards, the 583-line seed and the new guard test sit near "adds no new screens or backend" and "no new automated tests"; "demo-critical screens" and "highest-value findings" are undefined; the demo-polish tickets' Verify lines are not individually recorded (the findings only describe them collectively); 4.12 is Partial; the tracer check ran on a local Production build, not the demo machine (8.14 owns the host).
- Evidence: ticket Description, `git show --stat bc12082`, `demo-rehearsal-findings.md` §6.
- Open: The reviewer decides whether any point changes the intent or the deliverable.

## 3 — Intent — second opinion and fix

Session: unavailable · Timestamp: 2026-11-07T12:49:51+08:00

- Action: A fresh second opinion checked the demo-polish Verify gap. The findings and coverage map gained six per-ticket rows (3.18, 4.13, 2.12, 2.13, 2.14, 2.10), each quoting its Verify line and marked Partial with the missing checks; 8.15's result names them.
- Result: Every demo-phase ticket now has a recorded Verify result; the six polish rows are Partial and recorded against their tickets for 8.12. Commit `a040393`.
- Evidence: `demo-rehearsal-findings.md` §6, `demo-script.md` §9, plan Implementation Notes.
- Open: Block 1 is still current; the reviewer decides whether it is done.

## 4 — Intent — accepted

Session: unavailable · Timestamp: 2026-11-07T12:52:21+08:00

- Action: The reviewer accepted block 1 after the second opinion and the demo-polish Verify fix.
- Result: Block 1 done; block 2 (Broad strokes) is current. The six demo-polish rows stay Partial and recorded for 8.12.
- Evidence: narrative Blocks list.

## 5 — Broad strokes — drive and test

Session: unavailable · Timestamp: 2026-11-07T12:53:45+08:00

- Action: Started the demo build on http://localhost:5089 against the seeded AttendTaMVP_Test with DemoData enabled; reset the three demo accounts to a local temp password; checked the startup log, the login page and the seed counts.
- Result: Startup log "Check-in open event 3; ready to complete event 1"; /Account/Login returned 200; the seed holds 2 published events, 1 cancelled event and 8/3/3 registrations on the three events; 361 integration tests pass.
- Evidence: `%TEMP%\opencode\attendta-app.log`, DB counts, test run.
- Open: The reviewer is driving the beats; the app is still running.
