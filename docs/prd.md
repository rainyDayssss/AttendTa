# AttendTa product requirements

Version 1.2 | 4 November 2026 | Consolidated working baseline

This document consolidates the supplied requirements and explicit project-owner decisions. It is not a claim of adviser approval, completed implementation, or successful acceptance testing. Technical implementation belongs in [architecture.md](architecture.md); visual guidance belongs in [design.md](design.md).

## 1. Purpose and sources

AttendTa supports one student organization's membership approval, event participation, attendance, certificates, and evaluation. The goal is a working local/intranet demonstration with reliable records and clear member, officer, and administrator workflows.

Primary sources:

- [Final WBS](WBS_J4S_ATTENDTA_20261103.pdf): feature identifiers and delivery ownership.
- [Final HLD](Alliance_Jumpstart_HLD_Group_7_AttendTa.pdf): 20 core screens and their visible states.
- [System specification v2.1](AttendTa-System-Specification.md): detailed business rules, exclusions, integrity, and acceptance coverage.
- [UC function list](UC-Project-FL.pdf): original functional coverage; reduced-MVP refinements are not claims about the original UC wording.
- [ERD](AttendTa-ERD.md) and [DBML](AttendTa-MVP.dbml): intended data structure.
- [Sequence diagrams](AttendTa-Sequence-Diagrams.md): workflow illustrations, read with the business rules.
- [Project plan](Parent_Project_Plan_J4S_AttendTa_v2.00.pdf) and [Git/QA guidelines](git_qa_guidelines.pdf): environment and delivery practices.

Explicit project-owner decisions in this conversation take precedence over conflicting older text. For unresolved source conflicts, record the conflict; do not silently add scope. The HTML prototype at `F:/AttendTa` is a visual reference, not a required file structure, persistence model, authentication implementation, or business-rule implementation. Its documentation is not used as a requirements source.

## 2. Decisions and conflict resolutions

| Topic | Working baseline |
|---|---|
| Frontend | ASP.NET Core MVC, Razor views, Tailwind CSS. Adapt prototype visuals to the basecode. |
| Authentication | Registered email/password only; no institutional-domain restriction or student-number login. Password hashing uses Argon2. Login throttling is excluded from the MVP. |
| Identity | AttendTa logo is final. Institution wording is **University of Cebu**. Default avatars/initials only; no profile-photo upload. |
| Certificates | One fixed HTML/CSS template rendered to a retained PDF, one issuance at a time. The certificate includes the student's full name, event title and start, issue date, unique number, and issuer. Authorized signatory details remain pending. |
| Reports | Prioritize the specified per-event results. Prototype dashboard metrics do not introduce additional required calculations. |
| Database | Follow the ERD's physical lookup-table/FK design. Current string-based SQL is an implementation gap, not a changed requirement. |
| Environment | Local development/demo, with phone-camera QR scanning over a hotspot and Chrome as the target browser. No production/cloud deployment commitment. |
| Navigation | Use the HLD/design's Events, My Passes, History, scanner, and certificate presentation. “My Events” in earlier text describes the personal journey, not a mandate for one physical page. |
| Ownership | Gabutan owns Authentication only; Taguiam owns Master Data; Singco owns Events and Registration; Talisic owns Attendance and QR; Pinca owns Certificates, Reports and Administration, including initial-admin setup. Shared infrastructure handoffs require coordination, not silent reassignment. |
| Scheduling | Delivery dates and effort estimates are not fixed by this PRD. |

The WBS avatar label does not authorize uploads. HLD sample branding, printing/bulk buttons, and New User actions do not expand the reduced scope. Conflicting specification ownership paragraphs and old schedule references are not copied into this baseline.

## 3. Users and access

| Actor | Allowed responsibilities |
|---|---|
| Applicant | Request an account; cannot log in while pending; can submit a new request after rejection. |
| Member | View own profile/history; browse events; request/cancel participation within rules; access own QR and issued certificates. |
| Officer | Review accounts and registrations; maintain members/masters/events; promote waitlist; record/correct attendance; review/issue certificates; view reports. |
| Administrator | Officer capabilities plus fixed role assignment, account access management, and activity-history viewing. |

