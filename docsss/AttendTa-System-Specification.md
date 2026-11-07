# AttendTa — Agreed System Specification

Version: 2.1 — UC-aligned reduced MVP
Prepared: 2026-11-01; owner decisions updated 2026-11-04
Status: Revised documentation baseline; implementation and database enforcement require verification
Stated original submission deadline: October 7, 2026 (historical; not a new delivery promise)

## 1. Authority and scope

The requirements baseline is [UC-Project-FL.pdf](UC-Project-FL.pdf). This specification records the agreed refinements for the revised MVP. The UC's core functional coverage is retained; added design details must not be represented as explicit UC requirements.

This specification replaces the broader v1.3 workflows for the revised MVP; original source documents remain unchanged. Later explicit user decisions supersede earlier proposals. Read this specification with the revised [user stories](../AttendTa-User-Stories.md), [DBML](AttendTa-MVP.dbml), [sequence sources](AttendTa-Sequence-Diagrams.md), [offline visual diagrams](AttendTa-Sequence-Diagrams.html) and [WBS](WBS_J4S_ATTENDTA_20261103.pdf). The WBS is a delivery breakdown, not a separate requirements authority. Any inconsistency between revised artifacts must be resolved explicitly rather than silently importing an older workflow.

One student organization is supported. The system manages account approval, membership, event types, venues, event publication, registration review and waitlisting, QR/manual attendance, certificate eligibility and PDF issuance, participation reports, and administrative accountability.

### 1.1 Agreed refinements

- Email/password login and fixed member/officer/admin permissions.
- Password selected during account request; linked user/member created only on approval.
- Draft, published, completed and cancelled event states. The UC mentions publication/completion but does not explicitly prescribe these event statuses.
- Publishing opens signup immediately; no separate manual registration-opening action.
- Initial reviews and waitlist promotion are officer/admin actions. Promotion never runs automatically on seat release.
- Independent manual check-in controls, QR scanning, and manual name/student-number fallback.
- No user profile-picture uploads; mandatory venue photos at creation and event covers required before publication.
- Eligibility requires valid attendance, completed event and officer/admin approval. Downloadable certificates are retained PDFs using one fixed template.
- Persistent server-local file storage, not Supabase or browser `localStorage`.
- Retained history without general archive/restore or business-record hard deletes.
- ASP.NET Core MVC, SQL Server, EF Core database-first, ten core business tables and physical lookup tables for readable ERD enums.

### 1.2 Deferred scope

General archive/restore; automatic waitlist promotion; scheduled registration closeout or automatic mass cancellation; event reapplication after cancellation; separate manual registration opening; operational event edits after publication; self-service profile-detail editing; outgoing email/Brevo; email verification; forgotten-password recovery and reset-token tables; self-service email changes; dedicated session/device tables and individual token revocation; custom permission editors; CSV/PDF report exports; image galleries/cropping/editors; certificate template editors, bulk issuance, revocation or correction/reissue; in-system QR printing; attendance-duration tracking and re-check-in after invalidation.

Known-password changes (US-04), reopening declined certificate decisions, confirmed-participant course/year distribution, advanced activity-log filtering and a field-by-field comparison UI are also deferred. User profile-picture uploads are excluded. QR PNG downloads and certificate PDF downloads remain included. Email is required but ownership is not verified. A revised delivery date and effort estimates have not been agreed. US-04 remains reserved only in the user-story numbering; deferred tasks do not reserve numbered WBS slots.

### 1.3 UC traceability and consolidated screens

| UC module | Revised work |
|---|---|
| Authentication | Account request/approval, login/logout and fixed role-based access checks. |
| Master Data | Members, event types, venues and required venue photos. |
| Transactions | Events and required covers, registration/review/cancellation, explicit promotion, QR/manual attendance, eligibility and PDF issuance/downloads. |
| Reports | Participants, counts, attendance rate, no-shows and actual-attendee course/year distribution. |
| Administration | Role assignment, account deactivation/reactivation and basic activity-log viewing. |

These five UC categories remain traceability labels. The WBS uses six familiar functional headings for delivery: Authentication, Master Data, Events and Registration, Attendance and QR Codes, Certificates and Reports, Administration. Six headings do not imply six owners. Shared pagination, retention, validation, storage, setup, QA and deployment are cross-cutting delivery work, not an additional functional module. Estimate common components once plus their integrations.

