# AttendTa UI/UX phased implementation plan

Status: implementation in progress on `feature/ui-ux-consistency`. Commit and stop after each phase for review.
Updated: 2026-11-15.
Backend follow-up: [frontend/backend alignment handoff](handoffs/ui-ux-backend-alignment-handoff.md).
Baseline last verified: local dev tracked upstream/dev at da1f048. Recheck Git state before implementation and preserve concurrent work.

## Progress

| Phase | Status | Commit |
| --- | --- | --- |
| 1. Shared layout and component foundations | Complete, including convention correction | `f3c8804`, `ba9bbbc` |
| 2. Compact tables, search, filters and pagination | Implemented, including approved refinements; full integrated visual sign-off remains Phase 7 | `bf315bf`, `537110a`, `5b8cb9b`, `d834616`, `c673b05`, `1d7b3ea`, `f1998c9`, `040fd43`, plus final consistency cleanup |
| 3. Draft events, publishing and cover management | Not started | — |
| 4. Registration and certificate review, including bulk approval | Not started | — |
| 5. Event detail hierarchy and reports | Not started | — |
| 6. Login and remaining member journeys | Not started | — |
| 7. Integrated critique, polish and verification | Not started | — |

## Agreed scope

Improve affected management and member pages using the existing AttendTa visual identity. Shared components establish consistent spacing, typography, controls, tables, dialogs, navigation and feedback. Update DESIGN.md and PRODUCT.md to reflect agreed behavior; the user's direction supersedes conflicting old row-link/timezone-caption rules.

Bulk approval applies to Membership Account Requests, Registration Review, the event dossier pending roster, and Certificate Eligibility Review. Declines/cancellations and certificate issuance remain individual. Certificate eligibility approval does not issue a PDF. No bulk issuance.

Keep essential identity, comparison and decision data visible. Move secondary information into contextual details dialogs without inventing or exposing data the user is not authorized to view. Details should not require chains of modals.

## Phase 1 — Shared layout and component foundations

Targets: management/member/public shells, Styles/app.css and generated CSS, shared controls, shared dialogs, DESIGN.md and PRODUCT.md.

- Align management content to a consistent 24px gutter beside the sidebar; use 16px on small screens. Remove extra centering gaps for operational lists while retaining readable widths for prose/forms. Align breadcrumbs, headings, toolbars and tables.
- Standardize page/section spacing, panel padding, typography, field/label/helper spacing and status styles.
- Replace text-like row-action styling with proper bordered buttons. Define intentional main-action, row-action and dialog-action sizes, consistent hover/focus/disabled/loading states, and usable touch targets.
- Establish shared search/filter toolbar, read-only details dialog, decision dialog and pagination-footer patterns.
- Standardize Close/Cancel/Confirm placement and keyboard focus. Protect modified forms/reasons from accidental dismissal; unchanged/read-only dialogs close freely. Avoid nested modal chains.
- Remove repeated visible Asia/Manila labels while retaining existing time conversion.

Completion: representative list, form and dialog share alignment and control sizes; CSS builds; keyboard and responsive checks pass. Changes to shared dialog dismissal preserve existing save/retry behavior.

## Phase 2 — Compact tables, search, filters and pagination

Targets: shared PageRequest/PagedResult/pager/result-count contracts and corresponding controllers/services/repositories; management list views.

Visible information and details:

| Page | Visible | Details |
| --- | --- | --- |
| Membership requests | Applicant/student number, course/year, requested date, status, actions | Email, mobile, section, remaining application information, recorded decline reason |
| Members | Name/student number, email, course/year/section, status, actions | Mobile, membership type; student number retained as identity context |
| Events | Title/type, venue, start date/time, status, actions | Deadline, participant limit, description and secondary event information |
| Registration review | Participant/student number, event, status, actions | Exact submission time and secondary registration information |
| Dossier roster | Participant/student number, status, actions | Submission time and secondary registration information; event context remains above roster |
| Certificate eligibility | Member/student number, attendance confirmation, eligibility status, actions | Exact attendance time, full reason, available certificate details |
| Issued registry | Member/student number, certificate number, issue date, View PDF | Exact issuance time and attendance information |
| Reports | Participant/student number, course/year, registration status, attendance result | Exact check-in time and additional available participant information |
| Event types | Name, description preview with inline Read more only when clipped, status, actions | No separate details modal; expand description in the row |
| Venues | Name, location, capacity, status, actions | Availability notes, contact person, full photo |
| Accounts | Name, email, role, status, actions | Add details only if meaningful additional information exists |
| Activity log | Date/time, actor, action, entity type, details action | Entity ID, actor type, full reason |

