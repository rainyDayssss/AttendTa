# AttendTa important finale test cases

Decision: 2026-11-06 — minimize new test authoring during prototyping; retain focused guard
checks and all existing CI. Add/reuse important automation in 8.5 (#78), execute it together
with existing suites in 8.4 (#77), and cover the remaining SPEC scenarios manually in 8.6 (#79).
This is a **test plan, not execution evidence**. Every case starts as **not executed**.

## Preconditions and evidence

- Test the exact mock-free integrated commit after 8.11/8.12/8.10. Realistic, disposable data
  includes distinct administrator, officer and two members, active/inactive account and
  membership states, and draft/published/completed/cancelled events.
- Use the existing two xUnit projects. SQL tests connect through `ATTENDTA_TEST_SQL` to a local
  master connection and create their own `AttendTa_IT_<guid>` databases. Never reset or run
  destructive fixtures against `AttendTa_Demo`, shared/production databases or `AsiBasecodeDB`.
- Install Chromium and Poppler for PDF checks; absent tools are a blocked environment, not
  proof of a product pass. Serve assets locally and use the real phone/HTTPS origin for #75.
- Record case id, requirement, existing/new automated test or manual steps, tested commit,
  environment, result, evidence and linked defect. Do not log passwords, hashes, QR tokens,
  private paths or live credentials in evidence.
- Reuse existing passing tests before adding coverage. Tests for races require parallel real
  SQL connections; a pure mock test cannot establish the concurrency outcome.

## Important automated and integration cases

| Case | Requirements / source | Preconditions and action | Expected outcome |
|---|---|---|---|
| AUTH-01 | R1–R3, R35; existing auth/request suites | Submit a request, approve/reject it, log in by email; try invalid credentials and duplicate/concurrent decisions. | Hash-only credentials; one approval outcome; generic sign-in failure; no privileged public account creation; correct nonpersistent cookie. |
| AUTH-02 | R3, personal-resource guards, AD-8 | Request protected actions anonymously/as wrong role; request another member's data; submit a mutation without antiforgery; deactivate/change role then reuse the session. | Appropriate generic refusal, no unintended writes/data disclosure, stale session rejected. Reuse existing cookie/policy tests. |
| MD-01 | R4–R8, R33; existing Master Data suites | Edit a member/correct student number; toggle type/venue; replace a photo with valid, invalid and oversized content and force a write failure. | Existing uniqueness/reason/history rules preserved, active references guarded, at most 2 MiB JPEG/PNG by content, previous file survives failed replacement. |
| REG-01 | R9–R19; existing allocation/cutoff tests | Concurrent final-seat approvals/promotion; signup at deadline and decisions at start; cancel and try registering again. | No overbooking; fair explicit promotion; exact server cutoffs; cancelled token invalid; no automatic promotion or re-registration. Reuse #133 boundary coverage. |
| ATT-01 | R21–R24, R33 | Confirm a member, open check-in and submit their token twice, including parallel requests. | Exactly one attendance row and audit entry; repeats return original timestamp; display name/student number only after valid authorized check-in. |
| ATT-02 | R22–R24 | Try unknown/wrong-event/cancelled/unconfirmed/inactive/invalidated tokens and closed or unpublished check-in. | Refusal inserts nothing, leaks no other records and never reports false success. Searching/viewing/downloading alone records nothing. |
| EVT-01 | R13, R33 | Complete before start, then at/after start; force a completion-hook failure. | Before-start refusal; valid completion closes check-in and atomically creates reviews for valid attendees; hook failure rolls back event/reviews/audit. |
| ADM-01 | R31–R32, R33 | Concurrently remove access from the last two admins; deactivate account/member with upcoming, started and attended registrations; reactivate. | One active admin remains; preview/reason and correct cascade; tokens cleared and no promotion; account session ends but inactive membership can still log in; reactivation restores no old seats. |
| CERT-01 | R25, R27–R28, R33 | Approve/decline review, issue twice and concurrently; race issuance against attendance invalidation. | Required reason/final decline; one issued certificate; safe single race outcome; invalidation blocked after issue, otherwise review not eligible and issuance refused. |
| CERT-02 | R28–R29, NFR-4 | Force renderer/file/commit failure; retry; later edit member/event; download as owner, management and non-owner; request missing file/path. | No false issued row on failure; failed new file cleaned safely; retained bytes/snapshots unchanged; only authorized id-based PDF streaming; no caller path or overwrite. |
| REP-01 | R30, R36, canonical facts | Compare report to >25 registrations with mixed statuses, valid/invalidated attendance, edited profiles and a zero-confirmed event. | Full-dataset counts independent of page; N/A zero denominator; completed-only no-shows and final rate per authoritative SPEC; attendee distributions use signup snapshots; inactive history retained. |
| AUD-01 | R33–R34, AD-3/7 | Force a business transaction failure; inspect audit; try update/delete via the actual runtime SQL identity. | Business/audit atomicity; no secrets/tokens/private paths; append-only table and column permissions; viewer administrator-only and newest-first. |
| MOCK-01 | Demo rescope 2026-11-06; 1.14, 8.13, 8.11; `demo-mock-register.md` | Check every register row; start the published build with `DemoData__Enabled=true`, no `DemoData__TokenKey` and a database other than `AttendTa_Demo`; search the source and published output for every register id, the demo data folder and the `DemoData` keys; exercise each required workflow against actual persisted data. | Every register row is `removed` with its commit; the app starts normally (the 1.14 guard would refuse this start while the seam exists); the search finds nothing; real failures cannot fall back to demo data. |

## Required manual/device cases

| Case | Steps | Expected outcome / evidence |
|---|---|---|
| UI-01 | Follow public request/login, member events/details/passes/history/profile and officer/admin management navigation against real data. | Every required route is reachable in the correct role; no dead links, prototype-only role switching or fake official outcomes. Use final 8.12 walkthrough evidence. |
| UI-02 | Use keyboard for required forms, status tabs, centered editor and reason dialogs; trigger one validation error, cancel and reopen; check 200% zoom and representative 1440/1024/768/390px layouts. | Visible focus, contained/restored dialog focus, retained input, reachable actions, labelled errors and no horizontal page overflow. Dense tables may scroll within their region. |
| ATT-03 | On the actual hotspot trusted-HTTPS URL in phone Chrome, grant/deny camera, scan a valid pass and duplicate, then search/select/check in manually. | Camera works without certificate warnings; camera denial has usable fallback; clear persistent results; no internet/CDN dependency; lookup alone never inserts. #75 evidence required. |
| CERT-03 | Issue and view/download a real retained landscape A4 PDF; inspect locally available fonts, University of Cebu branding, configured signatory, recipient/event/date/number and readable layout. | Correct approved identity and snapshots, no sample/development artefact presented as official output, no navigation in the PDF, stable retained download. |
| OPS-01 | Run actual demo-host readiness script with the runtime identity, including server-level privilege and audit column-write checks; verify private/log directory access and offline Chromium/login. | Every host readiness row verified or an explicit blocker. Closed #74 is not substituted for the new 8.14 evidence. |
| OPS-02 | Back up disposable demo SQL plus SetupRoot together; restart/redeploy with storage intact; restore to a separate disposable target and compare rows/checksums/keys. | Images/certificates/keys persist; restored bundle is coherent. No shared/production database is reset or overwritten. #76 evidence required. |

## Full goal coverage without unnecessary new automation

The rows above prioritize risk; they are **not a replacement for SPEC §20**. In 8.5/8.6,
map every authoritative acceptance scenario to an existing automated case, a necessary new
automated case, or a manual case with concrete steps. Missing mappings block sign-off.

| Functional IDs | Minimum demonstration / evidence |
|---|---|
| AUTH-01–03 | Request, reviewed approval/rejection, real email sign-in/out and active/role checks. |
| MD-01–05 | Own read-only profile, permitted member correction, types, venues and safe required photos. |
| EVT-01–04 | Draft/create/edit, published member visibility/deadline behavior, permitted lifecycle actions and completion/cancellation. |
| REG-01–05 | Pending signup/snapshots, review/seat allocation, cancellation, explicit waitlist promotion and exact cutoffs. |
| QR-01 | Real owner-authorized token-only QR PNG; display/download does not record attendance. |
| ATT-01–03 | Window control, camera/manual attendance, invalidation and owner-scoped history. |
| CERT-01–02 | Valid-attendee reviews, decisions, one-at-a-time retained issuance and authorized downloads. |
| REP-01 | Correct selected-event totals, detail paging, no-shows/rate and snapshot-based distributions. |
| ADM-01–02 | Roles/access/membership cascades, last-admin safety and append-only attributable activity history. |
| FR-35–36 | Supplied initial-admin setup with no default credentials; deterministic authorized 25-row paging/filter/empty behavior. |

## One finale execution programme

1. **8.5 / #78:** inventory existing coverage, add missing important automation, write the full
   automated/manual scenario mapping and concrete manual remainder. Targeted new tests pass.
2. **8.4 / #77:** run actual CI-equivalent build, `css:build`, `test:js`, service and SQL/PDF
   suites on one integrated commit. Keep the manifest; do not invent a lint/typecheck script.
3. **8.2/8.3 / #75/#76:** attach actual phone/HTTPS and storage/restore evidence.
4. **8.6 / #79:** execute the remaining manual cases, linking automated/UI/device results.
5. **8.7 / #80:** demonstrate the continuous request-to-certificate/report journey and all
   26 functional IDs plus FR-35/FR-36, reusing completed steps rather than repeating everything.
6. **8.8 / #81:** human Tier 3 decision, affected retests and exact-commit sign-off.
7. **8.9 / #82:** release PR only after sign-off and green CI/peer approval. A code change
   after evidence invalidates the affected cases and requires a new tested-commit reference.

Release blockers: remaining required mocks, unauthorized writes/downloads, overbooking,
duplicate attendance/certificates, lost or mutable retained PDFs, unsafe last-admin/cascade,
wrong material report totals, failed important tests, unmapped SPEC scenarios or unverified
phone/storage/host requirements. Planning, a merged tool or an environment-only failure is
not a substitute for execution evidence.