**My Events** combines personal registration status, permitted cancellation, QR, attendance and issued-certificate download in one list/detail journey (US-15/17/20/23 and member side of US-25). **Event-attendee management** combines attendance information/correction, eligibility review and one-at-a-time issuance (US-21/22/24 and management side of US-25). Separate stories and sequence views express acceptance/workflows, not a commitment to separate screens.

### 1.4 Five-member vertical-slice ownership

| Owner | Package | End-to-end responsibility |
|---|---|---|
| Member 1 (Gabutan) | Authentication | Account-request authentication flow, email/password login, logout, session handling and shared authentication/access-check helpers. |
| Member 2 | Membership and Master Data | Members, types, venues/photos; shared event/venue image storage and pagination components. |
| Member 3 | Events and Registration | Events/covers, browsing, signup/review/cancellation, capacity, promotion and cutoffs; My Events shell. |
| Member 4 | Attendance and QR | QR rendering/scanning/downloads, manual fallback, check-in controls, correction and personal attendance; event-attendee screen shell. |
| Member 5 (Pinca) | Certificates, Reports and Administration | Eligibility, fixed-template PDF generation/private downloads, report queries/screens, initial administrator setup, role/access administration and activity-history UI; certificate sections in shared screens. |

Each slice includes database/query work, business rules, UI, authorization/validation, audit integration and tests, ending in a demonstrable result. Member labels are assignment placeholders, not application roles or a promise of equal effort. Member 3 carries the highest lifecycle/concurrency risk; estimate remaining effort before confirming workload balance.

Agree schema and service contracts together before parallel implementation. Each owner contributes reviewed database scripts and coordinated scaffolding changes. Member 1 builds authentication/session and shared access-check helpers; every owner enforces checks and writes appropriate audit entries with their own transactions. Pinca owns administration screens, role/access changes, initial-administrator setup and activity-log viewing. Member 2 builds reusable image and pagination services; consumers own their integrations. Member 3 integrates event covers and creates tokens on confirmation; Member 4 renders/validates them. Pinca's account deactivation and Member 2 membership inactivity invoke Member 3 registration cancellation logic rather than duplicating seat rules. Member 4 attendance correction coordinates issued-state guards and eligibility consequences with Member 5.

Member 3 owns My Events layout with Member 4 QR/attendance and Member 5 certificate sections. Member 4 owns the event-attendee layout with Member 5 eligibility/issuance sections. Retention is implemented per slice; Member 1 coordinates authentication/access conventions. Member 1 leads authentication setup; Pinca owns administrator bootstrap and administration setup; Member 2 owns storage/backups. Each owner provides database constraints/indexes and all five perform incremental integration, end-to-end tests, deployment checks and demo preparation. Shared work is estimated once plus integration effort, not assigned entirely to a final QA person.

## 2. Actors and permissions

| Actor | Responsibilities |
|---|---|
| Applicant | Submit request/password, receive acknowledgment, resubmit after rejection; no login while pending. |
| Member | Browse events, register, cancel within rules, access own profile and My Events (status/QR/attendance/certificates). |
| Officer | Manage requests, members, types, venues, events, registration reviews/promotions, attendance, certificate review/issuance, reports. |
| Administrator | Officer capabilities plus role assignment, account access management, activity-log review and initial administration. |
| System | Enforce authorization, validation, capacity and time guards; record consistent changes/audit. No scheduled promotion/closeout is assumed. |

Officers are members promoted by an admin using the same account and linked profile. Administrators need a member profile only to participate. Management privileges never bypass participation eligibility or capacity. Ordinary fixed-role permissions allow reviewing/promoting own participation and approving/issuing own certificates; the same validation, queue priority and audit requirements apply. There is no additional self-approval prohibition in this release.

Create the first active admin with a one-time setup script using supplied name/email/password and a password hash; never hard-code default credentials. Public requests cannot choose privileged roles. Protect the last active administrator from deactivation or demotion.

## 3. Account request and approval

### 3.1 Submission

Collect required student number, full name, email, course, positive year level, section, password and matching confirmation, plus optional mobile number stored as text. Hash the password; never store confirmation. Applicants do not assign membership type/status or privileges.

