# AttendTa frontend/backend alignment handoff

Prepared: 2026-11-15
Branch: `feature/ui-ux-consistency`
Application checkpoint: `226523b`
Related plan: [UI/UX phased implementation plan](../ui-ux-implementation-plan.md)

## 1. What this handoff means

Phases 1 and 2 and their approved review refinements are implemented. Phase 3 has not started.
The certificate availability fix and actual-PDF modal were approved and completed as follow-ups.
The remaining phases are still future work. Full integrated visual sign-off remains Phase 7.

This document distinguishes three kinds of work:

| Status | Meaning |
| --- | --- |
| **Implemented** | Exists in the current branch. Preserve and test it; do not build a second version. |
| **Deferred requirement** | Discussed in this session but explicitly left unimplemented. This handoff is not evidence that it already works. |
| **Proposed implementation** | A recommended way to implement a future requirement. Resolve the noted choices before changing behavior. |

The latest user direction was to change profile **placeholders only**, then hand off the backend
alignment work for later. Do not silently include stricter profile validation in the current PR.
The user asked to receive this file before the PR is created.

## 2. Current frontend and backend state

The branch includes backend changes already needed to support the shipped interface. It is not
an exclusively visual change, and its PR must not claim that all backend code is untouched.

| Area | Current behavior | Remaining backend work |
| --- | --- | --- |
| Course | BSCS/BSIT dropdowns, canonical values, legacy full-name matching, service validation | No new course format change is needed. Preserve this flow. |
| Year level | Dropdown offers 1–4; submitted profile validation accepts 1–4 only | Implemented 2026-11-15 (issue #238): 1–4 enforced on both forms; dropdowns and list filters read `ProfileOptions.YearLevels`. |
| Student number | Text, required, exactly eight ASCII digits; placeholder `23807613` | Implemented 2026-11-15 (issue #238). |
| Mobile | Optional text; eleven ASCII digits starting with `09` when supplied; placeholder `09171234567` | Implemented 2026-11-15 (issue #238). The `09` prefix was added by product decision; see §3.1. |
| Email | Required valid email on signup; placeholder `name@gmail.com` | No Gmail-only rule. Keep normal valid email addresses supported. |
| Section | Required text, trimmed; form suggests A–F but permits other values | No Section filter or new strict Section vocabulary was approved. |
| Lists | Server filtering/counting/paging, shared query contracts, adaptive row count | Preserve contracts; future bulk selection must use these paged results correctly. |
| Account role change | Shared modal and fallback form use existing audited role command | Preserve authorization, last-admin protection, field errors and modal response contract. |
| Deactivation | Separate preview menu option removed; normal confirmation includes impact and reason | No deactivation business-rule change is required. |
| Certificate viewing | Actual retained PDF in a modal; file availability checked; direct PDF endpoints retained | Restore missing original files operationally where possible; do not regenerate during reads. |
| Bulk approval | Not implemented | Phase 4: membership requests, registration review/dossier, certificate eligibility. |

Changing acceptable input values usually means changing validation. It does **not** inherently
require renaming properties, changing JSON/form field names, adding tables, or converting text
identifiers into numeric database types.

## 3. Profile rules to implement later

### 3.1 Target contract

| Field | Intended submission rule | Storage type to retain | Example error |
| --- | --- | --- | --- |
| `Course` | `BSCS` or `BSIT` | String | Choose BSCS or BSIT. |
| `YearLevel` | Integer 1–4 | Integer | Choose a year level from 1 to 4. |
| `StudentNumber` | Exactly eight ASCII digits, required | String | Enter an 8-digit student number. |
| `MobileNumber` | Empty or eleven ASCII digits starting with `09` | Nullable/optional string | Enter an 11-digit mobile number starting with 09. |
| `Email` | A normal valid email address | String | Enter a valid email address. |
| `Section` | One of A–C; the service upper-cases | String | Choose a section from A to C. |

The examples do not authorize these additional restrictions:

- Do not require Gmail addresses or a university email domain.
- ~~Do not require a mobile prefix such as `09`; that was not requested.~~ **Amended 2026-11-15
  (issue #238):** the `09` prefix is now required. The original rule admitted any eleven digits,
  so `12345678901` passed; a Philippine mobile number always starts with `09`, and the product
  owner chose to keep the stricter rule rather than revert it. `ProfileOptions.MobileNumberPattern`
  is the single source.
- Do not make mobile required without an explicit product decision.
- Do not mention a country in mobile helper/error copy.
- Do not restrict Section to A–F merely because a datalist suggests those values.
- Do not add a Section filter as part of validation work; that is separate scope.

Keep student/mobile values as strings so leading zeroes survive. Use `[0-9]`, not a broad Unicode
digit class, if the requirement is eight/eleven ordinary ASCII digits.

Recommended patterns after surrounding whitespace is trimmed:

```text
StudentNumber: ^[0-9]{8}$
MobileNumber:  ^09[0-9]{9}$ when nonempty
YearLevel:    1 <= value <= 4
```

Trim outside whitespace using the existing normalization convention. Reject internal spaces,
dashes, letters and other punctuation. Do not silently strip characters: changing an identifier
such as `23-807613` into another stored value could create a collision or hide a data error.

### 3.2 Files that own the rules

| File | Required change when this work is authorized |
| --- | --- |
| `ASI.Basecode.Resources/Constants/ProfileOptions.cs` | Offer year levels 1–4. Keep BSCS/BSIT and legacy course normalization. Consider shared numeric length/range constants to prevent drift. |
| `ASI.Basecode.Services/ServiceModels/AccountRequestViewModel.cs` | Add student/mobile patterns and change year range to 1–4; retain Required/StringLength as appropriate. |
| `ASI.Basecode.Services/ServiceModels/MemberViewModel.cs` | Apply matching submission rules to member creation/editing. Keep the student-number correction reason contract. |
| `ASI.Basecode.Services/Services/AccountRequestService.cs` | Preserve Normalize-before-Validate and direct service-boundary validation. Verify approval compatibility with old pending requests. |
| `ASI.Basecode.Services/Services/MemberService.cs` | Preserve Normalize/Validate for creation and editing, uniqueness checks, correction audit, and registration snapshots. |
| `ASI.Basecode.WebApp/Controllers/AccountRequestController.cs` | Keep field errors and nonsecret input on failure; clear password fields. Handle MVC validation and service validation consistently. |
| `ASI.Basecode.WebApp/Controllers/MemberController.cs` | Preserve modal/fallback validation behavior and retained input. |
| `ASI.Basecode.WebApp/Views/AccountRequest/Request.cshtml` | Align HTML controls/helpers with server rules in the same release. |
| `ASI.Basecode.WebApp/Views/Member/_Form.cshtml` | Use the same controls/rules as signup; keep audited student-number corrections. |
| `ASI.Basecode.WebApp/Models/ListToolbarViewModel.cs` | Year filter choices come from the shared options; labels stay numeric. |
| `ASI.Basecode.WebApp/Models/ListReadOptionsFactory.cs` | Currently accepts query year 1–10. Reconcile read filters with the new write rule and the chosen legacy-record policy. |

MVC runs model validation before controller actions. Service normalization does not retroactively
clear MVC ModelState errors. Check both paths; a service-only test is insufficient to prove that
the real form accepts and rejects the intended input.

Client controls should remain text/tel inputs with an appropriate numeric keyboard hint, pattern
and length attributes. Do not use `type="number"` for student/mobile identifiers. Client checks
help users; they do not replace service validation or existing database uniqueness protection.

### 3.3 Course flow already works

`ProfileOptions.Courses` provides both displayed options and submitted values: `BSCS`, `BSIT`.
Services normalize the recognized legacy values:

| Legacy value | Canonical member/new-request value |
| --- | --- |
| `BS Computer Science` | `BSCS` |
| `BS Information Technology` | `BSIT` |

The signup service stores the selected code. Approval passes academic data to
`MemberService.CreateMemberFromApprovedAccountRequestAsync`, which normalizes and validates it.
Members and Membership Requests filters match the code and its recognized legacy full name.

Preserve these files:

- `ASI.Basecode.Data/Repositories/AccountRequestRepository.cs`
- `ASI.Basecode.Data/Repositories/MemberRepository.cs`
- `ASI.Basecode.Services/Services/AccountRequestService.cs`
- `ASI.Basecode.Services/Services/MemberService.cs`

Do not rewrite historical course/section/year snapshots in event registrations when a current
member profile is edited. Existing request history also need not be rewritten just to display
a canonical code in a new member profile.

### 3.4 Existing records must be addressed before stricter validation

Older test/demo or genuine records may have student numbers containing prefixes/dashes, year 5
or higher, or formatted mobile numbers. New validation can block more than new signup:

1. Approval of an old pending request calls member creation and therefore validation.
2. Editing an unrelated field in an old member profile can validate its old academic/contact data.
3. Read filters may need to expose legacy records even when new submissions accept only 1–4.
4. Existing tests often use generated student numbers with prefixes to ensure uniqueness.

Before implementation, inventory affected records read-only. Do not reset the database or bulk
truncate, strip or coerce values. Decide on an explicit correction path with retained input,
an explanatory error and audit evidence. There is no assumption here that an old request-editing
screen already exists.

Recommended release approach: keep historical records readable, enforce the new contract on new
submissions, and define how old pending requests/profile edits can be corrected without silent
data loss. Whether an unchanged legacy value may remain during an unrelated edit is a product
decision, not an implementation detail to guess. If exceptions are allowed, encode them narrowly
at the service boundary and test them; do not disable validation for the whole model.

Do not silently interpret a legacy `year=5` link as All years if such records still exist. Decide
whether to support an explicit legacy read filter or show a clear unsupported-filter message.
The normal new-submission choices should still be 1–4 once that rule ships.

### 3.5 Database impact

The current schema already stores student numbers as `nvarchar(50)`, mobile as `varchar(30)`,
course as `nvarchar(100)` and year level as an integer. The year CHECK constraints enforce only
positive values. These columns can hold the proposed values without any structural migration.

Application validation can therefore be implemented without changing column types or widths.
The user explicitly chose to keep the ERD and database schema unchanged. Do not add or change
database CHECK constraints, columns, types, tables or EF schema mappings for this follow-up.
Implement the agreed rules in application validation and matching form controls. Never run a
reset script to make new validation tests pass.

## 4. List/query contracts already implemented

These backend capabilities are part of this branch and should not be reimplemented during the
next phase:

| Concern | Owning files/contract |
| --- | --- |
| Request-scoped criteria | `ASI.Basecode.Data/Queries/ListReadOptions.cs`, `ASI.Basecode.WebApp/Models/ListReadOptionsFactory.cs` and existing DI/middleware wiring |
| Page bounds/ranges | `ASI.Basecode.Services/ServiceModels/PageRequest.cs`, `PagedResult.cs` |
| Filter/reset/navigation links | `ASI.Basecode.WebApp/Models/ListNavigation.cs`, `ListPagingLabels.cs` |
| Shared controls | `Views/Shared/_ListToolbar.cshtml`, `_Pager.cshtml`, `_ResultCount.cshtml` |
| Measured row count and context | `wwwroot/js/features/list-controls.js` |

Current visible criteria:

| Surface | Query fields used by its interface |
| --- | --- |
| Members | `search`, `course`, `year`, `status` |
| Membership Requests | `search`, `course`, `year`, `status`; existing supported `sort` is preserved |
| Event Management | `search`, `filter` for status, `typeId` for existing classification |
| Event Types / Venues | `search`, `status` |
| Registration Review | `search`, `status`, `eventId` |
| Event dossier roster | `search`, `status`, event identity from the route |
| Certificate Eligibility | `search`, `filter`, `eventId`, `tab` |
| Issued Registry | `search`, `eventId`, `tab=issued` |
| Participation Report | `search`, `type` for attendance, `eventId` |
| Activity Log | `search`, `filter` for action |
| Campus Events | `search`, `filter` for Active/Completed; Active includes upcoming published events |
| Participation History | `search` within its existing action/section |

Contracts to preserve:

- Search/filter before total count and paging. Do not filter only the current visible rows.
- The page result carries actual requested/normalized size, correct total and exact first/last range.
- Default size is 5; validated sizes are 1–25. Dossier roster has a five-row minimum.
- Auto-fit can request more than five rows on taller windows. Never hard-code 25 in one repository.
- Keep the server's normalized size in every view model/footer.
- Changed criteria reset page/fit. Ordinary pagination preserves criteria.
- One Reset clears search/list filters and preserves selected event/history context.
- Query values are filters, not authorization. Member data remains owner-scoped on the server.
- Retired controls must not continue filtering invisibly through old URLs or hidden inputs.
- Native mutations and modal saves must return to the appropriate list context.
- Natural page scrolling remains available; do not clip rows or disable scrolling to force a viewport fit.

Section is now visible beside course/year on Members. Email is also visible, and Actions uses
compact width. Those are presentation changes; they do not imply a new Section filter or schema.

## 5. Existing modal contracts to preserve

### Account role editor

`AdminAccountsController`, `Views/AdminAccounts/_RoleEditor.cshtml`, `_RoleFields.cshtml`,
`Views/Shared/_RecordDialog.cshtml` and `wwwroot/js/features/record-dialog.js` share the existing
record-editor pattern:

- A fetch with `X-Record-Dialog: true` receives a partial form marked `data-record-form`.
- Validation/domain failures return that form with field errors and retained role/reason.
- Success returns `{ "saved": true }`; JavaScript reloads the originating filtered list.
- The regular route still serves a full-page fallback.
- Role changes use the existing audited command, actual route-owned target and antiforgery.
- Required reason, current-actor authorization and the last-active-administrator guard remain.
- Controls lock during saving; edited input is protected from accidental dismissal.

Do not replace this with a new role API or a new global JavaScript editor implementation.

### Deactivation

The separate Preview deactivation menu option was removed. The existing Deactivate confirmation
already displays affected registrations, target identity, reason and Cancel/Confirm. No account
or membership deactivation rule changed. Preserve the distinction: account access and membership
participation are separate states with different effects.

Removal of the menu option did not retire the historical preview endpoint. Backend endpoint
retirement is separate cleanup and must account for existing callers/tests. Do not mistake it for
a security or behavior change in this branch.

## 6. Certificate availability and actual-PDF modal

### 6.1 Implemented backend support

| File | Responsibility |
| --- | --- |
| `Services/Services/CertificateFileAvailability.cs` | Read-only check of a safe private certificate key, readable stream and `%PDF-` signature. |
| `Services/Services/CertificateLedgerService.cs` | Checks availability for visible issued rows; viewer refuses unavailable files; direct PDF reads reject corrupt bytes. |
| `Services/Services/MyCertificatesService.cs` | Member history checks availability while preserving the recorded Issued state. |
| `Services/ServiceModels/CertificateLedgerViewModels.cs` | Carries `HasDocument` for the view. |
| `WebApp/Controllers/CertificateLedgerController.cs` | Preserves authorized PDF routes/status codes and gives management a useful unavailable message. |

Paths in this table are under `ASI.Basecode.` projects: `Services/...` means
`ASI.Basecode.Services/...`, and `WebApp/...` means `ASI.Basecode.WebApp/...`.

There is no new issuance endpoint, automatic regeneration, reissue, bulk issuance or database
migration. Do not change Issued to Pending/Eligible merely because a file is missing.

### 6.2 Implemented frontend support

- `Views/Shared/_CertificatePreview.cshtml` is the shared modal shell.
- `wwwroot/js/features/certificate-preview.js` fetches the authorized PDF, renders retained bytes,
  supports Retry/Close, restores focus and releases render/worker/blob resources.
- `wwwroot/lib/pdfjs/` contains pinned PDF.js 6.5.438, its license and provenance instructions.
- PDF.js is loaded on demand from the app itself, not a third-party CDN.
- Canvas pages are accompanied by extracted text for assistive technology.
- Download returns the same retained PDF; Open in new tab remains available.
- Issued Registry and member certificate history share the modal. The standalone viewer and
  direct document routes remain as fallbacks.
- Missing/corrupt documents show Document unavailable instead of an unusable View PDF link.
- A slow fetch is cancellable. Read-only loading must not set the dialog's protected-save busy
  flag, which would prevent dismissal through the shared dialog guard.

PDF.js usage reference: https://mozilla.github.io/pdf.js/examples/

### 6.3 Identifier and access contract

The PDF/viewer route `id` is the **registration ID**, not the table's certificate primary key.
Some existing view-model properties are called `CertificateId` but intentionally carry the
registration ID for these routes. Do not substitute `certificates.certificate_id` during refactoring.

Management can read issued certificates; members can read only their own. Authentication,
ownership, private-path validation and current state are rechecked on direct reads. A non-owner
must not learn whether somebody else's certificate exists. Do not use the list's `HasDocument`
flag as permission or as a guarantee that the file still exists by the time it is opened.

### 6.4 Remaining local storage problem

At investigation time, the local preview had eight Issued rows: two matching retained PDFs,
five references to missing files and one missing file path. No matching originals for the missing
documents were found in the project's local folders. The six unresolved records are now shown
honestly as unavailable; their files were not reconstructed.

For a real recovery:

1. Identify the database and private `Common:SetupRoot` belonging to the same environment/backup.
2. Find the original retained file matching each record's exact private key.
3. Restore only verified originals under the correct private certificate area; validate identity
   and bytes before replacing or associating anything.
4. Do not copy another person's PDF or rename an unrelated PDF to make a path exist.
5. Verify the authorized PDF endpoint and member ownership after restoration. Reloading the list
   should then make View PDF available without changing Issued or rerunning issuance.
6. If originals cannot be recovered, leave the document unavailable. A formal repair/reissue policy
   would be new scope requiring explicit product/audit decisions.

Keep database backups and private file backups together. Keep the same external SetupRoot across
restarts/deployments. See [storage backup/restore](../storage-backup-restore.md) and
[demo host setup](../demo-host-setup.md). Do not copy secrets or local credentials into the handoff/PR.

Availability reads currently open a small file header, not an entire PDF. Member history checks
issued rows before its final paging. If this grows expensive, move checks to visible rows or batch
the metadata lookup without weakening owner scoping or caching a false promise of availability.

## 7. Phase 4 bulk approvals: real backend work still required

This is separate from the profile-validation follow-up and has not been implemented.

| Page | Bulk action | Must remain individual |
| --- | --- | --- |
| Membership Requests | Approve selected pending requests | Decline |
| Registration Review | Approve selected pending registrations | Decline, cancellation |
| Event dossier pending roster | Same registration approval use case | Decline, cancellation |
| Certificate Eligibility | Approve selected pending eligibility reviews | Decline, PDF issuance |

No bulk certificate issuance. Eligibility approval does not generate a PDF.

### Recommended implementation boundaries

Extend the existing controller → service → repository architecture. Reuse these command owners:

- `AccountRequestService.ApproveAsync`
- `RegistrationReviewService.ApproveAsync`
- `CertificateLedgerService.DecideAsync` with approval

Relevant controllers are `AccountRequestController`, `RegistrationReviewController`, the dossier
actions in `EventController`, and `CertificateLedgerController`. Review the corresponding service
interfaces and DI registrations before adding contracts. The following is a proposed design,
not an existing endpoint promise:

- A bounded list of selected IDs and the relevant selected-event context.
- A shared result containing each ID, actual resulting state or failure, and a useful message.
- No client-supplied actor, role, eligibility, capacity or approval outcome.

Server responsibilities:

1. Require the existing appropriate role and antiforgery on every mutation.
2. Deduplicate and bound IDs; verify the selected-event scope for every event-specific record.
3. Re-read status, actor, account/member activity and event facts at execution time.
4. Preserve transaction/locking conventions and atomic per-record mutation/audit behavior.
5. Decide and document batch semantics. Partial success with per-record results is recommended,
   but do not wrap existing transaction-owning commands in an unreviewed nested transaction.
6. Process in a deliberate deterministic order where capacity or waitlist priority can matter.
7. Distinguish Confirmed from Waitlisted. Current registration approval may place a participant
   on the waitlist; a successful approval is not a promise of a confirmed seat.
8. Preserve capacity, start/cutoff, attendance and eligible-waitlist rules. Do not bypass them by
   issuing a raw bulk SQL status update.
9. Preserve one audit per actual successful change and avoid duplicate effects on retry.
10. Return stale/already-decided/missing/blocked results clearly without revealing unauthorized data.

Frontend responsibilities when that backend exists:

- Select eligible pending rows, with a selected count and confirmation identifying targets.
- Select all means **all eligible rows on this page**, not unseen filtered database results.
- Clear selection on page, event, search/filter or page-size change. No hidden selection.
- Keep individual approval available, especially for small batches and retrying failures.
- Show actual per-record outcomes rather than a blanket “everyone approved” toast.

Do not add checkboxes that imply a working bulk command before this end-to-end behavior exists.

## 8. Remaining phases and backend alignment

| Phase | Main follow-up | Backend implication |
| --- | --- | --- |
| 3 | Draft form, publishing guidance, cover modal | Reuse lifecycle/cover commands; expose specific publish-readiness failures and keep field-level errors. Review before deciding whether additional read models are needed. |
| 4 | Bulk approval | Add bounded authorized orchestration/results as described above. |
| 5 | Event dossier/member detail hierarchy and report-card alignment | Mostly presentation; preserve event-wide metric scope vs participant filters. Add selector date/venue read data only where missing. |
| 6 | Login/request form spacing, member journeys, certificate return context | Mostly presentation/navigation; preserve authentication, owner scoping and meaningful server error recovery. |
| 7 | Full responsive/accessibility/integration review | Validate current behavior rather than inventing new business rules to make the UI easier. |

For Phase 3, explain that registration opens on publishing; there is no approved scheduled
registration-opening feature. Keep the difference between saving a draft and being ready to publish.
Use existing date/capacity/cover/state guards as the authority, and return actionable reasons rather
than a generic publish error. Do not loosen those guards to make a confusing form pass.

Cover management must retain private upload validation, size/type checks, safe storage and the
standalone fallback. A modal should not discard an unsaved event form or create nested modal chains.

## 9. Validation and acceptance checklist for the backend owner

### Profile validation follow-up

- Signup and member edit accept BSCS/BSIT, years 1–4 and a valid eight-digit student number.
- Leading zeroes remain intact in stored student/mobile values.
- Empty mobile remains valid; eleven digits succeeds; dashes, spaces, letters and wrong lengths fail.
- Years 0, 5, negative and malformed values fail on new submissions.
- Student numbers of 7/9 digits, mixed letters/punctuation or non-ASCII digits fail.
- Gmail and other normal email domains work; invalid email still fails.
- A crafted POST and a direct service call cannot bypass the same rules.
- Validation failure preserves safe input and field errors in both modal and full-page forms;
  passwords are cleared on membership request retry.
- Duplicate student/email checks still work, including pending requests and inactive members.
- Approval of a legacy pending request follows the explicitly chosen correction policy.
- A legacy member edit does not silently alter identifiers, status or registration snapshots.
- Read filters, counts, page boundaries and retained query values remain correct.

### Certificate/bulk/lifecycle follow-ups

- Issued plus missing/corrupt PDF remains Issued and offers no broken document link.
- Restoring a verified original makes it available again without another issuance or audit mutation.
- PDF download bytes are the retained bytes; no document generation occurs on GET.
- Modal loading, module/document failure retry, Escape/Close, focus return and list preservation work.
- Non-owner/anonymous/inactive access remains refused under the existing rules.
- Bulk tests cover all-valid, mixed/stale, duplicate IDs, repeated submit, cutoff/capacity/waitlist,
  wrong event, forbidden actor, per-record results and exact audit counts.
- Publish-readiness errors identify a fix and preserve input without changing lifecycle rules.

Useful existing tests to extend rather than replace:

- `tests/ASI.Basecode.Services.Tests/AccountRequestServiceTests.cs`
- `tests/ASI.Basecode.IntegrationTests/AccountRequestSubmissionTests.cs`
- `tests/ASI.Basecode.IntegrationTests/AccountRequestDecisionTests.cs`
- `tests/ASI.Basecode.IntegrationTests/MemberManagementTests.cs`
- `tests/ASI.Basecode.IntegrationTests/CompactListFlowTests.cs`
- `tests/ASI.Basecode.IntegrationTests/ListPresentationTests.cs`
- `tests/ASI.Basecode.IntegrationTests/AdminRoleAssignmentTests.cs`
- `tests/ASI.Basecode.IntegrationTests/CertificateLedgerFlowTests.cs`
- `tests/ASI.Basecode.IntegrationTests/CertificateIssuanceTests.cs`
- `tests/ASI.Basecode.IntegrationTests/CertificateReviewDecisionTests.cs`

When stricter input rules land, update valid test fixtures to generate compliant unique values.
Keep deliberately invalid/legacy fixtures where they test compatibility or validation failures.
Do not change production validation merely to accept an old fixture format.

Build/check entry points:

```powershell
dotnet build ASI.Basecode.sln --no-restore --configuration Release
node --test ASI.Basecode.WebApp/tests/*.test.js
dotnet tests/ASI.Basecode.Services.Tests/bin/Release/net10.0/ASI.Basecode.Services.Tests.dll -parallelMode none
```

Integration tests require `ATTENDTA_TEST_SQL` targeting a verified local SQL Server `master`.
The fixture creates/drops generated `AttendTa_IT_<guid>` databases. Do not point it at a shared
application database or run destructive setup/reset scripts. The certificate browser-flow test
also requires the installed Playwright browser/rendering prerequisites.

At checkpoint `226523b`, the clean Release build, 329 service checks, 24 JavaScript checks and
16 affected certificate/compact-list integration tests had passed; focused follow-up browser tests
also passed after retry/accessibility corrections. This is recorded evidence for that checkpoint,
not a claim that the entire integration suite or Phase 7 visual coverage has been completed.

## 10. Suggested order for the next implementation session

1. Read this file, the phased plan, root `DESIGN.md`, `PRODUCT.md` and repository instructions.
2. Recheck Git state; preserve unrelated work. Do not reset to a branch tip over user changes.
3. Resolve the legacy-record policy before enforcing new profile rules.
4. Implement shared profile validation and matching form/filter changes together, in its own commit.
5. Run meaningful boundary/flow tests; keep the ERD/schema unchanged and preserve stored history.
6. Resume Phase 3, then Phase 4, then the remaining phases in the agreed sequence.
7. Commit and stop after each phase for review, as the user requested.

Keep separate commits/PR scope for profile rules, lifecycle/cover work and bulk approvals where
practical. The current PR should accurately describe shared UI foundations, compact lists and the
approved follow-ups. It must call out deferred validation, missing original certificate files,
remaining phases and limits of completed verification.

Current configured remotes are origin `Wynnwrath/AttendTav2` and upstream `rainyDayssss/AttendTa`.
The PR target for this delivery is `rainyDayssss/AttendTa:dev`, using the feature branch on the
origin fork. Creating the PR does not authorize merging or deploying it.