- Add/standardize server-backed search and relevant filters. Search keeps its label; one Reset clears search and list filters while preserving the selected event/history section. This replaces the earlier Search-to-Clear proposal and separate clear-filters control.
- Preserve search/filter/sort/event context across pagination and actions. Changed search/filter resets to page 1.
- Replace fixed 25-row paging with validated server-backed page sizing and Auto-fit. Start at five, then measure actual row height and available window space to show 1–25 records. The event dossier uses a five-row minimum with normal page scrolling so its summary cannot reduce the roster to one person per page. Taller viewports can grow beyond five; unrestricted manual 10/25 choices are superseded by this agreement.
- Measure available content space, not monitor height. Keep page/count metadata synchronized with fetched rows. Avoid unstable resize/refetch loops; page-size changes preserve a meaningful position where possible and clear selection.
- Standardize footer: 'Showing 1–5 of 42 members', filtered totals, exact last-page ranges, 'Page 1 of 1', visible disabled boundary navigation and useful zero-result states. No fabricated totals or pagination when there are zero pages.
- Remove desktop horizontal scrolling through column reduction and wrapping. Use a labelled stacked presentation for narrow screens when necessary. Do not shrink essential text to force a fit.

Completion: small/large/filtered/empty datasets have correct counts and boundaries; no omitted/duplicated records due to page calculations; list context survives navigation; normal desktop tables have no horizontal or internal vertical scrollbar. Natural page scrolling remains available on phones, zoom and long content.

## Phase 3 — Draft events, publishing and cover management

Targets: event create/edit modal and fallback forms, event list/dossier publishing flow, cover view/controller/service responses.

- Group fields into Event information, When and where, Registration and capacity.
- Rename Starts to Event starts and Signup deadline to Registration closes. Explain registration opens immediately on publishing; no scheduled registration-opening field exists.
- Show inline date relationship/capacity guidance. Distinguish draft-save validity from publish readiness.
- Show publication checklist with specific problems and direct fixes: cover missing, registration deadline elapsed, inactive venue/type, capacity mismatch, or changed event state.
- Replace generic publication failure text with specific actionable reasons; backend validation remains authoritative.
- Review schedule, venue, deadline and capacity before publishing; explain which fields become locked afterward.
- Implement Add/Manage Cover as a reusable modal from list/dossier/editor workflow, with current image, selected-image preview, actual type/size guidance, Save/Cancel and confirmed permitted removal. Preserve standalone fallback route.
- Do not stack cover/editor modals or discard an unsaved event editor to switch tasks. Save first or offer an explicit safe transition. Refresh readiness after cover save.

Completion: valid publish path and each blocked condition are understandable; failures preserve input and identify the fix; cover preview/removal and post-publication field restrictions are clear.

## Phase 4 — Registration and certificate review, including bulk approval

Targets: Membership Account Requests, Registration Review, dossier pending roster, Certificate Eligibility Review; shared selection/confirmation/result components; approval endpoints and services.

- Remove always-visible decline/cancellation reason fields; open contextual dialogs on choosing the action. Repeat target identity/event and consequences; preserve reason/context on validation failure.
- Add eligible-pending-row checkboxes, selected count, Select all on this page, Deselect all, and Approve selected. Selection clears on page/search/filter/event changes.
- Confirm selected names/count before submission. Recheck authorization and per-record eligibility on the server; audit each successful approval and return understandable per-record results.
- Registration bulk approval obeys existing capacity, event cutoff and waitlist-priority rules; it must not silently bypass or change them. Review those rules before choosing batching/transaction details.
- Treat certificate eligibility approval as a separate step from individual PDF issuance. Preserve finality warnings on individual declines/issuance.
- Keep ordinary individual approval available.

Completion: valid, mixed-state, stale, duplicate-submission and capacity/cutoff cases have meaningful results; successes/failures are attributable to each selected person; no hidden selections or duplicate decisions.