A valid submission creates only a pending `account_requests` row. Reject overlapping pending requests using the same email or student number and identities already held by an existing user/member, including inactive identities. Separate pending-only unique indexes are required for request email and student number. Earlier rejected applications may be followed by a new request with corrected information/password; preserve the earlier row. This is not event reapplication.

### 3.2 Review

Transitions: `pending -> approved` or `pending -> rejected`.

Approval atomically creates an active member-role user and linked member profile, saves the resulting user ID in `created_user_id`, transfers the password hash, clears it from the finalized request, saves reviewer/time and writes the audit entry. Copy current name/email/hash to `users` and student/academic/contact details to `members`. No independent Add Member workflow exists.

Rejection requires reviewer, time and reason, clears the request hash, and creates no user/member. Preserve submitted information as historical snapshots. Never expose request hashes in management screens. Concurrent decisions cannot approve a request twice.

### 3.3 Communication

Show submission acknowledgment without promising email or public status tracking. Officers manage outcomes/reasons in request management and communicate offline for the demo. A production applicant-facing outcome channel remains unresolved; no tracking-token feature/table is implicitly added. Do not expose requests using predictable IDs, email or student number alone. After externally communicated approval, the applicant can log in with the submitted credentials.

## 4. Authentication and profile management

Use registered email/password verification against a password hash and signed, expiring cookie authentication. Email is the only login identifier; student number is not accepted for login. Reject pending applicants and inactive accounts. Do not add login throttling or lockout in this MVP. On protected requests, check current account eligibility and current role rather than relying indefinitely on stale claims. Deactivation denies existing access as well as new logins. Apply ownership checks and antiforgery protection to mutations.

Logout clears the browser cookie; individual revocation of copied tokens is not promised. Known-password changes and forgotten-password recovery are deferred; no change-password form/action or substitute administrator reset workflow is included. Initial account-request passwords and supplied setup-admin credentials are still hashed for login. Replace placeholder login and reversible credential storage before deployment. Never log passwords, hashes or QR tokens.

| Field/action | Editor |
|---|---|
| Full name, course/year, section, mobile, membership type/status | Officer/admin for approved member records. |
| Student number | Officer/admin; uniqueness validation, correction reason, old/new audit values. |
| Login email | Read-only; email-change workflow deferred. |
| Existing password | Change/reset workflows deferred; initial passwords are still securely hashed. |
| Role and account active state | Administrator. |

Members view their profile but cannot edit profile details in this release. Current name/email belong to `users`; student number and academic/member data belong to `members`. Profile changes do not rewrite signup snapshots or issued certificates.

## 5. Deactivation and membership inactivity

Account inactivity blocks login/access. Membership inactivity blocks participation but an active account can still view personal history and issued certificates. Neither state is archival; changing one must not silently change the other.

Before applying either change, show its effect on upcoming registrations, require a reason and audit affected actions:

- For events with `now < starts_at`, cancel pending/waitlisted requests and confirmed registrations without valid attendance. Preserve attended registrations, invalidate cancelled tokens and release seats.
- Do not automatically promote waitlisted participants; an officer must run promotion separately.
- For events already started or completed, preserve registration/attendance history. Current ineligibility still blocks new check-in.
- Preserve issued certificates; deactivation is not revocation, although inactive accounts cannot authenticate to download.
- Reactivation restores only access/membership state, not cancelled registrations or released seats. Event reapplication is deferred.

## 6. Master data and required images

Members have a required unique user link, unique student number, positive year level, course, section, optional mobile and membership type, plus active/inactive state. Accounts may have zero or one member profile; profiles originate only through approval.

Event types have unique nonblank names, descriptions and active state. Retain names/references when inactive. Venues contain room name, positive capacity, location, availability notes, contact person, active state and required photo. Room names need not be globally unique. Notes do not imply booking-conflict detection.

Only active types/venues are selectable for draft events and must be revalidated on publication. Deactivation preserves existing event references. Prevent venue capacity reductions below limits of linked draft/published events.

### 6.1 Image policy

| Record | Requirement | Removal |
|---|---|---|
| Draft event cover | Optional during preparation. | Management may remove while drafting. |
| Published event cover | Required before publication. | Replacement only; never remove without replacement. |
| Venue photo | Required at creation. | Replacement only; never remove without replacement. |

