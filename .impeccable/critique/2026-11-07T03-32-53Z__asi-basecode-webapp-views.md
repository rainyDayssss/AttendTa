---
target: 8.15 demo surface (Home, Event, Check-in, Certificates, Reports, Members, Account requests, Accounts, Event types, Venues, Activity log, Member feed, My Events, Passes, Profile)
total_score: 28
max_score: 40
na_heuristics:
p0_count: 0
p1_count: 0
target_identity: "file:F:\\AttendTaBMAD\\ASI.Basecode.WebApp\\Views"
timestamp: 2026-11-07T03-32-53Z
slug: asi-basecode-webapp-views
closed: true
---
⚠️ DEGRADED: single-context (nested subagents blocked by the harness depth limit; detector + full-role walker evidence used)

# Impeccable critique — 8.15 demo surface

Target: `ASI.Basecode.WebApp/Views` — Home, Account/Login, Event, EventType, Venue, Member, AccountRequest, AdminAccounts, ActivityLog, RegistrationReview, CheckIn, CertificateLedger, MemberEvents, MyEvents, Passes, Profile, Reports, Shared.
Mode: Operate.
Date: 2026-11-07.
Evidence: 102 page records at 1440×900 and 390×844 for Administrator, Officer, and Member (`%TEMP%\attendta-walk-output\report.json`), plus the interactive beats in `demo-rehearsal-findings.md`.
Detector: `impeccable detect --json ASI.Basecode.WebApp/Views` returned `[]` (clean, exit 0).

## Design Health Score

| # | Heuristic | Score | Key Issue |
|---|-----------|-------|-----------|
| 1 | Visibility of System Status | 3 | Notices, counts, loading states, and status badges are clear; the report still needs a Show click before content appears |
| 2 | Match System / Real World | 3 | Plain campus language; "View dossier" and "Manage event" both open the same page |
| 3 | User Control and Freedom | 3 | Cancel/back/close everywhere; no undo, but destructive actions confirm |
| 4 | Consistency and Standards | 3 | Shared dialogs, buttons, badges, and tables; the create-event form lacks the Publish action DESIGN.md lists |
| 5 | Error Prevention | 3 | Required reasons, confirmations, and server guards; the cover requirement is only discovered after a failed publish |
| 6 | Recognition Rather Than Recall | 3 | Labelled navigation and role-aware Home cards; the report's Show step is not obvious from the selected event |
| 7 | Flexibility and Efficiency | 2 | No bulk actions or keyboard shortcuts; acceptable for the demo scope, weak for daily officer use |
| 8 | Aesthetic and Minimalist Design | 3 | Restrained slate canvas, dense tables, limited amber; event rows carry five actions |
| 9 | Error Recovery | 3 | Inline errors and retained input; the publish refusal message is generic |
| 10 | Help and Documentation | 2 | Inline hints and registration guidelines; no task-focused help surface |
| **Total** | | **28/40** | **Good** |

## Design Specificity Verdict

**LLM assessment:** The surface is now authored for a campus organization: event dossiers, door passes, certificate eligibility, and per-event participation reports carry the product's own vocabulary. The shared shell, tables, and dialogs are deliberately conventional for Operate mode, which is correct. The weakest specificity is the create-event flow, which still reads like a generic CRUD form and hides the publish/cover step.

**Deterministic scan:** `impeccable detect --json ASI.Basecode.WebApp/Views` returned an empty array (clean, exit 0). No overused-font, contrast, or markup findings were reported.

**Visual overlays:** Not attempted. The app is an authenticated MVC app, not a static page, and no overlay was injected.

## Overall Impression

The demo surface is coherent, calm, and trustworthy. The role-aware Home page removes the old dead end, the shared dialogs are consistent, and the pass/certificate/report surfaces look like one product. The biggest remaining opportunity is the create-event flow: it should make the draft → cover → publish path visible in one place instead of sending the presenter to another screen and a generic refusal.

## What's Working