## Phase 5 — Event detail hierarchy and reports

Targets: management dossier summary, member event details, Participation & Attendance Report, shared event selector.

- Give title/status, event date/time, venue, deadline, capacity/remaining seats and relevant action clear prominence. Reduce duplicated metadata and separate descriptions from operational facts.
- Keep lifecycle/check-in actions contextual and consistent without giving every action equal emphasis.
- Align report summary cards and value baselines across final, non-final, cancelled and empty states. Match breakdown-panel spacing and responsive behavior without unnecessary fixed-height blank space.
- Keep event-wide summary metrics distinct from participant-table filtering; label their scope.
- Distinguish same-title events in selectors with date and venue where available. Keep selected-event context visible.

Completion: users can identify when/where an event happens and its availability without hunting; report states remain aligned and semantically accurate; recurring events are distinguishable.

## Phase 6 — Login and remaining member journeys

Targets: login, membership request/submission pages, Campus Events feed, My Events tabs, My Passes, Profile and certificate viewer.

- Refine login hierarchy, fields, password-control behavior where appropriate, validation and Sign in prominence. Place Request membership as a separate secondary action with adequate text-to-button spacing.
- Match membership request form typography, fields, helpers, errors and actions to the same system; give submitted state a clear next step.
- Align browse-event cards and View details actions; make schedule/venue/signup status scannable. Apply appropriate search/filter/paging behavior.
- Standardize registrations, attendance and certificate history rows/details, statuses, PDF actions, pass layout and profile/form spacing. Member data remains owner-scoped.
- Preserve originating ledger page/search/filter/event context when returning from PDF viewer.
- Replace generic developer-oriented error screen with actionable user recovery and a reference ID.

Completion: login-to-membership, event browsing/registration, pass access, history and certificate return journeys have consistent controls and no avoidable navigation dead ends.

## Phase 7 — Integrated Impeccable critique, polish and verification

- Run Impeccable critique and polish for changed surfaces, with an integrated final pass. Record issues and fixes, rather than treating a clean detector as proof of UX quality.
- Verify rendered desktop/laptop/phone layouts, short windows and 200% zoom, keyboard operation, dialog focus/restore, meaningful touch targets and contrast.
- Exercise long names/titles/reasons, zero/one/many records, filtered results, first/last pages, validation failures, unavailable/stale records, failed requests and repeat submissions.
- Verify Auto-fit does not churn requests, move selected people unexpectedly or obscure pagination. Never enforce a viewport cap by clipping content or disabling necessary scrolling.
- Build solution and CSS; run targeted tests for changed paging/search/filter contracts and approval behavior. Do not manufacture tests that mirror CSS. Broaden testing only for meaningful integration risk or failures.
- Keep DESIGN.md and PRODUCT.md aligned with shipped behavior. Provide changed-page list, validation evidence and remaining limitations.

Completion: shared patterns match across affected pages, behavioral tests pass, rendered checks support the result, and remaining limitations are explicit. Current live-browser audit is incomplete because Computer Use could not verify the Windows browser URL; resolve browser inspection before making visual-verification claims.

## Working boundaries

### Phase 1 delivery evidence

Shared 24px management gutter, aligned breadcrumb, bordered row controls, readable labels/table
headers, consistent editor field spacing and a reusable unsaved-dialog guard implemented.
Existing standalone form-panel width caps retained. Public/member centering retained.
Repeated visible timezone notes removed without changing conversion.

Validation: Release solution build succeeded with 0 warnings/0 errors after restoring current
dependencies; Tailwind CSS regenerated; all 9 JS tests passed; detector returned no findings.
Dual-agent critique and polish corrected coarse-pointer row-control specificity. Browser
component-fixture checks verified controls, modified-input warning, Keep editing, Discard changes
and focus return. Full authenticated-page/mobile visual verification remains for Phase 7.

Phase 1 convention review: shared unsaved-input protection belongs in the existing `site.js`
dialog infrastructure, rather than a separately loaded global script. Record/certificate feature
scripts reuse its small `AttendTaDialogs` API. Each opener establishes its initial field state
explicitly; AJAX editors reset that state only after an initial load, not after validation errors.
Existing Razor partials, native dialogs, progressive fallback routes and server-side business rules
remain the implementation pattern. Phase 2 list behavior will follow the existing feature-script
loading convention, and page-specific filters will be explicit rather than a growing route switch.