One current image per event/venue; JPEG/PNG with validated content and maximum size 2 MiB. Generate filenames on the server. Officers/admins manage venue/event images; user profile-picture uploads and user image storage are not included. Completed/cancelled events are read-only. Storage/cleanup/security rules are in §14.5.

## 7. Event lifecycle and signup availability

### 7.1 Event information

Title, description, type, venue, scheduled start, positive participant limit within venue capacity, registration deadline no later than start, and cover required by publication. No scheduled end or automatic completion is required.

### 7.2 States

| State | Meaning |
|---|---|
| Draft | Preparation; hidden from member feed. |
| Published | Visible; new signup permitted only before deadline; check-in independently controlled. |
| Completed | Concluded; check-in closed; certificate review and final no-show reporting available. |
| Cancelled | Processing/check-in blocked; retained for history. |

Normal flow: `draft -> published -> completed`; eligible drafts/published events may be cancelled. Completed/cancelled events cannot be republished.

### 7.3 Publication opens registration

Publishing requires valid masters, cover, capacity and a future deadline. Record publication actor/time and audit. There is no separate manual opening command or `registration_opened_*` storage.

Signup availability is derived from `status = published AND now < registration_deadline`, plus participant eligibility and duplicate checks. Full events may still accept requests for review/waitlisting with a clear no-guaranteed-seat message. At the exact deadline signup is closed even if an old form remains open. Show the exact local deadline and timezone. Deadline closure leaves the event published and does not cancel existing requests or open check-in.

## 8. Event edits

| Stage | Permitted edits |
|---|---|
| Draft | All fields, with validation. |
| Published | Title, description, cover replacement only. |
| Completed/cancelled | Read-only. |

After publication lock schedule, type, venue, participant limit and deadline. Check-in closure does not unlock them. Retain registrations, log relevant old/new values and preserve issued snapshots/PDFs. No capacity-increase/rescheduling/reopening workflow is included.

## 9. Registration lifecycle

### 9.1 Submission and states

Validate active account/member, published event, deadline and no existing member/event pair. Create one pending row with course/year/section snapshots and submission time. A cancelled row still prevents another registration for that event. Members may register for multiple different events; no overlap restriction.

| State | Meaning | Seat | QR |
|---|---|---|---|
| Pending | Awaiting review; historical/non-actionable at start. | No | No |
| Confirmed | Reviewed and assigned a seat. | Yes | Usable subject to current checks. |
| Waitlisted | Reviewed; awaiting seat; historical/non-actionable at start. | No | No |
| Cancelled | Withdrawn, declined, event-cancelled or administratively cancelled with reason. | No | No |

### 9.2 Review

While published and before event start, officers/admins use Approve/Decline on pending requests, including after signup deadline. Ordinary role permissions apply, including own participation, with audit. Decline becomes cancelled with reason. Approval becomes confirmed only if capacity exists and no prior eligible waitlisted request has priority; otherwise waitlisted. Record reviewer/time and notes. Recheck current eligibility, queue and capacity inside the seat-allocation transaction, not merely when rendering a preview. Confirmation records actor/time and an unpredictable unique QR token.

### 9.3 Explicit waitlist promotion

Officer/admin selects **Promote next eligible participant**. Select the earliest eligible reviewed waitlisted registration ordered by `reviewed_at ASC, registration_id ASC` across the full queue. Require published event, `now < starts_at`, free capacity and current participant eligibility. No additional self-promotion prohibition applies; queue priority must still hold. Preserve original review data, save confirmation actor/time/token and log the human action. Capacity and queue selection/update must serialize with competing approvals/promotions. No candidate/seat means no mutation. No automatic email or promotion occurs; the member sees status/QR in My Events on their next visit.

### 9.4 Cancellation and no reapplication

Owner may cancel pending/confirmed/waitlisted registrations while published, before scheduled start and without valid attendance, even after signup deadline. Require explicit confirmation warning that cancellation is final in this release. Save reason/actor/time, invalidate token and release seat atomically with audit. Do not trigger promotion. No Register Again, exceptional restoration or seat-reclamation workflow is included.

### 9.5 Event-start cutoff

At `now >= starts_at`, reject reviews, confirmations, promotions and member cancellations. Keep pending/waitlisted rows unchanged; show **Not confirmed before cutoff** and disable processing/check-in. Do not convert them automatically to cancelled or count them as no-shows. Confirmed rows remain confirmed. No background closeout job is required.

