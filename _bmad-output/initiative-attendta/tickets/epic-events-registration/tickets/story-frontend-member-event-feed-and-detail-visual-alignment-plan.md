---
title: 'Frontend member event feed and detail visual alignment'
type: 'feature'
ticket: '18'
created: '2026-11-07'
status: 'built'
baseline_revision: '8c81f13'
route: 'full'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The real member event feed and detail work functionally, but their presentation does not yet match DESIGN screens 11–12: card/cover hierarchy, grouped metadata, a distinct registration-status panel, truthful capacity/deadline copy, and a centered cancellation confirmation.

**Approach:** Rework only the MemberEvents Razor markup and scoped presentation CSS. Reuse the existing `IMemberEventService` reads/commands, `RegistrationStatusDisplay`, `EventStatusDisplay`, `IEventStateOverlay`, the global dialog script and the shared pager. No data-layer, service or view-model rule changes.

## Boundaries & Constraints

**Always:** Keep real data as the only source; keep the existing Register/Cancel POST actions and antiforgery; keep the three-column desktop feed grid collapsing to one column; use the canonical `RegistrationStatusDisplay` label/badge rule and the server-computed `IsStartCutoffPassed`; render status text in the panel and badges, never color alone; make the cancellation dialog a centered, keyboard-safe `task-dialog`; keep the member shell and pager.