Officers use the same account and member profile for participation. Administrators need a member profile only when participating. Management privileges do not bypass participation eligibility, capacity, cutoffs, or queue priority. The reduced specification permits own-participation reviews/promotions and own-certificate decisions under the same rules and audit; no new self-approval restriction is added.

Account inactivity blocks protected access, including an existing session. Membership inactivity blocks new participation/check-in but does not itself prevent login or viewing retained history/certificates. Protect the last active administrator from demotion/deactivation. No custom permission editor is included.

## 4. User journeys

1. Applicant submits identity, academic details, and password; officer approves; linked account/member becomes active; applicant logs in after offline notification.
2. Officer creates venue with photo and event draft; publishes with valid cover/deadline/capacity; member submits a pending registration; officer confirms or waitlists it.
3. Confirmed participant presents an event-specific QR; officer opens check-in and scans or explicitly checks in through manual lookup; attendance is retained.
4. Officer explicitly completes the event, approves eligible attendance, and issues the participant's certificate. The owner can view/download the retained PDF.
5. Officers inspect per-event reports; administrators inspect audit history. Cancellation, explicit waitlist promotion, deactivation, and attendance correction are separate guarded workflows.

## 5. Functional requirements and acceptance

The identifiers below preserve final WBS traceability. Related operations include authorization, validation, audit where required, and meaningful failure feedback.

| ID | Required behavior and acceptance |
|---|---|
| AUTH-01 | Collect student number, full name, email, course, positive year level, section, optional mobile, password/confirmation. Create only a pending request. Reject existing identities, including inactive ones, and duplicate pending email/student number. Hash with Argon2; never retain confirmation. Rejection allows a separate new request. |
| AUTH-02 | Review pending requests only. Approval atomically creates an active member-role user and linked profile, records reviewer/time/resulting user, clears the request hash, and audits. Rejection requires reason and creates no account; clears hash. Repeated/concurrent decisions cannot create duplicates. |
| AUTH-03 | Verify registered email/password, reject inactive accounts and pending applicants, issue expiring authentication, and clear the browser cookie on logout. Protected requests respect current account status/role and ownership. |
| MD-01 | Member sees own read-only profile. Officers handle corrections; no member self-edit/password-change/photo-upload flow. |
| MD-02 | Maintain approved member details, academic/contact data, membership type/status, and current full name. Student-number correction requires uniqueness, reason, and old/new audit. Email stays read-only. No standalone Add Member; preserve signup and issued-certificate snapshots. |
| MD-03 | Create/edit unique nonblank event types; activate/deactivate while preserving references and uniqueness across inactive records. |
| MD-04 | Maintain venue details, positive capacity, active state, and required photo. Reject capacity reductions below limits of linked draft/published events. Availability notes do not implement booking conflict detection. |
| MD-05 | Officer/admin uploads one current JPEG/PNG per venue/event, validated content, maximum 2 MiB. Venue photo required at creation; event cover optional in draft and required at publication. Required images permit replacement, not removal without replacement. |
| EVT-01 | Create drafts with title, description, active type/venue, start, positive limit within venue capacity, deadline no later than start, and optional draft cover. Publish only with valid cover, masters, capacity, and a future deadline. Publishing immediately opens signup and member visibility. |
| EVT-02 | Show signup open before deadline and closed at/after deadline, with labelled local time. Reject stale submissions server-side. Closing signup does not change event status. |
| EVT-03 | Validate all draft edits. Published events permit title, description, and cover replacement only; lock operational fields. Completed/cancelled records are read-only. |
| EVT-04 | Cancel eligible draft/published events with reason only when no valid attendance or issued certificate exists. Cancel active registrations, clear tokens, close check-in, preserve history/reasons, audit atomically, and never promote. Explicitly complete a concluded published event at/after start; close check-in. |
| REG-01 | Active eligible member submits one pending registration per event before deadline, with immutable course/year/section signup snapshots. Full events may accept requests with waitlist warning. Members may register for events whose times overlap; no schedule-conflict check is performed. Cancelled rows still prevent reapplication. |
| REG-02 | Officer approves/declines pending requests before event start, even after signup deadline. Confirm only with capacity and no earlier eligible waitlist priority; otherwise waitlist. Decline becomes cancelled with reason. Record review and confirmation actors/times; token only on confirmation. |
| REG-03 | Owner may cancel pending/confirmed/waitlisted participation while event published, before start, and without valid attendance. Warn cancellation is final; record reason/actor/time, clear token, release seat, audit. No automatic promotion. |
| REG-04 | Explicit officer action promotes earliest currently eligible waitlisted registration ordered by review time then registration ID. Recheck full queue and free capacity; preserve original review and record confirmation/token/audit. No eligible candidate/seat means no mutation. |
| REG-05 | Exact deadline blocks signup; exact start blocks reviews, confirmations, promotion, and member cancellation. Retain unresolved pending/waitlisted rows as non-actionable “Not confirmed before cutoff” history; exclude them from no-shows. No scheduler required. |
| QR-01 | Owner can view/download an on-demand PNG containing an unpredictable event-registration token. Saved copies require online officer validation. Unconfirmed/cancelled/ineligible participation has no usable credential. Display/download never records attendance. |
| ATT-01 | Officer manually opens/closes/reopens check-in for a published event, including early opening. QR and explicit manual name/student-number check-in validate event, confirmed registration, current eligibility, open gate, and duplicates. A valid scan result shows the participant's name and student number for identity checking. Record actor/time/method. Repeated valid scan shows original time. |
| ATT-02 | Invalidate mistaken attendance with confirmation/reason, preserving original row and actor/time history. Exclude it from reports; affected unissued certificate becomes not eligible. Issued certificate blocks ordinary correction. Warn that re-check-in after invalidation is unavailable. |
| ATT-03 | Show only authorized personal attendance/history, clearly identifying corrected records. Members cannot record/correct attendance. |
| CERT-01 | Create pending reviews only for valid attendees of completed events. Approve eligible or decline not eligible with reviewer/time and decline reason. Declined decisions cannot reopen; issued decisions cannot change. No-show alone creates no certificate record. |
| CERT-02 | Issue one eligible participant's fixed-template landscape A4 PDF at a time. Retain unique number, recipient/event snapshots, issue date and issuer. Recheck eligibility and prevent duplicate issuance/correction races. File failure cannot produce issued status. Authorized owner/management can view/download the saved PDF; later edits do not regenerate it. |
| REP-01 | Show per-event registration statuses, confirmed/attendee counts, attendance rate, completed-event no-shows, and actual-attendee course/year distributions. The participant detail list shows name, student number, course/program, registration status, attendance result, and check-in time where present. Use signup snapshots and the full applicable dataset; paginate detail rows only. Retain historical results despite current inactivity. |
| ADM-01 | Admin assigns fixed roles and changes account access with reasons/audit. Protect last active admin, invalidate stale access, apply future-registration rules, and preserve history. Reactivation never restores cancelled seats. |
| ADM-02 | Admin views a read-only chronological activity list with actor, action, affected record, time, and reason/description. Preserve relevant old/new values without credential exposure; no advanced filter builder or comparison editor. |