## 10. Cutoff matrix

| Action | Guard |
|---|---|
| Publish | Draft, required cover/master validation, deadline still future. |
| New signup | Published, active eligibility, no existing registration, `now < deadline`. |
| Pending review/confirmation | Published, eligible participant, `now < starts_at`. |
| Officer-triggered promotion | Published, eligible queue/free capacity, `now < starts_at`. |
| Member cancellation | Published, owner, no valid attendance, `now < starts_at`. |
| Check-in | Published, manually open, confirmed/currently eligible participant, no existing attendance row. |
| Completion | Published, event concluded, `now >= starts_at`. |
| Certificate review/issuance | Completed, valid attendance, required approvals and permissions. |

Store UTC and display organization-local time in Asia/Manila. At exact deadline/start the corresponding action is blocked, including stale forms. A Friday deadline and Monday start allow reviews/promotions/cancellations between those times. Early Monday check-in does not move either cutoff; checked-in participants cannot cancel.

## 11. Attendance control and correction

Officers/admins select a published event and explicitly open/close check-in; early opening and reopening while still published are allowed and audited. Completion/cancellation closes it. No automatic event-start opening is assumed.

All methods validate selected event, open check-in, matching confirmed registration, active account/member and absence of an attendance row. QR scans record automatically after validation; name/student-number lookup requires selecting the participant and clicking Check in. Searching/viewing/downloading never records attendance. Store method, authenticated recorder and UTC time with audit. A unique registration link plus transaction logic prevents simultaneous duplicates; repeated valid scans show the original check-in time.

### 11.1 Correction

Officer/admin explicitly invalidates with confirmation and reason; server fills actor/time. Retain original row/history; registration remains confirmed. Exclude invalidated attendance from reports/eligibility, set affected unissued certificate eligibility to not eligible with correction reason and recheck attendance on every issuance. Serialize correction against final issuance. Issued certificates block ordinary invalidation.

Re-check-in after invalidation is deferred: warn before correction and reject a subsequent check-in against the retained invalidated row. Before start, member cancellation may become possible if other rules hold. After completion a confirmed registration without valid attendance is a no-show. Arrival is recorded, not duration; early departure does not automatically invalidate attendance. Members cannot mark or correct attendance.

## 12. QR credentials

One unique unpredictable token per confirmed event registration, not one permanent student QR. Encode the token only; resolve member/event server-side without embedding credentials or unnecessary personal data. Authorize owner before display/PNG download. Generate PNGs on demand; do not retain QR files or add QR image-path columns.

Members may save screenshots/PNGs or print externally and present offline. Scanning officers require internet for current validation and persistence. Cancellation clears the token; ineligibility rejects saved copies. No reconfirmation after cancellation exists in this release. Successful check-in does not change registration state or permit another attendance row.

| Scan result | Behavior |
|---|---|
| Valid | Record and show participant name/student number for identity checking. |
| Already attended | Show original time; no insert. |
| Wrong event | Reject event mismatch. |
| Ineligible, cancelled, unconfirmed or invalidated attendance | Reject; no insert. |
| Check-in closed or event not published | Reject. |
| Unknown token | Reject without revealing unrelated records. |

A shared screenshot is not proof of identity; officer verification remains necessary.

## 13. Cancellation and completion

Cancel only eligible draft/published events without valid attendance or issued certificates. Require reason/actor/time. Atomically mark cancelled, cancel pending/confirmed/waitlisted registrations, clear tokens, close check-in and audit. Preserve previous reasons for already-cancelled rows. Never promote during cancellation. Keep previously published cancelled events identifiable to affected members; hidden drafts must not become publicly visible merely through cancellation.

An officer/admin explicitly completes a concluded published event at/after start. Close check-in and enable valid-attendee certificate review/final no-show reporting. Time passage alone does not complete an event. Completion before scheduled start and exceptional lifecycle recovery are outside this release.

## 14. Certificate eligibility, issuance and storage

### 14.1 Eligibility

`valid attendance AND completed event AND officer/admin approval = eligible`.