Correction validation: isolated Phase 1 Release build passed with 0 warnings/0 errors. All 13 JS
tests passed, including Escape/discard, keeping edited text, baseline cleanup and a server-rendered
failed dossier reason remaining protected. Shared/feature JS syntax checks passed; the detector
reported no findings. Independent review confirmed the retry-reason correction. Phase 2 working
changes were excluded from this validation and remain uncommitted.

### Phase 2 delivery evidence — 2026-11-15

Compact management tables share a typed search/filter toolbar, Show details dialog and one
pagination footer. Identity and decision facts remain visible; secondary contact information,
full reasons and exact timestamps move to details. Certificate Eligibility omits the redundant
unissued-certificate column; Issued Registry keeps certificate information. Registration reasons
appear only after choosing the relevant decision. Shared display timestamps retain Manila
conversion without repeating the timezone suffix.

Server-side queries filter before counting and paging. The default is five records; the shared
feature script measures table rows or card rows to adapt the size to available window space and
preserves query context across paging and native decisions. Narrow tables stack labelled fields
with natural page scrolling. Participation History propagates the actual requested size into
its footer; a regression test covers this contract.

Validation: Release solution build passed with 0 warnings/0 errors; all 329 service tests and
19 JavaScript tests passed; CSS regenerated; diff whitespace check passed; Impeccable detector
reported no findings. Independent critique identified toolbar action/query handling and missing
card Auto-fit support; both were corrected. Rendered Razor snapshots with synthetic records
were inspected at 1366×768, 1024×600 and 390×844 for representative tables and dialogs. The
checked tables had no horizontal overflow or internal vertical scroll region. Mobile certificate
column-width drift found during polish was corrected.

Integration limitation: the expanded affected suite reached 108/111 passing before the final
corrections. Follow-up checks passed activity-log and member-attendance assertions; the remaining
certificate browser test required adjustments to its TestServer redirect relay. Its final retry
could not initialize SQL Server LocalDB (instance startup failure), so complete certificate
issuance verification remains outstanding. Do not treat Phase 2 as a fully green integration
run. The integrated responsive/long-content/accessibility pass remains Phase 7.

### Approved Phase 2 review refinements

- Keep Course and Year on Members and Membership Requests. Course choices are BSCS/BSIT,
  shared with signup/member editing and enforced at the service boundary. Match existing long
  program labels in the two directory filters without rewriting stored registration history.
- Keep Search, Status and an existing-data Event type dropdown on Event Management; remove
  date fields. Event selection on Registration Review names a specific event, not its type.
- Campus Events keeps Search and Active/Completed tabs, defaulting to Active. Active means
  published and not completed, including upcoming events; a separate Upcoming tab would overlap.
- Use a five-row dossier minimum, name the selected roster status in its count, and
  preserve status/search across pages. Avoid resize/refetch churn from small viewport changes.
- Put Show details inside one three-dot menu per row. Keep frequent actions visible; align
  compact certificate decisions and return keyboard focus to the menu trigger after details close.
- Issued Registry keeps Event/Search; Accounts keeps Search/Role/Status. Activity Log loses its
  redundant caption and keeps Search/Action only. Reports keeps Event/Search/Attendance; My Events
  keeps Search within Registrations/Attendance/Certificates. Removed controls do not keep filtering
  through old URLs or hidden fields.
- Align page-title tops and heading-row heights with and without Create buttons, using shared CSS.

Refinement validation: Release build passed with 0 warnings/0 errors; all 329 service tests and
22 JavaScript tests passed. The 75 affected integration cases passed across the main run and a
focused rerun after correcting the empty roster's selected-status label. These include canonical
course validation, legacy course filtering, Active/Completed feed separation, type-ID filtering,
fixed dossier sizing, transaction/audit preservation and individual certificate issuance/PDF access.
The earlier certificate verification limitation is resolved. Tests connect through the verified
local LocalDB pipe; the fixture still accepts only local servers and creates/drops generated
AttendTa_IT_<guid> databases. Additional tests reject remote and malformed pipe targets.

