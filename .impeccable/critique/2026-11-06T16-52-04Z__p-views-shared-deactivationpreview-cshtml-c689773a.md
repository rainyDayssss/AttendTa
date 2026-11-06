---
target: "Issue #153 preliminary shared-dialog polish"
total_score: 29
max_score: 40
na_heuristics:
p0_count: 0
p1_count: 0
target_identity: "file:C:\\Users\\JEHRON\\Documents\\Programming\\ALLIANCE\\AttendTa\\ASI.Basecode.WebApp\\Views\\Shared\\_DeactivationPreview.cshtml"
target_fingerprint: "sha256:58be9e2525f7158b7da8cecdbe6fa82efc758be35b6bee61244206fe9b3bf830"
target_path: "C:\\Users\\JEHRON\\Documents\\Programming\\ALLIANCE\\AttendTa\\ASI.Basecode.WebApp\\Views\\Shared\\_DeactivationPreview.cshtml"
timestamp: 2026-11-06T16-52-04Z
slug: p-views-shared-deactivationpreview-cshtml-c689773a
closed: true
---
DEGRADED: single-context Impeccable critique — no subagent tool is exposed.

# Preliminary shared-dialog critique

Scope: existing approval, registration-cancellation and deactivation-preview dialogs. Source inspection plus isolated Chromium renders, not the final 20-screen walkthrough. Visual authority: docs/DESIGN.md. Mode: Operate.

## Design specificity and impression

Outfit typography, navy primary actions, restrained borders, compact decision hierarchy and labelled controls fit AttendTa. Preserve the existing visual world; no redesign needed. The main opportunity was safe, centered interaction, not new decoration.

## Heuristic scores

| Heuristic | Score | Evidence / limitation |
|---|---:|---|
| System status | 3/4 | Explicit headings, existing submitting labels |
| Real-world language | 3/4 | Clear approval, cancellation and deactivation consequences |
| User control | 3/4 | Escape/focus restore passed, explicit exits retained |
| Consistency | 3/4 | Shared centering and scrolling across three variants |
| Error prevention | 3/4 | Interior clicks preserved, server guards unchanged |
| Recognition | 3/4 | Text-labelled actions and consequence copy |
| Efficiency | 3/4 | Keyboard entry/exit checked, background excluded from Tab navigation |
| Minimalist design | 3/4 | One decision per dialog, no added decoration |
| Error recovery | 2/4 | Preview error test retained, authenticated recovery not walked |
| Help | 3/4 | Contextual consequences retained |
| Total | 29/40 | Good at component level only, not release certification |

## What's working

- Compact readable hierarchy with clear primary/secondary actions.
- Native Escape and focus restoration.
- Mobile cancellation actions wrap without changing meaning.

## Priority issues

- P1, corrected: interior clicks dismissed dialogs. The shared handler closed any click targeting the dialog, including padding. Bound checks now dismiss only outside clicks. Node regressions cover padding, edges, children and four outside directions. Suggested command: impeccable polish.
- P1, corrected: centering and long-content containment were unreliable after Tailwind's margin reset. Explicit margins restore centering; shared height bounds, scrolling, long-word wrapping and action wrapping keep controls reachable. Suggested command: impeccable polish.

## Persona and cognitive-load checks

Sam: keyboard entry, Escape, focus restoration and background exclusion checked; screen-reader announcement not manually verified. Casey: phone actions reachable with long content; real device touch and browser zoom not verified. Officer: existing dense visual identity retained. One decision and at most three local controls per reviewed dialog; no new options, recall burden or cognitive load introduced. Consequence copy reassures before high-stakes actions. Full role journey is blocked.

## Detector and rendered evidence

`impeccable detect --json ASI.Basecode.WebApp/Views` returned `[]`. No false positives or locations returned. Source-only result is not accessibility certification. Browser overlay not injected; no claim of user-visible overlays. Custom Playwright smoke used the existing installed runtime, exact dialog snippets with server-only Razor attributes removed, compiled CSS, jQuery and site.js on a temporary loopback server. It made no authenticated requests or mutations.

All three variants passed at 1440x900, 1024x768, 768x1024, 390x844 and 720x450. Checked centering, interior and outside clicks, Tab exclusion of background controls, Escape/focus restore, long unbroken content scrolling, reachable actions and no page errors. Native Tab can visit browser chrome/body between cycles; this is not a custom focus-trap certification. Screenshots inspected at desktop and phone sizes, including normal and long-content actions. Temporary evidence: `%TEMP%/attendta-153-dialog-evidence/`; smoke script: `%TEMP%/attendta-153-dialog-check.cjs`. Server and browser close in finally. These files are local transient evidence, not committed screenshot automation.

720x450 is only a half-size viewport reflow proxy, not actual 200% browser zoom. Final integrated role routes, 20 screens, real PDF/QR, error/retry journeys and rehearsal findings remain unverified pending #152/#135. Minor observation: the existing 32px task-close icon remains unchanged; explicit full-size Cancel is retained.

## Polish result

Final normal and long-content/action renders retain the incumbent hierarchy and palette. Both scoped priority issues corrected; no further presentation changes justified in this pass. Actual 200% browser zoom and full integrated acceptance remain release-validation blockers. No backend changes or demo seam removal.

Questions skipped: two Priority Issues were identified, both within the authorized preliminary scope.