No-show/confirmation alone is insufficient; no unmeasured duration requirement. Create pending certificate reviews only for valid attendees of completed events. Record reviewer/time and require decline reasons; ordinary role permissions also apply to own certificates, with the same validation/audit. Review and issuance share event-attendee management.

### 14.2 States

`pending_approval -> eligible -> issued`, or `pending_approval -> not_eligible`.

Reopening a declined decision is deferred: no `not_eligible -> pending_approval` action. Warn before decline that the decision cannot be reopened in this release. Attendance correction may move unissued eligibility to not eligible with reason. Issued certificates have no ordinary state reversal.

### 14.3 Fixed-template issuance

Officer/admin issues one eligible certificate at a time from event-attendee management. Ordinary role permissions include own certificates with the same checks/audit. Generate a landscape A4 PDF with embedded fonts using one fixed template. Preserve unique certificate number, recipient full name, event title/start, issue date and issuer. No template editor, bulk issuance, email, revocation or correction/reissue.

Prevent duplicate concurrent issuance for a registration. Prepare immutable snapshots and unique output path; write the PDF successfully, then recheck approval/attendance/completion under serialization with corrections before committing issued state, number, snapshots, path, issuer/time and audit. A file write is not a SQL transaction. On file failure do not mark issued; on failed database commit remove or reconcile an unreferenced file. Do not replace a successfully issued PDF on a retry. Report success only after final commit.

### 14.4 Authorized downloads

An active account owner or authorized management can view/download issued PDFs through a backend stream with `application/pdf`. Membership inactivity alone does not revoke historical certificates; account inactivity still blocks authenticated access. Check current account/ownership or management permission on each request. Never expose certificate files via public static middleware, physical paths or arbitrary caller-supplied file paths. Later profile/event changes cannot rewrite snapshots or retained PDFs.

### 14.5 Shared file storage

- `users` has no image-path field. `events.image_path` is nullable for drafts but a nonblank validated cover is required for published/completed events; `venues.image_path` is non-null/nonblank.
- `certificates.file_path` nullable before issuance, required when issued; contains a relative private PDF path, never a PNG/image field.
- Configure persistent local storage outside public web and application deployment directories, separated by asset type. Use relative paths, unique server-generated filenames, content/type/size validation and path-containment checks; reject traversal and unsafe components.
- Authorize uploads and serving. Images are exposed publicly only intentionally; certificate ownership checks are always required. Retain the old image until its replacement path commits; clean up/reconcile failed/new orphan files without deleting the referenced original.
- Mandatory venue photo and event publication must not commit with missing/failed uploads. Check existence and validity, not merely a client-supplied path. Required image replacement cannot temporarily commit null. Completed/cancelled covers remain unchanged.
- Restrict filesystem access to the application account; use persistent disk surviving restart/redeployment, capacity monitoring and coordinated file/SQL backups and restores. Ephemeral deployment disks are unsuitable. One application server is assumed; multiple instances require shared storage or a later provider decision.

## 15. Participation reports and snapshots

Officers/admins view registrations/statuses, confirmed participants, valid attendees, no-shows, attendance rate and course/year distribution. The per-event participant detail list shows participant name, student number, course/program, registration status, attendance result and check-in time where present. Use simple tables/totals; CSV/PDF report exports are deferred (certificate PDF downloads are distinct).

For completed, non-cancelled events:

- Attendee: confirmed registration with valid attendance.
- No-show: confirmed registration without valid attendance.
- Attendance rate: `confirmed registrations with valid attendance / confirmed registrations * 100`; zero denominator displays N/A.
- Pending/waitlisted/cancelled rows, including cutoff history, are excluded from expected attendance and no-shows. Cancelled events are shown separately.

Capture course/year/section once at signup. No reapplication refresh exists. Later profile edits do not rewrite historical distributions. Show actual-attendee course/year distribution only; keep the participant list, confirmed count, attendee count, rate and no-shows, but defer confirmed-participant distribution. A separate snapshot-correction editor is not promised by this baseline. Account/master inactivity must not hide relevant historical reports. Pagination changes only detail rows, never full-dataset aggregates.

## 16. Retention without archive/restore

No general archive/restore UI, `deleted_*` fields, deletion reasons or cascading soft deletion. Preserve business rows/references and identities. Use account active state, membership active state, type/venue active flags, event lifecycle, registration cancellation, attendance invalidation and certificate decisions instead.

