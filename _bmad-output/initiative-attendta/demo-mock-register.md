# AttendTa demo data register

Decision: 2026-11-06 — demo rescope, approved by the project owner. Every piece of demo data
that stands in for a missing backend is listed here with the one ticket that removes it. The
initiative's Delivery section and `delivery-queue.md` give the phase order.

This file is the planning record. The code keeps the same ids in a comment on each demo
class, so a search for the id finds every line to delete.

## Rules every demo-phase ticket follows

1. **Real first.** A screen calls an existing service method wherever one returns the data or
   runs the command. Demo data covers only what no existing method provides. A demo provider
   may compose existing reads (for example the published feed plus the per-event registration
   detail) so real events, registrations and members appear.
2. **One switch, guarded at startup.** The configuration key `DemoData:Enabled` turns demo data
   on. It is off in every committed `appsettings*.json` and does not depend on
   `ASPNETCORE_ENVIRONMENT`. When it is on, the app reads the connected database's name at
   startup and refuses to start, with a clear log message, unless that name is `AttendTa_Demo`
   or exactly equals a second key, `DemoData:AllowDatabase` (for example `AttendTaMVP_Test` on
   a builder's machine). It also refuses to start when `DemoData:TokenKey` is missing (rule 10).
   Nothing about the guard appears on screen. With the switch off, the same screens show their
   normal empty state and an unavailable command returns the standard refusal outcome; nothing
   breaks the existing test suites.
3. **No visible sign.** Nothing rendered to a browser tells a viewer the data is not real: no
   banner, badge, ribbon, watermark, toast, footer line, tooltip, alt text, aria label, page
   title, download file name, console message or CSS class/id containing the words "demo",
   "mock", "sample", "fixture", "prototype", "simulated" or "test". Demo records use realistic
   names and values. Generated PDFs use the real snapshot template (`RenderSnapshotAsync`),
   never the watermarked sample render. Server-side logs may note at startup which register ids
   are active.
4. **No real writes.** A simulated command changes only the in-memory demo store, keyed by real
   record ids. It never inserts or updates SQL rows, never writes under `Common:SetupRoot`, and
   never appends to the Activity Log. Restarting the app resets the demo store's check-ins,
   decisions and issues; passes keep scanning (rule 10).
5. **Coherent flow.** One demo store serves every screen, so an action on one screen shows on
   the others for the rest of the session: check in a member, see the attendance in their
   history, complete the event, approve and issue the certificate, see it in the member's list
   and in the report. Start-up seeds scenarios on records already in the connected database so
   every demo screen opens populated.
6. **Never hides a real backend.** When a real implementation exists for a contract, its demo
   provider is not registered. A failing real service shows its real error; it never falls back
   to demo data.
7. **Overlay only named fields.** Demo state may change only these fields of an existing real
   read, and only for records a demo command changed: the check-in open flag on the officer event
   list, member feed and event detail (M-04). Nothing else from a real read is altered.
8. **Same refusals as the real command.** Every demo command reloads the real record's current
   state and applies the real ticket's refusal rules, so an invalid action fails on screen the
   way it will after integration:

   | Demo command | Allowed only when | Real rule |
   |---|---|---|
   | Open or close check-in | The event is Published (not unpublished, cancelled or completed) | 4.2 |
   | Check in (scan or manual) | The token or registration matches this event, check-in is open, the real registration status is Confirmed and its attendance is not invalidated; a repeat returns the original time | 4.3, 4.5 |
   | Invalidate attendance | A reason is given and the registration's certificate is not issued; a pending or approved review becomes not eligible with that reason | 4.6, 6.5 |
   | Approve or decline review | The review is pending and its attendance is valid; decline needs a reason | 6.6 |
   | Issue certificate | The review is approved, the event is Completed and no certificate is issued yet | 6.7 |

   A registration cancelled through the real service therefore stops its pass from scanning,
   and a cancelled event cannot be completed or opened for check-in.
9. **One folder, one registration method.** A demo provider implements the same service
   interface the real ticket will implement. All demo code lives in one WebApp folder. One
   registration method, behind the switch, calls one registration file per surface; each
   surface owns its file and adds only its own providers there. Removing a row means deleting
   its classes and its line in that surface's registration file.
10. **Stable, unguessable pass tokens.** A demo pass token is the HMAC-SHA256 of the real
    registration id under `DemoData:TokenKey`, a secret set only in the demo machine's
    environment. The same registration gets the same token after a restart, so printed or
    downloaded passes keep scanning, and nobody can derive a token without the key.
11. **Record it.** A ticket that adds demo data fills its row below in the same pull request
    (interface, classes, seeded scenarios). The one ticket named in "Removed by" deletes the
    classes and sets the row to `removed` with the commit. 8.11 verifies that no row is open.
12. **Hand-over between rows.** When a ticket removes a row whose data another open row still
    reads (for example 4.3 makes check-ins real while M-10a still derives reviews from them),
    the remaining demo provider reads the real source from that commit on. The removing ticket
    makes that switch in the same pull request.

## Register

Each row has exactly one removing ticket. A surface that several tickets make real is split
into lettered rows.

| Id | Surface | Missing backend today | Added by | Removed by | State |
|---|---|---|---|---|---|
| M-01 | Demo seam: `DemoData:Enabled`, the startup database guard, `DemoData:TokenKey`, the demo store (pass tokens and issued certificate documents; attendance and certificate reviews are real SQL rows after 6.6), the registration method and the start-up seed | — (infrastructure) | 1.14 | 8.13 | open |
| M-02 | My Events: all of a member's registrations across events, with status and cutoff labels | Cross-event owner query (only per-event and upcoming-only reads exist) | 3.17 | 3.13 | removed |
| M-03 | Pass card QR image and PNG download (rule 10 token, QR drawn by a locally served browser library) | QR library, token read, authorized PNG endpoint | 4.10 | 4.1 | removed |
| M-04 | Check-in window open/close command, the shared control partial, and the open flag overlaid on event reads (rule 7) | Open/close command | 1.14 | 4.2 | removed (7f6ae2218e6bc9e74611c610287b11ffa7f19ec4) |
| M-05 | Check-in by scan: outcome (success, repeat, invalid, closed) and the attendance record | Validation engine and attendance write | 4.11 | 4.3 | removed |
| M-06 | Manual lookup of confirmed participants by name or student number, and manual check-in | Registration-scoped search and manual check-in | 4.11 | 4.5 | removed (ff06123) |
| M-07 | Attendance invalidation with reason, and its effect on the review (rule 8) | Invalidation command | 4.11 | 4.6 | removed (6a82840) |
| M-08 | Member attendance history | Owner-scoped attendance query | 4.12 | 4.7 | removed (d3edd37) |
| M-09 | Event completion command and the Completed status overlaid on event reads (rule 7) | Completion command | 1.14 | 3.12 | removed |
| M-10a | Certificate reviews created at completion (a pending review for each valid check-in of a completed event) | Review creation hook | 6.12 | 6.4 | removed (cd227f694fdf71d10504db4406d13a3694941cbb) |
| M-10b | Review queue and the approve and decline decisions | Review queue and decisions | 6.12 | 6.6 | removed (8bbf587) |
| M-11a | Certificate issuance (number from the demo store, nothing retained) | Issuance pipeline and retained file | 6.12 | 6.7 | open |
| M-11b | Officer PDF view and download (rendered in memory with the real template) | Authorized download of the retained file | 6.12 | 6.8 | open |
| M-12 | Member certificate history and download links - `IMyCertificatesService`, `DemoMyCertificatesService`, `UnavailableMyCertificatesService`; shared `DemoCertificateDocuments` | Owner-scoped certificate query | 6.13 | 6.9 | open |
| M-13a | Report summary: status counts, Confirmed and Valid attendees, rate | Report summary queries | 7.4 | 7.1 | removed |
| M-13b | Report no-shows and course/year breakdowns | No-show and distribution queries | 7.4 | 7.2 | removed |
| M-13c | Report paged detail rows | Paged detail query | 7.4 | 7.3 | removed (04157b3) |
| M-14a | Account deactivation with reason (the Accounts list shows the new state) — `IAccountAccessCommands.DeactivateAsync`, `DemoAccountAccessCommands` | Account deactivation and cascade | 5.11 | 5.4 | removed |
| M-14b | Account reactivation with reason — `IAccountAccessCommands.ReactivateAsync`, `DemoAccountAccessCommands` | Account reactivation | 5.11 | 5.5 | removed |
| M-15a | Membership deactivation with reason (the Members list shows the new state) — `IMembershipAccessCommands.DeactivateAsync`, `DemoMembershipAccessCommands` | Membership deactivation and cascade | 5.11 | 5.6 | removed |
| M-15b | Membership reactivation with reason — `IMembershipAccessCommands.ReactivateAsync`, `DemoMembershipAccessCommands` | Membership reactivation | 5.11 | 5.7 | removed |

6.5 removes no row; its invalidation guard must be in place before 6.7 removes M-11a. 4.4
removes no row; it verifies the 4.11 camera against the real engine after 4.3.

A demo-phase ticket that finds another missing contract adds the next id (M-16, …) with its
removing ticket. When no ticket owns the real work, the builder names that gap in the pull
request and the row's "Removed by" says `8.13`.

## M-01, M-04 and M-09 — demo seam detail (1.14)

**Switch and guard.** `DemoData:Enabled` is `false` in the committed `appsettings.json` and is
read in every environment. `DemoDataStartupGuard.Validate` runs from `Startup.ConfigureServices`
immediately after the connection-string check, before any demo provider is registered. When the
switch is on it refuses to start unless `DemoData:TokenKey` is set and the connected database's
`DB_NAME()` is `AttendTa_Demo` or exactly equals `DemoData:AllowDatabase`; the reason goes to
stderr and the exception message names the database. Nothing about the guard is rendered.

**Contracts and classes.** All demo code is in `ASI.Basecode.WebApp/DemoData/`; the contracts are
in `ASI.Basecode.Services`.

| Piece | File |
|---|---|
| Options (`Enabled`, `AllowDatabase`, `TokenKey`) | `ASI.Basecode.WebApp/DemoData/DemoDataOptions.cs` |
| Startup guard | `ASI.Basecode.WebApp/DemoData/DemoDataStartupGuard.cs` |
| In-memory store and its records | `ASI.Basecode.WebApp/DemoData/DemoStore.cs` |
| Start-up seed (`IHostedService`) | `ASI.Basecode.WebApp/DemoData/DemoSeedService.cs` |
| One registration method | `ASI.Basecode.WebApp/DemoData/DemoDataRegistration.cs` |
| Shared check-in control and its model | `Views/Shared/_CheckInControl.cshtml`, `ASI.Basecode.Services/ServiceModels/CheckInControlViewModel.cs` |
| Operator pass-token switch | `ASI.Basecode.WebApp/Program.cs` (`--demo-pass-token <registrationId>`) |

**Store.** One singleton keyed by real ids. After 6.6 it holds only pass tokens and the still-demo
issued certificate documents (M-11a/M-11b); attendance and certificate reviews are real SQL rows.
The check-in window is no longer store state after 4.2. `GetPassToken` is the lowercase hex
HMAC-SHA256 of the invariant registration id under `DemoData:TokenKey`; the `--demo-pass-token`
switch prints the same value and writes nothing.

**Seeded starting state (for `demo-script.md`).** The seed composes `IEventRepository.GetPageAsync`
and `IRegistrationRepository.GetReviewQueueAsync(Confirmed)`, then writes the ready-to-complete
event's attendance and certificate reviews as real SQL rows inside one transaction. It never
overwrites an existing attendance row or decision, writes no stored file and appends no Activity Log
entry. The issued document stays in the demo store (M-11a).

- *Ready to complete* — the started published event with the most confirmed registrations (ties on
  the lowest event id). It stays Published in SQL so the demo completes it live through the real
  service. Up to eight confirmed registrations are checked in as real `attendance` rows, the last one
  is invalidated with "Pass could not be verified at the door." and its review is not eligible with
  that reason, the first remaining valid check-in has an approved review and a still-demo issued
  certificate (`AT-<year>-<registrationId:D6>`), the second is pending review, and the third is
  declined (not eligible with a review reason). Fewer registrations degrade in that order.
- *Check-in open* — removed by 4.2; the officer opens the real window for the next published event from the Event list.
- Fewer rows degrade gracefully: missing scenarios are skipped and the active register ids are
  logged at start-up. The seed writes only the attendance and certificate rows; it writes no stored
  file and no Activity Log entry.

**Screens, real source and demo source.**

| Screen | Real read / command | Demo source | Files |
|---|---|---|---|
| Officer event list | `IEventLifecycleService.GetPageAsync`; `OpenCheckIn`/`CloseCheckIn` POST (`IEventLifecycleService.SetCheckInWindowAsync`); `Complete` POST (`IEventLifecycleService.CompleteAsync`) | none — the real event row is authoritative | `Controllers/EventController.cs`, `Views/Event/Index.cshtml` |
| Member event feed | `IMemberEventService.GetFeedAsync` | none — the real `events.is_check_in_open` row is authoritative | `Controllers/MemberEventsController.cs`, `Views/MemberEvents/Index.cshtml` |
| Member event detail | `IMemberEventService.GetDetailAsync` | none — the real `events.is_check_in_open` row is authoritative | `Controllers/MemberEventsController.cs`, `Views/MemberEvents/Details.cshtml` |
| Check-in control | `IEventLifecycleService.SetCheckInWindowAsync` | none — posts the real command | `Views/Shared/_CheckInControl.cshtml` (reused by 3.16 and 4.11) |

Check-in state is the real `events.is_check_in_open` column; no overlay changes an event read. The
officer list renders the opener or closer name and Manila time from the real actor/time columns,
while the dossier and scanner keep the badge-only control. Refusal rules: open/close check-in only
on a Published event. Completion is the real `EventLifecycleService.CompleteAsync` (Management only,
Published only, at or after the start) and the Completed label comes from SQL, so the member feed
and detail read Published or Completed events and no overlay hides the real completion.

**M-04 removal (4.2).** 4.2 replaced the demo command and overlay with the real
`IEventLifecycleService.SetCheckInWindowAsync`. It locks the event, refuses draft, cancelled and
completed events, refuses an already-open or already-closed window, reports `Stale` when the status
changed before the lock and `Invalid` when the flag changed, then sets `is_check_in_open` and the
opener or closer actor/time columns and appends one `event.check_in_opened` or
`event.check_in_closed` row in the same transaction. `IEventStateCommands`, `IEventStateOverlay`,
their demo and switch-off providers and `DemoEventsRegistration` are deleted. The officer list
renders "Opened by <name> · <Manila time>" or "Closed by <name> · <Manila time>"; the dossier and
scanner keep their badge-only control. Hand-over (register rule 12): `DemoCheckInService` and
`DemoCheckInLookupService` remained the M-05/M-06 providers until 4.3 and 4.5, reading
`events.is_check_in_open` from the real event projection, and the seed no longer opens a window.
Both are now removed: 4.3 and 4.5 replaced them with the real services.

## M-14a, M-14b, M-15a and M-15b — account and membership access detail (5.11)

**M-14a, M-14b, M-15a and M-15b are removed.** 5.4 deleted the demo account deactivation, 5.5 the
demo account reactivation, 5.6 the demo membership deactivation, and 5.7 the demo membership
reactivation. `DemoAccountAccessCommands`, `DemoMembershipAccessCommands`, `UnavailableAccessCommands`,
the access overlay and its registration file no longer exist. Both `IAccountAccessCommands` commands
are the real `AccountAccessService`, and both `IMembershipAccessCommands` commands are the real
`MembershipAccessService`. The account list and its access dialog therefore read the real `users` row
with no overlay, and the Members list shows the real `members.membership_status_id` row after either
command.

**Contracts and classes.** The contracts are in `ASI.Basecode.Services`; no access demo code remains
in `ASI.Basecode.WebApp/DemoData/`.

| Piece | File |
|---|---|
| Account command contract `IAccountAccessCommands` (`DeactivateAsync`, `ReactivateAsync`) | `ASI.Basecode.Services/Interfaces/IAccountAccessCommands.cs` |
| Membership command contract `IMembershipAccessCommands` (`DeactivateAsync`, `ReactivateAsync`) | `ASI.Basecode.Services/Interfaces/IMembershipAccessCommands.cs` |
| Dialog composition `IAccessDialogService` / `AccessDialogService` (real read + real 5.3 preview + last-admin count) | `ASI.Basecode.Services/Interfaces/IAccessDialogService.cs`, `ASI.Basecode.Services/Services/AccessDialogService.cs` |
| Real account deactivation and reactivation (M-14a removed by 5.4, M-14b by 5.5) | `ASI.Basecode.Services/Services/AccountAccessService.cs` |
| Real membership deactivation and reactivation (M-15a removed by 5.6, M-15b by 5.7) | `ASI.Basecode.Services/Services/MembershipAccessService.cs` |

**Source and store use.** Account access is the real `AccountAccessService` for both commands. Each
runs in one transaction that locks the `users` row and appends one audit row: deactivation also takes
the last-administrator application lock and cascades through `IRegistrationCancellationService`,
while reactivation restores login access only and touches no registration, seat, QR token or
certificate. Membership access is the real `MembershipAccessService` for both commands. Deactivation
locks the member's `users` row, sets `members.membership_status_id` to inactive, cancels the member's
upcoming registrations through `IRegistrationCancellationService` with the `MembershipDeactivated`
cause, and appends one `member.membership_deactivated` audit row; reactivation locks the same row,
sets the status active, and appends one `member.membership_reactivated` audit row while restoring
eligibility only. Neither takes the last-administrator lock or changes `users.is_active`, so the
linked account keeps signing in. A blank reason is refused before any state changes. Deactivating the
last active Administrator is refused by the real account command from
`IAccountAdministrationRepository.CountActiveAdministratorsAsync`.

**Seeded starting state.** None. Both lists open on the real rows.

**Screens, real source and demo source.**

| Screen | Real read / command | Demo source | Files |
|---|---|---|---|
| Accounts list | `IAccountAdministrationService.GetPageAsync`; `Deactivate`/`Reactivate` POST | none — the real row is authoritative | `Controllers/AdminAccountsController.cs`, `Views/AdminAccounts/Index.cshtml`, `_AccessActions.cshtml`, `_AccessDialog.cshtml` |
| Members list | `IMemberService.GetPageAsync`; `Deactivate`/`Reactivate` POST | none — the real row is authoritative | `Controllers/MemberController.cs`, `Views/Member/Index.cshtml`, `_AccessActions.cshtml`, `_AccessDialog.cshtml` |

Account access stays separate from membership eligibility: deactivating an account never changes
the member's status, and changing a membership never changes the linked account.

## M-02 — demo seam detail (3.17, removed by 3.13)

**Removed.** 3.13 deleted the demo and switch-off providers and their surface registration file.
The tab now reads `IMyEventsService.GetRegistrationsAsync`, implemented by the real
`MyEventsService` over `IRegistrationRepository.CountMyRegistrationsAsync` and
`GetMyRegistrationsAsync`: one owner-scoped cross-event query ordered newest start first and paged
at 25 in SQL. It includes every stored status, including Cancelled and registrations on
non-published events, resolves the label and badge through `RegistrationStatusDisplay`, and shows
the "View event" link only for a published event. Nothing is written.

## M-03 — pass card QR image and PNG download detail (4.10)

**Contract and classes.** The contract is in `ASI.Basecode.Services`; all demo code is in
`ASI.Basecode.WebApp/DemoData/`.

| Piece | File |
|---|---|
| Pass contract `IPassService` (`GetMyPassesAsync`) | `ASI.Basecode.Services/Interfaces/IPassService.cs` |
| Screen model `PassesViewModel` (`Active`, `Past`, `Empty()`) | `ASI.Basecode.Services/ServiceModels/PassesViewModel.cs` |
| Card model `PassViewModel` (event metadata, holder, rule-10 token, download name) | `ASI.Basecode.Services/ServiceModels/MyEventsViewModels.cs` |
| Demo passes | `ASI.Basecode.WebApp/DemoData/DemoPassService.cs` |
| Switch-off passes (empty state) | `ASI.Basecode.WebApp/DemoData/UnavailablePassService.cs` |
| Passes surface registration file | `ASI.Basecode.WebApp/DemoData/DemoPassesRegistration.cs` |
| Screen and card | `Controllers/PassesController.cs`, `Views/Passes/Index.cshtml`, `Views/MyEvents/_PassPartial.cshtml` |
| QR library and browser drawing | `wwwroot/lib/qrcode/qrcode.js` (MIT, 1.4.4) with `LICENSE`, `wwwroot/js/features/passes.js` |

**Source and composition.** `DemoPassService` composes
`IRegistrationRepository.DiscoverOpenEventIdsForUserAsync` for the actor's non-cancelled event ids,
`IRegistrationRepository.GetMemberRegistrationAsync` for the actor's own row per event,
`IEventRepository.GetPublishedEventAsync` for the title, type, venue and start, and
`IMemberRepository.GetMemberRecordByUserIdAsync` for the holder name and student number. It keeps
only Confirmed registrations whose event is published, splits them on `TimeProvider` (event start
passed means Past), takes the rule-10 token from `DemoStore.GetPassToken`, and never writes. Pending,
waitlisted and cancelled registrations, and cancelled or unpublished events, produce no card. The QR
encodes only the 64-character token; the browser draws it with a 4-module quiet zone and saves it as
PNG. Nothing rendered names the data as demo data, including the download file name.

**Removal (4.1).** 4.1 replaced the demo provider behind the same `IPassService` contract with the
real owner-scoped `PassService` over `IRegistrationRepository.GetMyPassRowsAsync`, and replaced
`QrToken` with the server-rendered `QrImageUrl` and the authorized `DownloadUrl`. It deleted
`DemoPassService`, `UnavailablePassService`, `DemoPassesRegistration`, `wwwroot/lib/qrcode/` and
`wwwroot/js/features/passes.js`, so `IPassService` is the real service in both switch states. The QR
PNG is rendered in `WebApp/Rendering/QrPassPngRenderer` with `Net.Codecrete.QrCodeGenerator` and
streamed from `Passes/Qr/{id}` (the same GET serves the inline image and `?download=true`). Every
unavailable request is one 403 with `This pass is unavailable.` The demo scanner now matches the
real `registrations.qr_token` through `FindConfirmedRegistrationIdByTokenAsync` (register rule 12).
The M-01 `--demo-pass-token` switch and `DemoStore.GetPassToken` remain until 8.13 removes the store.

## M-05, M-06 and M-07 — door scanner detail (4.11)

**Contracts and classes.** The contracts are in `ASI.Basecode.Services`; all demo code is in
`ASI.Basecode.WebApp/DemoData/`.

| Piece | File |
|---|---|
| Scan contract `ICheckInService` (`GetScreenAsync`, `ScanAsync`) | `ASI.Basecode.Services/Interfaces/ICheckInService.cs` |
| Lookup contract `ICheckInLookupService` (`SearchAsync`, `CheckInAsync`) | `ASI.Basecode.Services/Interfaces/ICheckInLookupService.cs` |
| Invalidation contract `IAttendanceInvalidationService` | `ASI.Basecode.Services/Interfaces/IAttendanceInvalidationService.cs` |
| Screen, outcome, lookup and log models | `ASI.Basecode.Services/ServiceModels/CheckInScreenViewModel.cs` |
| Real scan provider (M-05 removed by 4.3) | `ASI.Basecode.Services/Services/CheckInService.cs`, `ASI.Basecode.Services/Services/AttendanceRecordingService.cs`, `ASI.Basecode.Data/Repositories/AttendanceRepository.cs` |
| Real lookup and manual check-in provider (M-06 removed by 4.5) | `ASI.Basecode.Services/Services/CheckInLookupService.cs`, `ASI.Basecode.Data/Repositories/RegistrationRepository.cs` |
| Real invalidation provider (M-07 removed by 4.6) | `ASI.Basecode.Services/Services/AttendanceInvalidationService.cs`, `ASI.Basecode.Services/Interfaces/IAttendanceInvalidationHandler.cs`, `ASI.Basecode.Data/Repositories/AttendanceRepository.cs` |
| Store additions | `DemoStore.GetAttendanceForEvent`, `DemoAttendance.MemberName`/`StudentNumber` |
| Screen, adapters and decoder | `Controllers/CheckInController.cs`, `Views/CheckIn/Index.cshtml`, `wwwroot/js/features/scanner.js`, `scanner-camera.js`, `scanner-manual.js`, `wwwroot/lib/jsqr/jsQR.js` (Apache-2.0, 1.4.0) |

**What it composed (4.11).** The demo screen read the published event page, selected the first event
with an open window, and read the store's attendance for the log. A scan matched the rule-10 token of
a confirmed registration of the event, reloaded `IsConfirmedAsync`, and returned `success`, `repeat`
(the original time), `invalid` (unknown, wrong-event, cancelled or invalidated) or `closed` (not
published, completed or window closed). The manual lookup read the event's confirmed rows (bounded
at 500), filtered by name or student number in memory, and paged at 25; the explicit Check in recorded
`student_number` when the query equals the student number, otherwise `manual_search`. Invalidation
requires a reason, checks that the registration belongs to the event, refuses an issued certificate,
and the real `CertificateReviewInvalidationHandler` leaves a pending or approved review not eligible
with that reason. Searching or viewing never records. The manual lookup is real from 4.5; invalidation
was the demo store until 4.6.