## 6. Cross-feature rules

### Lifecycle and retention

- Event states: draft, published, completed, cancelled. Time passing does not complete an event. Cancelled drafts remain hidden from members; previously published cancelled events remain identifiable to affected members.
- Registration states: pending, confirmed, waitlisted, cancelled. Attendance does not introduce an `attended` registration status. Only confirmed registrations occupy seats.
- Attendance is one retained row per registration; invalidation does not delete it or permit re-check-in.
- Certificate states: pending approval, eligible, not eligible, issued. Ordinary issuance/decline cannot reverse. No attendance-duration condition is added.
- For account or membership deactivation before an event starts, cancel pending/waitlisted participation and confirmed participation without valid attendance. Retain attended and started/completed history. Release seats without promotion. Account and membership states remain distinct.
- Before applying account deactivation or membership inactivity, show the effect on upcoming registrations and require a reason. Recheck the impact on submission; a preview does not reserve or freeze the displayed records. Record the change and affected cancellations in audit history.
- Preserve business rows, identities, references, and audit history. No general archive/restore or hard deletes.

### Reports

For a completed, non-cancelled event: attendee = confirmed registration with valid attendance; no-show = confirmed registration without valid attendance; rate = attendees / confirmed registrations × 100. Zero denominator displays **N/A**, not 0%. Pending/waitlisted/cancelled requests are not expected attendees. Cancelled events are presented separately. Distribution includes actual attendees only and uses signup course/year snapshots. Prototype global summary tiles are not additional approved requirements.