Inactive identities remain reserved. Reactivation does not restore cancelled registrations, reclaim seats, republish events, restore attendance or revoke/rewrite certificates. Requests retain review history; logs remain append-only. Controlled cleanup of unreferenced files is storage maintenance, not permission to delete business history or issued PDFs.

## 17. Audit and integrity

Audit decisions, role/access/member changes, profile/identity corrections, images, event publication/edits/cancellation/completion, check-in controls, registration reviews/promotions/cancellations, attendance corrections and certificate decisions/issuance. Do not log deferred workflows as implemented operations.

Log actor type and account where applicable, action, entity type/ID, description/reason, timestamp and relevant old/new values as validated JSON. The viewer is one read-only chronological list of actor, action, affected record, timestamp and reason/short description; advanced filtering and field-by-field comparisons are deferred, although useful old/new values remain stored. Never include credential-bearing row dumps, hashes, tokens or physical storage paths. `entity_id` is an application-validated polymorphic reference; only actor user ID has a direct user FK. User actions require a real actor; system validation does not invent a user. Business mutations and required database audit writes commit together. Latest update fields do not replace history.

Enforce one account/email, member/student number, registration/member-event, attendance/registration and certificate/registration; positive capacity/year; deadline ordering; valid seeded roles/statuses/methods; required state-specific fields and paired actors/timestamps. Revalidate on action submission. Capacity and lifecycle transitions require transactional logic beyond FKs. No background scheduler is needed to enforce time guards.

## 18. ERD and SQL Server conventions

### 18.1 Ten core business tables

| Table | Responsibility |
|---|---|
| `users` | Current name/email/hash, role/access state, creation/update audit; no profile-image field. |
| `account_requests` | Submission snapshots/pending hash, decision and unique nullable resulting user link. |
| `members` | Unique account/student number, academic/contact data, type/state, creation/update audit. |
| `event_types` | Unique name/description, active state, creation/update audit. |
| `venues` | Location/capacity/notes/contact, required photo, active state, creation/update audit. |
| `events` | Details/cover, publication, check-in controls, completion/cancellation, creation/update audit; no separate signup opening fields. |
| `registrations` | Unique member/event, signup snapshots, review/confirmation/cancellation, nullable token, creation/update audit. |
| `attendance` | Unique registration, method/recorder/time and invalidation fields. |
| `certificates` | Unique registration, eligibility/review, retained PDF path, unique number, immutable snapshots and issuance/audit fields. |
| `activity_logs` | Append-only actor/entity/action/reason/old-new history. |

Physical seeded lookup tables increase SQL Server table count without changing the ten-business-table scope. No email verification/reset/session tables are included. `activity_logs.actor_type = system` remains available for consistent system-generated records, not as a claim of automatic waitlist promotion.

### 18.2 Relationships

One user has zero/one member; each member has one user. Approved requests link one resulting user; setup admins need no request. Members/events each have many registrations, resolving participation. A registration has zero/one attendance and zero/one certificate. Types/venues each have many events. For standalone DBML one-to-one relations put child FK second:

```dbml
Ref: users.user_id - members.user_id
Ref: users.user_id - account_requests.created_user_id
Ref: registrations.registration_id - attendance.registration_id
Ref: registrations.registration_id - certificates.registration_id
```

### 18.3 Types and audit fields

Use SQL Server/EF Core database-first. Readable enums become stable seeded lookup rows/FKs, not editable admin data. Use UTC `datetime2(3)` and creation defaults `SYSUTCDATETIME()`; SQL Server `timestamp` is binary rowversion, not event time. Maintain UTC convention explicitly because datetime2 has no timezone. Use bit, nvarchar and nvarchar(max); mobile numbers remain text.

Actor precedes time in paired creation/update/review/confirmation/check-in/invalidation/completion/cancellation fields. Update fields start null and are set explicitly on change. Bootstrap `users.created_by` may be null; approval sets active true despite the false default. Human confirmation/promotion requires `confirmed_by`; original review remains intact. Attendance uses recorded fields rather than duplicate creation fields. System-created certificate reviews may have null creator, but human review/issuance requires an actor.

### 18.4 Constraints and indexes