**Scanner follow-up (#138, after PR #170).** Form-post scan and manual check-in results now
survive the redirect as an event-scoped, server-rendered outcome panel (identity, student number,
original time and refusal text), including without JavaScript. The camera adapter discards and
stops streams from permission requests superseded by a mode change or page exit. Lookup table
text uses an explicit dark foreground on its white rows. M-05/M-06/M-07 retain the same contracts,
providers, shared store and removal owners; these changes add no demo state or real writes.

**Removal (4.3, 4.5, 4.6).** 4.3 replaced `ICheckInService` with the real `CheckInService` and the
shared `AttendanceRecordingService`, deleted `DemoCheckInService` and `UnavailableCheckInService`, and
removed their registration lines. A scan now writes one real `attendance` row plus one
`attendance.recorded` audit row in the caller-owned transaction; the screen keeps its shape and reads
the real recent log. 4.5 (commit `ff06123`) replaced `ICheckInLookupService` with the real
`CheckInLookupService`, deleted `DemoCheckInLookupService` and `UnavailableCheckInLookupService`, and
removed their registration lines, so the real service serves both switch states. The search is a
read-only, registration-scoped SQL read over the selected event's Confirmed registrations matching a
name or student-number substring (`Contains` under the database collation), ordered by member name
then registration id and paged at 25; the explicit Check in pre-checks the published event and open
window, reads the row's student number to choose `student_number` or `manual_search`, and delegates
the validation and write to `AttendanceRecordingService`. 4.6 replaced
`IAttendanceInvalidationService` with the real `AttendanceInvalidationService`, deleted
`DemoAttendanceInvalidationService`, `UnavailableAttendanceInvalidationService` and
`DemoCheckInRegistration`, and removed their registration lines, so the screen keeps its shape in
both switch states. Invalidation now runs one transaction that locks the participant's `users` row,
the event and the attendance row, validates the trimmed reason (1–500 characters), invokes every
`IAttendanceInvalidationHandler` before the write, stamps `invalidated_at`, `invalidated_by` and
`invalidation_reason` on the retained row and appends one `attendance.invalidated` entry with the
reason; a repeat invalidation is refused, so the first reason and its audit entry survive, and the
registration status never changes. M-10a is removed by 6.4's real completion hook, which creates
reviews from real attendance.


## M-08 — demo seam detail (4.12, removed by 4.7)

**Removed.** 4.7 replaced the empty `MyEventsService.GetAttendanceAsync` page with the real
owner-scoped attendance read behind the same `IMyEventsService` contract, so the tab, its partial,
pager and controller action did not change. The read is
`IAttendanceRepository.CountMyAttendanceAsync` plus `GetMyAttendanceAsync`: one owner-scoped
cross-event query joining `attendance → registrations → members` and the event, filtered by
`members.user_id`, ordered `recorded_at DESC, attendance_id DESC` and paged at 25 in SQL. It
returns every owned attendance row, including an invalidated one with its stored reason, and the
method label comes from `CheckInMethodDisplay`. Nothing is written.

| Piece | File |
|---|---|
| Read contract `IMyEventsService.GetAttendanceAsync` | `ASI.Basecode.Services/Interfaces/IMyEventsService.cs` |
| Method label rule `CheckInMethodDisplay` | `ASI.Basecode.Services/ServiceModels/CheckInMethodDisplay.cs` |
| Real provider | `ASI.Basecode.Services/Services/MyEventsService.cs`, `ASI.Basecode.Data/Repositories/AttendanceRepository.cs` |

**Hand-over (register rule 12).** 3.13 removed M-02's `DemoMyEventsService`,
`EmptyMyEventsService` and `DemoMyEventsRegistration`, and kept `GetAttendanceAsync` on the real
`MyEventsService` returning an empty page. 4.7 replaced that method with the real owner-scoped
query; no demo class, provider or registration line remained to delete, so the row needed no
code removal beyond the method body. Removed in `d3edd37`; the sha is recorded here in the
follow-up docs commit, as 4.6 did for M-07.

## M-13a, M-13b and M-13c — participation report detail (7.4)

**All three rows are removed.** 7.1 replaced the summary computation (M-13a), 7.2 the no-show and
breakdown computation (M-13b), and 7.3 the paged detail rows (M-13c) with the real
`ParticipationReportService` and `ParticipationReportRepository`. 7.3 deleted the demo overlay
provider, its registration file and its tests, so the real service answers the report in both demo
switch states.

**Contract and classes.** The demo code lived in `ASI.Basecode.WebApp/DemoData/`; the contract is in
`ASI.Basecode.Services`.

| Piece | File |
|---|---|
| Read contract `IParticipationReportService.GetReportAsync(eventId, page, ct)` | `ASI.Basecode.Services/Interfaces/IParticipationReportService.cs` |
| View model, row model, breakdown model and paging metadata | `ASI.Basecode.Services/ServiceModels/ParticipationReportViewModel.cs` |
| Real report service (selector, labels, paging and mapping) | `ASI.Basecode.Services/Services/ParticipationReportService.cs` |
| Real report reads (summary, no-shows, distributions and paged detail) | `ASI.Basecode.Data/Repositories/ParticipationReportRepository.cs` |
| Screen and styles | `Views/Reports/Participation.cshtml`, `Styles/features/reports.css` |

**Real report.** `ParticipationReportService` reads the selector options and selected event through
`IEventRepository`, then reads the event summary, no-shows, attendee distribution and detail count
from `ParticipationReportRepository`. `GetSummaryFactsAsync` composes `ParticipationFacts.SeatHolders`,
`ValidAttendances` and `AttendanceRates`; `GetNoShowCountAsync` counts `ParticipationFacts.NoShows`
(Completed-only, so a cancelled event returns zero); `GetAttendeeDistributionAsync` groups valid
attendees by `registrations.course_snapshot` and `year_level_snapshot` without joining `users` or
`members`, so inactive history stays visible; and `CountDetailRowsAsync`/`GetDetailPageAsync` count
and page every registration of the event. All reads use `AsNoTracking`; nothing is written. There is
no demo report provider and no empty provider.

- *M-13a summary* — served from the SQL dataset: status counts, Confirmed, Valid attendees and the
  unrounded Valid/Confirmed rate, N/A at zero Confirmed, "Not final" until the event is Completed,
  and N/A for a cancelled event.
- *M-13b no-shows and breakdowns* — removed by 7.2. `GetNoShowCountAsync` counts Confirmed
  registrations without valid attendance on a Completed event; pending, waitlisted and cancelled
  rows are excluded, and a cancelled event shows no figure. `GetAttendeeDistributionAsync` groups
  valid attendees by their registration signup snapshot, including inactive members, and the service
  labels, orders and percentages each group's share of the valid attendees.
- *M-13c detail rows* — removed by 7.3. `GetDetailPageAsync` pages the event's registrations in SQL,
  ordered by participant name then `registration_id` so page boundaries are stable, 25 per page.
  Each row carries name, student number, snapshot course and year, the registration status
  label/badge, the attendance label/badge (Checked in, Invalidated, No-show, Not checked in, and "—"
  for a non-Confirmed row with no attendance) and the stored UTC check-in time, which the view
  formats for Asia/Manila. Attendance is left-joined because `attendance.registration_id` is UNIQUE,
  so a null check-in time is the only "no attendance row" signal. The count is the pager's total, so
  the summary, no-shows and breakdowns stay full-dataset on every page.

**Seeded starting state.** The ready-to-complete event seeded by M-01 still seeds up to eight
demo-store check-ins (the last one invalidated) for the certificate-ledger demo. The report no longer
reads them: it shows real SQL attendance only, so it opens with the not-final markers before the
demo completes the event and reflects only the check-ins recorded through the real scanner
afterwards.

**Removal.** 7.1 replaced the summary computation (M-13a) and 7.2 the no-show and breakdown
computation (M-13b). 7.3 replaced the paged detail read (M-13c) in `04157b3` and deleted
`DemoParticipationReportService.cs`, `DemoReportsRegistration.cs` and
`DemoParticipationReportOverlayTests.cs`. The screen did not change visibly.

**Hand-over (rule 12).** 4.3 (real check-in) and 4.6 (real invalidation) landed before 7.3, so the
detail rows read the real attendance and invalidation source. 7.3 then removed the demo overlay
entirely: the report matches recorded attendance, and the seeded demo-store check-ins stay for the
certificate-ledger demo only.

## M-10a, M-10b, M-11a and M-11b — certificate ledger (6.12)

**Contract and registration.** `ASI.Basecode.Services/Interfaces/ICertificateLedgerService.cs`
defines `GetPageAsync`, `DecideAsync`, `IssueAsync`, `GetViewerAsync` and `GetPdfAsync`. The real
`CertificateLedgerService` is registered before the demo seam and serves both switch states.
`DemoData/DemoCertificatesRegistration.cs` replaces that registration with
`DemoCertificateLedgerService` only when the central switch is enabled, so the demo provider overlays
the still-demo issued state over the real queue. The unavailable ledger stub is retired; only the
M-12 member-history stub remains for the switch-off state.

| Row | Implementation and handoff |
|---|---|
| M-10a / removed by 6.4 | `DemoStore.MarkCompleted` creates pending reviews for every valid stored check-in through `EnsureCertificateReviews`; the query also ensures reviews for real completed events. Existing decisions are never overwritten. Replace the creation call/adapter with the completion hook and real attendance facts. |
| M-10b / removed by 6.6 | The real `CertificateLedgerService` reads the selected completed event's confirmed registrations, attendance and certificate rows and pages the queue in memory at 25. `DecideAsync` locks the participant's `users` row and the event, rechecks `pending_approval` and Valid Attendance, writes `eligible` or `not_eligible` with the trimmed reason, and appends one `certificate.approved` or `certificate.declined` audit entry in the same transaction. A changed candidate is `Stale`; a missing review is `NotFound`; a blank reason is `Invalid` on `reason`. The demo provider delegates both the queue and the decisions to it and overlays only the still-demo issued state. |
| M-11a / removed by 6.7 | `IssueAsync` requires completion, confirmation, valid attendance and approval, creates a unique `AT-<year>-<registrationId:D6>` number and snapshot, calls the real `RenderSnapshotAsync`, rechecks the real review, attendance and event state before and after rendering, then publishes once through `DemoStore.PublishCertificate`; the store gate guarantees one document per registration. After publication the SQL review is marked `issued` with the issuer, time, number and snapshots, so the real invalidation handler refuses the certificate. Render failure publishes nothing. No private storage or audit write occurs. |
| M-11b / removed by 6.8 | `GetViewerAsync`/`GetPdfAsync` require Management and issued state; the controller streams `application/pdf` with no-store caching and an optional attachment name. A seeded issue renders its captured snapshot on first download, then reuses those bytes. No caller-supplied path is accepted. Replace with authorized retained-file retrieval, not current-data regeneration. |

**M-10a removal (6.4).** 6.4 replaced the demo review creation with the real
`CertificateReviewCompletionHandler`, registered as an `IEventCompletionHandler`. Inside the
caller-owned completion transaction it inserts one `pending_approval` row per registration with
Valid Attendance, stamped with the completing officer and the completion time, and skips a
registration that already carries a certificate row.

**M-10b removal (6.6) and seed hand-over.** 6.6 moved the queue and both decisions to the real
service and deleted `DemoStore.DecideCertificate`. The start-up seed now writes the ready-to-complete
event's attendance and certificate reviews as real SQL rows, so the real completion hook finds them
and the real decision command updates them. Existing attendance rows and decisions are never
overwritten, so a restart keeps them. The issued attendee's snapshot, number, issue time and PDF
bytes stay in the same `DemoStore` issue record used by 6.13; the issued document itself is still
demo state until 6.7 and is re-seeded at start-up only when the real review is approved. Existing
invalidated reviews remain visible as not eligible with their reason. Later SQL edits do not rewrite
issued snapshot values or rendered bytes.

**Presentation ownership.** `Controllers/CertificateLedgerController.cs`,
`Views/CertificateLedger/Index.cshtml`, `Views/CertificateLedger/Viewer.cshtml`,
`Styles/features/certificate-ledger.css` and `wwwroot/js/features/certificate-ledger.js`.
The selector, pager, shared shell and member-history slot remain unchanged. There is no bulk action.

## M-12 — member certificate history (6.13)

**Contract and registration.** `ASI.Basecode.Services/Interfaces/IMyCertificatesService.cs`
provides `GetPageAsync` and `GetPdfAsync`. `DemoCertificatesRegistration.cs` uses `TryAddScoped`
to register `DemoMyCertificatesService` only with the central switch on, preserving a real
provider registered first. With the switch off, `UnavailableMyCertificatesService` returns an
empty page and the same not-found refusal for documents.

**Read composition and states.** The provider composes the actor's real non-cancelled event ids,
`IRegistrationRepository.GetMemberRegistrationAsync`, and `IEventRepository.CountAsync` /
`GetPageAsync` for event titles and schedules. Confirmed registrations are joined to the real SQL
certificate reviews through `ICertificateRepository.GetReviewQueueAsync`, and the still-demo issued
document is overlaid from the same `DemoStore` issue record that 6.12 updates. Pending, eligible,
declined (with reason), issued, and unavailable (no review or missing issued snapshot) are displayed.
Ordering is event start descending then registration id descending, at the shared page size of 25.
The existing My Events certificate action and pager remain independent of Registrations and
Attendance.

**Documents and ownership.** Every member PDF request reloads `DiscoverReviewAsync` and compares
its real `UserId` to `ICurrentActor.UserId` before accessing an issued snapshot. Unknown ids and
other owners receive the same not-found response. The Member policy retains the normal per-request
active-account cookie validation; membership status does not block historical access.
`DemoCertificateDocuments` is the one shared snapshot render/cache path for ledger and member reads:
it calls the real `RenderSnapshotAsync`, attaches seeded bytes in process memory once, and returns
the original bytes thereafter. View streams inline `application/pdf`; Download uses an attachment
name based on the certificate number. Responses use no-store caching. No SQL, private-file or
Activity Log writes occur.

**Seed and removal.** No additional seed or store is introduced. M-01 / 6.12's issued, pending,
declined and invalidated attendee scenarios appear only to their actual owners; officer approval
and issuance are reflected on the next member read. A confirmed registration without a review
shows unavailable, and a member without confirmed registrations gets the empty state. 6.9 replaces
the member provider with its real owner-scoped query and retained-document access, deletes the
switch-off member provider and registration lines, and marks M-12 removed. When 6.8 replaces the
officer document adapter first, M-12 must be handed over to the retained-document source rather
than continuing to regenerate documents (register rule 12).

## Known limits of the demo

- Account deactivation and reactivation are both the real command, so a deactivated session's next
  request is refused and a reactivated account can sign in again.
- Deactivation applies the real last-active-Administrator rule; reactivation restores login access
  only and touches no registration, seat, QR token or certificate.
- Membership deactivation and reactivation are both the real command: deactivation writes
  `members.membership_status_id`, cancels the member's upcoming registrations and appends
  `member.membership_deactivated`, while reactivation restores eligibility only and appends
  `member.membership_reactivated`. The linked account keeps signing in in both cases, and cancelled
  registrations stay Cancelled.
- Review decisions, check-ins and the seeded demo attendance are real SQL rows and survive a restart;
  only the issued certificate document (M-11a/M-11b) is process memory and is re-seeded at start-up
  when its review is approved. Passes keep scanning.
- A pass scanned in the demo is matched against the real `registrations.qr_token`, the same value
  the server-rendered PNG encodes, so a cancelled registration's cleared token stops scanning.