1. **One visual system across roles.** The slate canvas, white panels, thin borders, dark navy primary actions, and limited amber hold from the member feed to the certificate ledger. The pass cards and report cards feel like the same product.
2. **Decision surfaces explain consequences.** The completion, cancellation, deactivation, decline, and issue dialogs name the record, the consequence, and the required reason. That is the right pattern for an auditable system.
3. **The member path is legible.** Browse → detail → registration status → pass → certificate history reads in the order a student expects, and the QR/pass card is the strongest product-specific moment.

## Priority Issues

- **[P2] Create-event flow hides publish and cover.** The form saves a draft, but publishing is on the Events list and requires a cover upload. A first-time presenter discovers this only after a failed publish.
  **Why it matters:** it turns the demo's "create and publish" beat into a two-screen scavenger hunt and produces a generic error.
  **Fix:** add a Publish action to the create/edit form, or a clear inline step that links to the cover upload and returns to publish. Make the refusal name the missing cover.
  **Suggested command:** `/impeccable clarify` or `/impeccable polish`.

- **[P2] Event rows carry redundant actions.** "View dossier" and "Manage event" open the same page, next to Cover, Edit, Cancel, and Publish/check-in controls.
  **Why it matters:** five actions per row slow scanning and make the primary next step unclear.
  **Fix:** keep one dossier link and group lifecycle actions behind it; leave the row to the one action that matches the status.
  **Suggested command:** `/impeccable distill` or `/impeccable layout`.

- **[P2] Member attendance history is empty in the demo.** The scanner and report show check-ins, but My Attendance says "No attendance to show yet" until 4.7 (M-08).
  **Why it matters:** the participant record appears to lose the attendance the demo just recorded, which undercuts the "one auditable lifecycle" claim.
  **Fix:** land the real owner-scoped attendance read (4.7) or add a clearly labelled demo read; do not leave the tab empty in the client demo.
  **Suggested command:** `/impeccable onboard` for the empty state, but the real fix is 4.7.

- **[P2] The report needs a Show click after an event is already selected.** The selector shows Leadership Summit, but the panel says "Select an event above and choose Show".
  **Why it matters:** the screen looks empty even though a valid selection exists; a presenter can mistake it for missing data.
  **Fix:** load the first completed event by default, or submit the selector on change.
  **Suggested command:** `/impeccable clarify` or `/impeccable polish`.

- **[P3] The publish refusal is generic.** "This event no longer meets the publication requirements" does not name the missing cover or the inactive master data.
  **Why it matters:** the user cannot recover without guessing.
  **Fix:** name the specific unmet requirement in the message.
  **Suggested command:** `/impeccable clarify`.

## Persona Red Flags

**Alex (power user):** no bulk actions or keyboard shortcuts; five actions per event row; the report requires an extra Show click; the certificate ledger is one-at-a-time by design (correct, but slow for a large cohort).

**Jordan (first-timer):** "Save draft" looks like the end of the create flow; the cover and publish steps live on another screen; the generic publish refusal gives no next step; the report's selected event looks broken until Show is clicked.

**Sam (accessibility-dependent):** the shared dialogs, tables, and skip link are keyboard-operable and the QR canvas has `role="img"` with an accessible name. The remaining risk is the dense event row: five adjacent actions with similar labels increase tab stops and misclick risk. The report's Show step is a focus target but not announced as the required next action.

## Minor Observations

- The Home cards use the same weight for every shortcut; a "next action" card (for example, pending requests or the next event) would help a presenter start.
- The event list's "Cover" action is a separate screen; it could be part of the create/edit flow.
- The member attendance empty state is honest but does not explain that the scanner/report already hold the check-in.
- The Activity Log is intentionally sparse in the demo; the presenter should say so rather than let it look broken.
- The login page now matches DESIGN.md's heading and helper copy.
- The report's zero-confirmed N/A path is present but not exercised by the seeded state.

## Questions to Consider

- Should the create-event form own the whole draft → cover → publish path, or is the two-screen flow a deliberate separation of concerns?
- What if the event row had one primary action per status instead of five equal buttons?
- Should the member attendance tab read the demo store until 4.7, or is the empty state the honest demo?