Independent source review found no actionable regression in menu composition, focus return,
course choices or filter scope; the detector and JS syntax checks passed. Existing browser-flow
tests produced desktop/phone certificate screenshots, inspected during polish. The nine refreshed
live routes returned HTTP 200, including signup. Full integrated visual coverage remains Phase 7.

Phase 3 has not started. Stop after this phase's refinement commit for user review.

Final Phase 2 polish: row menus follow primary actions in DOM and visual order across requests,
registration review, dossier, certificates and reports. The shared Search label aligns toolbars
with and without dropdowns; live status follows tabs and the certificate table loses an extra
margin. Completed event badges use muted blue (6.40:1 text contrast) and cancelled events use
red (5.65:1), retaining visible labels. These are shared rules rather than page-specific offsets.
Validation: Release build passed without warnings; 329 service and 22 JS checks passed. The compact
list and certificate browser-flow integration tests passed, including DOM action ordering and shared
Search labels. Desktop/phone certificate captures were regenerated; desktop alignment was inspected.
Dual source review and the detector found no actionable issue. The port 5099 preview was restarted.

### Phase 2 account modal and viewport refinement

Accounts now opens Change role in the existing record editor dialog. The fallback page and
modal share fields and the existing audited command. Inline validation retains role/reason;
loading, retry, focus return and unsaved-change protection use the incumbent shared behavior.
Fields lock while saving so edits cannot be lost during a pending request.

Search stays Search. One Reset clears search and declared list filters while keeping event
and history context. Auto-fit measures available space and actual row/card height, expanding
beyond five to at most 25 records on taller screens. The dossier retains a five-row minimum.
Old fitted URLs recalculate once; paging remains stable until filters or viewport change.

Validation: clean Release build; 329 service, 23 JavaScript and 29 affected integration tests
passed. Desktop/phone browser tests covered role modal focus, Escape, overflow, pending-save
locking and failed-save input retention; captures were inspected. Reset URLs and expanded
dossier rows were checked through real Razor responses. Independent Impeccable assessments
identified the pending-save issue, now fixed; the detector returned no findings. The updated
port 5099 preview returned HTTP 200 for Accounts and the modal partial. Phase 3 remains pending.

Preserve the current visual identity and existing authorization, audit, date conversion, capacity and state rules. Add targeted backend support for agreed query/paging/approval capabilities. Do not introduce bulk declines, bulk issuance, a scheduled registration-opening feature, database resets or unrelated business-rule changes as part of UI refinement.

Course flow verification: signup, member editing and filters use BSCS/BSIT as both option
labels and submitted values. New requests store those codes; approval creates a member with
the same code. Legacy request values "BS Computer Science" and "BS Information Technology"
remain readable/filterable and normalize to BSCS/BSIT when creating the member. Original
requests and historical registration snapshots are preserved. No application change was needed.
Expanded regression checks cover both signup choices, both legacy approvals, positive/negative
course filtering on Requests and Members, and member management. All 34 affected integration
tests passed with a clean Release build.

### Deferred profile validation — placeholders only for now

Per the user's latest scope, only frontend examples change: student number `23807613`, mobile
`09171234567` without dashes, and email `name@gmail.com`. Signup and member editing share the
same examples; login uses the same generic email example. These are hints, not new restrictions.
No backend validation, schema, existing data, dropdown options or HTML input limits changed.
Follow-up display cleanup removes "Year" from numeric option/table values, retaining the field label.
Venue Capacity headings now align with their right-aligned numeric values in a compact column;
Actions headings use the shared right alignment and compact action width in every table state.

Pending backend work, not implemented: enforce year levels 1–4, exactly eight ASCII digits for
student numbers, and exactly eleven ASCII digits for mobile numbers when supplied. Year choices
are still 1–5 and backend year validation still accepts any positive integer. BSCS/BSIT course
handling already exists. Email already accepts valid ordinary addresses, without a domain restriction.
Apply aligned form/filter choices and validation together when that follow-up is authorized.