- Unique filtered indexes excluding null for request resulting user, QR token and certificate number. Plain nullable unique constraints have different SQL Server null behavior.
- Separate unique pending-only request indexes for email/student number using stable seeded status IDs. Ordinary queue indexes do not enforce pending uniqueness.
- Normalize identifiers consistently and choose suitable case/collation rules. Validate positive values, actor/time consistency, nonblank required paths, event ordering, review/issuance completeness and JSON validity.
- Cross-table capacity and required existing cover/file rules require service/transaction checks or suitable database logic. A non-null path alone does not establish file validity.
- Queue index follows event/status/reviewed_at/registration_id. Personal histories, feed and log ordering use unique ID tie-breakers. Verify actual SQL Server query plans.
- No archive fields or registration-opening relationships. QR tokens are set on confirmation and cleared on cancellation, never logged. Attendance remains one row even after invalidation.

The DBML is a design artifact, not evidence that a deployed database already enforces the rules.

## 19. Shared list browsing

Apply fixed 25-row server-side pagination with Previous/Next to requests/accounts, members, types, venues, events/feed, review/personal registrations, manual name-search results, attendance histories, certificate queues/download lists, report detail rows and logs. Single-record forms/exact student-number lookup do not require paging. Hide navigation for a single page; show empty state for no matches.

Pagination is included in the relevant functional WBS tasks and their estimates, not a separate SHR module/task or separate pagination estimate. Member 2 can provide reusable helpers; each task owner implements and tests its list integration. US-29 remains a shared acceptance-criteria reference in the user-story document. WBS labels and predecessors use module codes (AUTH, MD, EVT, REG, ATT, QR, CERT, REP, ADM) with explicit application actors; developer ownership remains separate from access permissions.

Apply current authorization, ownership, search and allowlisted filters before counts and slicing. Sort deterministically with unique ID tie-breaker. Default request order is requested_at/request_id ascending; feed starts_at/event_id ascending; members name/member_id; logs occurred_at/log_id descending; waitlist reviewed_at/registration_id ascending. Define stable defaults for remaining lists.

Normalize invalid/missing pages to 1 and out-of-range pages to last available; no page-size selector or arbitrary client page size. Reset after filter changes, preserve supported query state in navigation and refresh/fall back after mutations. Never disclose unauthorized counts or accept raw SQL ordering. Revalidate actions against current records, not page positions.

Report aggregates and officer promotion inspect full applicable datasets, independent of visible rows. Paging/filter values are request/UI state, not new ERD columns/tables.

## 20. Delivery verification and remaining configuration

Include database constraints, shared services, file persistence/backup, integration, security, concurrency, deployment and demo work in estimates. All MD/MM estimates remain TBD; avoid double counting and state working days/month before conversion. No new delivery commitment is inferred from the historical October 7 deadline.

The release gate is **26/26 functional IDs demonstrated end to end on the integrated application, with every minimum acceptance coverage item below passing**. Apply this gate to the integrated project release, not to the frontend-only workspace by itself.

- Account duplicates, rejection/resubmission, atomic approval and inactive/current-role access checks.
- Mandatory venue photo and publication cover, invalid/oversized files, replacement/SQL failures and safe private download; no user profile-picture workflow.
- Deadline/start boundaries including stale forms, publication-open signup, final cancellation/no reapplication and retained unresolved cutoff history.
- Concurrent final-seat approvals/promotions, full-queue fairness and no automatic promotion on cancellation/deactivation.
- QR wrong-event/unknown/ineligible/duplicate scans, valid-scan display of participant name and student number, manual fallback, early/reopened check-in, correction restrictions and no re-check-in.
- Successful registration for separate events whose scheduled times overlap; the MVP does not block overlapping registrations.
- Completion, eligibility, concurrent issuance/correction, immutable PDF downloads, unauthorized access and missing-file handling.
- Full-dataset reports independent of paging, zero denominator, corrected attendance, inactive historical records and restricted counts.
- Initial administrator setup, persistent storage across redeployment, backups/restores, build/review/integration testing and demonstration.

Use Asia/Manila as the organization-local timezone. The fixed certificate template follows the approved design; authorized signatory name/title/signature remain pending and must be supplied before official certificate issuance. Applicant-facing outcome communication beyond the officer-managed offline demo remains outside MVP scope. The reduced scope does not authorize adding public tracking, operational rescheduling or exceptional certificate repair silently.