**Never:** Change registration/cancellation business rules, add demo data, add automated tests, add feed filters or a live-event shortcut not required by the ticket and not backed by a ready real source, hardcode registration labels, or touch the MyEvents host, `_Layout`/`_Header`, or unrelated surfaces.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Empty feed | No published events | Empty-state copy inside the feed region | No error |
| Open with seats | `IsSignupOpen`, not full | "Signup open" badge, confirmed/limit and remaining seats | No error |
| Full but open | `IsFull && IsSignupOpen` | "Full" badge; wording says requests are accepted but a seat is not guaranteed; register form stays | Submit result keeps `SeatNotGuaranteed` message |
| Deadline passed | `!IsSignupOpen` | "Signup closed" badge and closed date; no register form | No error |
| Check-in open | overlay `IsCheckInOpen` | "Check-in open" badge in card/hero | No error |
| Pending before start | registration Pending, cutoff not passed | Panel "Pending review"; cancel shown when `CanCancel` | Dialog confirms/cancels |
| Pending/Waitlisted after start | cutoff passed | "Not confirmed before cutoff", neutral `badge-closed`; no cancel | No error |
| Confirmed before start | Confirmed, `CanCancel` | "Confirmed"; explicit cancel action | Dialog confirms/cancels |
| Waitlisted before start | Waitlisted, `CanCancel` | "Waitlisted"; explicit cancel action | Dialog confirms/cancels |
| Cancelled | Cancelled | "Cancelled"; states no re-registration | No error |
| Eligible, open, full, no registration | `CanRegister && IsFull` | Full warning plus submit action | Submit result keeps no-guarantee message |
| Not eligible | `!CanRegister`, open | "Only active members can register."; no form | No error |
| Stale/closed cancel | POST rejects | Redirect with `TempData["ErrorMessage"]` | Existing controller handling |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.WebApp/Controllers/MemberEventsController.cs` -- `Index` (`GET /Events`, named `MemberEventFeed`), `Details`, `Register`, `Cancel`; applies `IEventStateOverlay` after the real read. No change.
- `ASI.Basecode.Services/Services/MemberEventService.cs` -- `GetFeedAsync` (56–79) and `GetDetailAsync` (81–117) populate `IsSignupOpen`, `IsFull`, `IsStartCutoffPassed`, `CanRegister`, `CanCancel`. Do not change.
- `ASI.Basecode.Services/ServiceModels/MemberEventListItemViewModel.cs` -- read-only fields incl. `StatusLabel`, `IsFull`. Do not change.
- `ASI.Basecode.Services/ServiceModels/MemberEventDetailViewModel.cs` -- read-only fields incl. `StatusLabel`, `IsStartCutoffPassed`, `HasRegistration`, `RegistrationStatus`, `CanCancel`, `CanRegister`. Do not change.
- `ASI.Basecode.Services/ServiceModels/RegistrationStatusDisplay.cs` -- the single registration label/badge rule (`Label`, `BadgeClass`, `IsNotConfirmedBeforeCutoff`). Reuse; do not duplicate.
- `ASI.Basecode.Services/ServiceModels/EventStatusDisplay.cs` -- lifecycle label/badge rule. Reuse.
- `ASI.Basecode.WebApp/Views/MemberEvents/Index.cshtml` -- rewrite card markup and heading to screen 11; keep `_Pager` and cover route.
- `ASI.Basecode.WebApp/Views/MemberEvents/Details.cshtml` -- rewrite as hero + two-column detail; add status panel, guideline card, `task-dialog` cancellation; delete the inline dialog script.
- `ASI.Basecode.WebApp/Styles/features/browse-events.css` -- new scoped stylesheet for feed cards, detail hero/scrim, detail grid, status panel and guideline card.
- `ASI.Basecode.WebApp/Styles/app.css` -- add the new feature import; add `badge-full` to the `@source inline(...)` list because `RegistrationStatusDisplay` emits it from C#.
- `ASI.Basecode.WebApp/wwwroot/css/app.css` -- generated by `npm run css:build`; do not hand-edit.
- `ASI.Basecode.WebApp/package.json` -- `css:build` = `tailwindcss -i ./Styles/app.css -o ./wwwroot/css/app.css --minify`; run from `ASI.Basecode.WebApp`.
- `ASI.Basecode.WebApp/wwwroot/js/site.js` -- global `[data-dialog-open]` / `[data-dialog-close]` handler; submits disable and relabel via `data-submitting-label`. Reuse; no change.
- `ASI.Basecode.WebApp/Views/Shared/_Pager.cshtml`, `_ResultCount.cshtml` -- reuse where the current views already do.
- `ASI.Basecode.WebApp/Views/Shared/_Layout.cshtml`, `_Header.cshtml`, `_NavLinks.cshtml` -- member shell/nav already present (1.13). Do not change.
- `ASI.Basecode.WebApp/DemoData/DemoEventStateOverlay.cs` -- only flips `EventStatusId`/`IsCheckInOpen` after the real read. Do not change.
- `ASI.Basecode.WebApp/Views/Event/Details.cshtml` -- officer-only reference for `task-dialog`, dialog actions and Tailwind utility usage; do not reuse as a shared partial.
- `ASI.Basecode.WebApp/Views/MyEvents/_RegistrationsTab.cshtml` -- consumer of `.event-meta`, `.badge-*`, `.event-card-title`; keep those existing classes working.

## Tasks & Acceptance

**Execution:**
- [ ] `ASI.Basecode.WebApp/Views/MemberEvents/Index.cshtml` -- retitle "Campus Events"; render cover-first cards with a scrim overlaid by type/lifecycle/check-in/signup badges, title, and grouped venue/schedule/registration metadata with truthful full/deadline copy; keep the pager and empty state -- screen 11 hierarchy and metadata.
- [ ] `ASI.Basecode.WebApp/Views/MemberEvents/Details.cshtml` -- add a back-to-events link; render a wide cover hero with dark scrim carrying badges, title, schedule/venue; place description and capacity beside a narrower registration-status panel (using `RegistrationStatusDisplay`) and a registration-guidelines card; keep Register/Cancel POST forms; make cancellation a centered `task-dialog` driven by `data-dialog-open`/`data-dialog-close`; remove the inline script -- screen 12 reading order and states.
- [ ] `ASI.Basecode.WebApp/Styles/features/browse-events.css` -- add responsive feed-card, hero/scrim, two-column detail, status-panel and guideline styles; collapse to one column on mobile -- visual alignment and responsive contract.
- [ ] `ASI.Basecode.WebApp/Styles/app.css` -- import the new feature sheet and add `badge-full` to the inline source list -- keep dynamic badge classes compiled.
- [ ] `ASI.Basecode.WebApp/wwwroot/css/app.css` -- regenerate with `npm run css:build` -- ship the compiled stylesheet.

**Acceptance Criteria:**
- Given published events, when a Member opens `/Events`, then the page shows "Campus Events", a responsive cover-first grid, and accurate open/full/closed/check-in badges with deadline or seat information.
- Given any registration state, when a Member opens an event detail, then the status panel shows the canonical `RegistrationStatusDisplay` label and truthful next action, including "Not confirmed before cutoff" after the start.
- Given a cancellable registration, when the Member activates cancel, then a centered confirmation dialog opens, and confirming performs the existing POST and shows the existing success/error feedback.
- Given a full or past-deadline event, when a Member views it, then the copy states the no-guarantee rule or the closed deadline instead of an unconditional disabled action.
- Given a 390px-wide viewport, when either screen is viewed, then content stacks in the documented reading order without horizontal page scrolling.

## Implementation Notes

## Plan Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| Detail no longer surfaces a "Check-in closed" state for started events. Old `Details.cshtml` rendered a `Check-in` row with `badge-checkin-closed`/"Closed" when `IsCheckInOpen \|\| IsStartCutoffPassed` (`git show 8c81f13:...Details.cshtml`); the new hero renders only `@if (Model.IsCheckInOpen)` -> "Check-in open" (`Views/MemberEvents/Details.cshtml:30-33`) and no closed indicator anywhere. | low | rejected | Confirmed by diffing the old view against the new. Real but negligible information loss: the I/O matrix and Design screen 12 specify only the check-in-open badge, the only reachable window is a Published event that has started while check-in is not open, and the smallest fix adds a new branch (not a direct correction), so it is rejected per the low-finding rule. |

## Design Notes

Authority: `docs/design.md`. Screens 11–12 (lines 324–332) plus the shell/responsive/component contract (lines 180–254), reproduced here so implementation does not re-investigate.

### Page shells and responsive composition (§4)

| Shell | Composition and source details |
|---|---|
| Member | White 64px top navigation with logo, academic-year label, Events, My Passes, History, identity and Profile menu. Main content generally max width 1152px with responsive gutters. No officer sidebar. |

Responsive rules:

- Below **1024px**, replace desktop sidebar with navigation drawer; never squeeze the same sidebar beside a phone-width table.
- Member event cards follow their source grid; preserve the three-column desktop composition on p.33 and collapse toward one column. Passes use two columns from 768px and one below.
- Registration field groups collapse from two/three columns to one below 640px. Profile record grids collapse below 768px.
- Tables retain meaningful column labels. Use contained horizontal scrolling for dense operational tables; use card conversion only where the prototype explicitly provides it.
- Wrap toolbars and filters without hiding primary actions, selection state, or result count. Do not let the document itself scroll horizontally.
- Dialogs remain centered. At widths up to 600px, use 12px outer clearance, 20px body padding, and single-column field grids.
- Preserve all meaningful information at narrow widths; sample desktop density is not permission to clip names, actions, validation messages, or QR codes.

### Shared component contract (§5)

**Buttons and links.** Shared sizes: small 32px high, default 40px, large 48px; default horizontal padding 16px and icon gap 8px. Dark primary buttons use white text; white secondary buttons use a thin border; ghost controls are quiet; destructive buttons are red. Use the screenshot's actual action emphasis rather than making every action amber. Provide rest, hover, focus, pressed, disabled, and submitting states. Keep labels visible while submitting and prevent repeat submission. Do not use color alone to distinguish destructive actions. Scanner controls provide 44px minimum targets, increasing to 56px for coarse pointers in the current source.

**Forms.** Use labels above fields, helper text beneath, clear required markers, and inline error text near the relevant field. Shared inputs/selects are 40px high, with 12px horizontal padding and 6px radius. Textareas start at 96px high. Preserve page-specific tighter controls where shown. Show existing values in edit forms. Read-only records must look read-only, not like broken editable inputs. On validation failure retain user input, identify the field, and keep the correction visible. Upload controls show the existing image or selected preview plus format/required-photo guidance.

**Tables, tabs, filters, and pagination.** Use white table surfaces, subtle header rows, thin horizontal rules, left-aligned descriptive text, and aligned numeric columns. Keep the title/filter toolbar, table, and footer visually connected. Shared ledger headers use 10px × 12px padding; root page tables often use 12px × 16px. Match the individual screen. Tab selection must remain visible with label, border/background, and count where shown. Search and status filters sit above records. Place result count and pagination under the table; do not repeat page-size implementation details in the top bar or footer branding. Empty results retain the table context and explain whether no records exist or filters matched none.

**Badges and status.** Keep badges small with restrained tinted backgrounds and readable labels. Distinguish these domains:

- Event lifecycle: draft, published, completed, cancelled.
- Registration: pending, confirmed, waitlisted, cancelled.
- Attendance: recorded/valid and invalidated, with correction context.
- Certificate: pending review, approved, declined, issued.
- Account and membership: separate active/inactive statuses.

"Live," "upcoming," and deadline labels describe timing; they must not silently replace lifecycle or registration state. Counts and controls must reflect the same record state.

**Dialogs.** Canonical source: `dialogs.css`, supported by `dialogs.js`. Task forms and confirmations are **centered dialogs**; drawers are for navigation. Old element IDs containing "drawer" do not change this visual contract.

- Standard dialog: max width 480px. Record editor: max width 680px. Both fit the available viewport.
- White panel, 16px radius, 1px slate border, modal shadow, 24px normal outer clearance; maximum height viewport minus 48px with internal scrolling.
- Overlay: `rgb(15 23 42 / .48)` with 3px background blur.
- Body padding 24px; footer usually 16px × 24px, 8px action gap; title leaves space for a 32px close button at top right.
- Record editors place a status/destructive action separately from Cancel and Save. Destructive confirmations use an explicit verb and reason field where required.
- Focus moves into the dialog, stays inside while open, and returns to the trigger on dismissal. Associate title and description; keep background content inert. Closing must never submit or confirm.

**Feedback and system states.** Use inline feedback for field errors and blocked decisions; use the existing toast pattern for nonblocking completion messages. Announce meaningful updates to assistive technology. Do not use browser-native alert/confirm dialogs to replace HLD dialogs. Every data-driven screen needs loading, populated, empty, error/retry, and unavailable/unauthorized behavior appropriate to its context. These are implementation requirements, not additional HLD screen numbers. Preserve the shell during loading and errors; never use an indefinite blank white panel.

### Screen 11 — Browse events (§6, line 326)

Member shell, "Campus Events," compact white live-event shortcut, filter pills, and a three-column desktop event grid. Each card shows cover image, category/timing badges, title, venue/schedule, registration information, and View details. Retain neutral canvas and white surfaces per `browse-events.css`. Do not revive historical animated backdrop or enlarged live-hero treatments that differ from p.33. Collapse columns responsively and retain useful empty/search-result feedback.

### Screen 12 — Member event details (§6, lines 328–332)

Member shell with back-to-events link. Wide photo hero has a dark scrim, readable badges, title, and schedule/venue. Under it, description and capacity sit beside a narrower registration-status panel and guideline card. Preserve that reading order when stacked on mobile.

P.35 shows confirmed registration and pass/cancel actions; p.36 shows signup closed; p.37 shows the centered cancellation confirmation. Pending and waitlisted states reuse the same status panel with truthful labels and allowed actions. Full capacity may permit a request with a waitlist warning if the PRD allows it; do not replace all full states with an unconditional disabled action.

### Ticket scope notes

- The compact live-event shortcut and filter pills are named on screen 11 but are outside this ticket's stated focus (cover hierarchy, metadata, registration-status panel, truthful capacity/deadline messages, cancellation dialog). Add neither unless a ready real source exists.
- "Full" here means the existing `RegisterAsync` still creates a Pending request with `SeatNotGuaranteed`; copy must say a seat is not guaranteed, not that the request is rejected.
- Cancellation dialog has no reason field (PRD G2); the explicit destructive verb is "Yes, cancel my registration".

## Verification

**Commands:**
- `dotnet build "ASI.Basecode.WebApp/ASI.Basecode.WebApp.csproj"` -- expected: build succeeds with no errors.
- `npm run css:build` (working directory `ASI.Basecode.WebApp`) -- expected: `wwwroot/css/app.css` regenerates and contains the new feature rules.
- `git diff --check` -- expected: no whitespace errors.

**Manual checks:**
- Run with `$env:DemoData__Enabled = "true"` and sign in as a Member; open `/Events` and an event detail at desktop and 390px widths. Confirm the feed hierarchy, truthful badges/deadline/seats, the detail hero/scrim, status panel states (Pending, Confirmed, Waitlisted, Cancelled, cutoff), guideline card, and the centered cancellation dialog; confirm Register/Cancel still POST and show existing feedback.