**Implemented 2026-11-15 (issue #238):** the paragraph above records the state before the
follow-up. Year levels 1–4, eight-digit student numbers, A–C sections, eleven-digit mobile numbers
starting with `09`, and the password rule now comes from `ProfileOptions` and is enforced on both
forms and both services. Before deploying, run `database/inventory-legacy-profile-values.sql` to
list the stored rows that predate the rules.

### Phase 2 column balance and redundant details refinement

Venues now orders Room Name, Location, Capacity, Status and Actions; compact numeric/status/action
widths leave text columns the available space. Its details retain photo, contact and availability
notes rather than repeating Location/Capacity. Members adds Email to the main table and uses compact
Status/Actions widths instead of reserving 30% for Actions; details retain identity context, mobile
and membership type. Event classifications no longer opens a redundant details modal: Read more/less
expands the description inline only when actual rendered text exceeds two lines. Full text remains
available without JavaScript, and resizing preserves an expanded description. Certificate details
omit absent reason/issuance fields rather than displaying irrelevant "Not issued" placeholders.

Validation: clean Release build and CSS generation; 24 JavaScript checks and the compact-list and
certificate browser-flow integration tests passed. Rendered layouts at 1440, 1024 and 390px were
captured; representative desktop/laptop/phone images were inspected. Checks cover no horizontal
overflow, visible member email, numeric capacity/header alignment, conditional disclosure, keyboard
expansion and preservation on resize. Existing certificate issuance/PDF checks still pass.
Independent source review found no defect; the detector returned no findings. No backend validation,
database schema or business rules changed. Phase 3 has not started.

Phase 2 closeout review: checked the shared toolbar/Reset, declared filters, compact columns,
details menus, context-preserving paging, adaptive row sizing and exact footer metadata against
the phase checklist. Members now exposes Section in its existing academic column. Reports'
remaining "Year" prefix was removed to match number-only year values elsewhere. No additional
Phase 2 feature gap was identified in this source review. Full app-wide responsive/zoom/keyboard
sign-off remains Phase 7; this review is not a claim that every live viewport has been inspected.
Draft/publish/cover improvements, bulk approvals, report card/detail hierarchy and the remaining
login/member journeys retain their assigned later phases. Deferred profile validation remains pending.

No further user design decisions are presently required. Surface genuinely new business-rule conflicts with a concrete example if implementation reveals one.

### Certificate availability repair and PDF modal follow-up

User-approved follow-up: replace misleading View PDF links for missing/corrupt files with Document
unavailable, retaining Issued and its metadata. A shared service helper checks the private certificate
key and readable PDF signature; direct reads keep ownership/authorization and now reject corrupt
PDF bytes. Management/member lists use those facts. This is backend read-side support; issuance,
decisions, audit rules, schema and profile validation are unchanged.

View PDF now opens a shared read-only modal with the actual retained PDF, Close, Open in new tab
and Download. The standalone viewer remains as a fallback. Loading can be dismissed; retry handles
document/module failures, and closing cancels fetch/render work, releases blob/worker resources,
restores focus and leaves list filters intact. PDF.js 6.5.438 is pinned and self-hosted under
wwwroot/lib/pdfjs with its license/provenance; it loads only on demand and needs no CDN/plugin.
Official usage reference: https://mozilla.github.io/pdf.js/examples/ . Visible canvas rendering
and extracted accessible PDF text replace unreliable native PDF iframe rendering.

Local data investigation found eight Issued rows: two retained PDFs, five missing referenced files
and one missing path. No matching missing originals were found in the project local folders. These
six remain unavailable until originals are restored; no local certificate records were altered,
reset, reissued or regenerated. The UI repair does not claim to reconstruct missing documents.

Validation: clean Release build and CSS generation; 329 service and 24 JavaScript checks passed.
The 16 affected certificate/compact-list integration tests passed. Follow-up browser checks cover
visible PDF pixels and accessible text, actual download bytes, desktop/phone layouts, filter/focus
preservation, dismissal during a stalled fetch, module/document retry, unavailable/restored/corrupt
files, unchanged issuance/audit and owner-only access. Independent review caught and resolved
busy-loading dismissal and cached failed-import recovery; the detector and syntax checks passed.
Phase 3 remains unstarted; remaining cross-app sign-off stays in Phase 7.

Accounts no longer offers a separate Preview deactivation menu item. Deactivate opens the existing
confirmation with affected registrations, required reason and Cancel/Confirm; authorization,
audit and last-administrator protection are unchanged. This is a menu cleanup, not a backend rule change.
Validation: clean Release build and all 12 affected preview/deactivation integration tests passed;
independent source review and detector found no issue. Preview restarted on port 5099.