### Lists and feedback

Use fixed 25-row server-side pages, authorized counts, stable ordering with an ID tie-breaker, Previous/Next, preserved supported filters, and clear empty states. Normalize invalid pages to 1 and beyond-last pages to the last available page. Exact single-record lookups need no paging. Report totals and queue selection never depend on visible page rows. Every mutation revalidates current records.

Forms retain input on validation failure. Show errors, blocked actions, submitting states, and results clearly. Support keyboard interaction, visible focus, readable status text, responsive layouts, and contained scrolling for dense tables, following design.md. Do not claim accessibility certification from design guidance alone.

### Security and records

Enforce real server-side authentication/authorization, ownership, Argon2 password hashing, antiforgery for cookie-authenticated mutations, and safe uploads/downloads. Do not add login throttling or lockout in this MVP. Do not trust a browser-supplied actor or role. No passwords, hashes, QR tokens, or physical paths in logs. Required business changes and database audit entries commit together. Files persist outside the application/public web folders; store relative paths and compensate for file/database failures.

## 7. Excluded scope

No profile-photo uploads; password change/reset/recovery; outgoing email or verification; self-service email/profile editing; custom permissions; general archive/restore; automatic promotion or scheduled cutoff closeout; event reapplication; separate signup-opening command; operational event edits after publication; attendance duration or re-check-in; certificate bulk issuance, template editing, revocation/reissue or reopening declines; dedicated QR printing; report CSV/PDF exports; dedicated device/session management; public applicant tracking. Initial administrator provisioning is setup, not a general New User screen.

## 8. Demo acceptance and remaining content

Release acceptance requires all 26 functional IDs to work end to end in the integrated application and every minimum acceptance item in the System Specification §20 to have passing evidence. Demonstrate the full journey in §4 using real database records, not seeded browser behavior. Verify duplicate requests, inactive account versus inactive membership, last-admin protection, mandatory files, deadline/start boundaries, overlapping-event registrations, last-seat competition, QR identity display and duplicate scans, correction/issuance conflict, private downloads, snapshot retention, N/A rates, and unauthorized counts.

Hotspot/LAN access is an intended demo scenario. Other devices use the host's network address, not their own localhost. Camera availability must be tested on actual devices with suitable HTTPS; manual lookup is included as a fallback, not proof QR scanning works.

### Concrete acceptance examples

| Scenario | Required observable result |
|---|---|
| Pending applicant attempts login | Access denied; no member account exists until approval. Approval creates one linked account/profile even if submitted twice. |
| Active account has inactive membership | User can log in and view retained history/certificates but cannot newly participate or check in. |
| Last seat has two competing approvals | At most one additional confirmed seat; other request is handled according to capacity/queue rules, never overbooked. |
| Deadline passed but event has not started | New signup rejected; existing pending review/promotion and permitted owner cancellation remain available. |
| Event starts with pending/waitlisted rows | Rows remain historical and unconfirmed, not automatically cancelled and not no-shows. |
| Same confirmed QR scanned twice | One attendance row; repeat shows original time. Invalidated attendance cannot be checked in again. |
| Correction competes with certificate issuance | Either correction prevents issuance or successful issuance blocks correction; no issued certificate with a simultaneously invalidated attendance result. |
| PDF write or final database save fails | No false issued-success result; unused file is cleaned up/reconciled. Existing issued PDF is never overwritten on retry. |
| Member name/event title changes after issuance | Downloaded issued PDF remains unchanged. |
| Completed event has zero confirmed registrations | Rate displays N/A; changing detail pages does not change totals. |
| Member requests another member's PDF or history | No unauthorized content or counts disclosed. |
| Two admins attempt to remove the last active admin access | At least one active administrator remains. |

These examples supplement every feature's acceptance row; they do not reduce scope to only the happy-path demo. The HLD's 20 screen identities are mapped to MVC responsibilities in architecture §3.2, while layout detail remains in design.md. Folder structure is an implementation concern and is intentionally kept out of this PRD.

Working organization display timezone: Asia/Manila. Pending content/configuration: authorized certificate signatory and signature, and actual host/network/certificate setup. Applicant outcomes use offline officer communication for this demo; any production channel remains outside scope. No date or performance guarantee is inferred from old schedules. Completion requires working integrations and acceptance evidence, not these documents alone.
