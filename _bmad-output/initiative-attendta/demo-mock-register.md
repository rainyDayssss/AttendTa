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
   read, and only for records a demo command changed: the event status shown as Completed and
   the check-in open flag on the officer event list, member feed and event detail (M-04, M-09).
   Nothing else from a real read is altered.
8. **Same refusals as the real command.** Every demo command reloads the real record's current
   state and applies the real ticket's refusal rules, so an invalid action fails on screen the
   way it will after integration:

   | Demo command | Allowed only when | Real rule |
   |---|---|---|
   | Open or close check-in | The event is Published (not unpublished, cancelled or completed) | 4.2 |
   | Complete event | The event is Published, not cancelled, and its start time has passed | 3.12 |
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
| M-01 | Demo seam: `DemoData:Enabled`, the startup database guard, `DemoData:TokenKey`, the demo store (event check-in and completed state, pass tokens, attendance, certificate reviews and issues), the registration method and the start-up seed | — (infrastructure) | 1.14 | 8.13 | open |
| M-02 | My Events: all of a member's registrations across events, with status and cutoff labels | Cross-event owner query (only per-event and upcoming-only reads exist) | 3.17 | 3.13 | removed |
| M-03 | Pass card QR image and PNG download (rule 10 token, QR drawn by a locally served browser library) | QR library, token read, authorized PNG endpoint | 4.10 | 4.1 | removed |
| M-04 | Check-in window open/close command, the shared control partial, and the open flag overlaid on event reads (rule 7) | Open/close command | 1.14 | 4.2 | open |
| M-05 | Check-in by scan: outcome (success, repeat, invalid, closed) and the attendance record | Validation engine and attendance write | 4.11 | 4.3 | open |
| M-06 | Manual lookup of confirmed participants by name or student number, and manual check-in | Registration-scoped search and manual check-in | 4.11 | 4.5 | open |
| M-07 | Attendance invalidation with reason, and its effect on the review (rule 8) | Invalidation command | 4.11 | 4.6 | open |
| M-08 | Member attendance history | Owner-scoped attendance query | 4.12 | 4.7 | open |
| M-09 | Event completion command and the Completed status overlaid on event reads (rule 7) | Completion command | 1.14 | 3.12 | open |
| M-10a | Certificate reviews created at completion (a pending review for each valid check-in of a completed event) | Review creation hook | 6.12 | 6.4 | open |
| M-10b | Review queue and the approve and decline decisions | Review queue and decisions | 6.12 | 6.6 | open |
| M-11a | Certificate issuance (number from the demo store, nothing retained) | Issuance pipeline and retained file | 6.12 | 6.7 | open |
| M-11b | Officer PDF view and download (rendered in memory with the real template) | Authorized download of the retained file | 6.12 | 6.8 | open |
| M-12 | Member certificate history and download links - `IMyCertificatesService`, `DemoMyCertificatesService`, `UnavailableMyCertificatesService`; shared `DemoCertificateDocuments` | Owner-scoped certificate query | 6.13 | 6.9 | open |
| M-13a | Report summary: status counts, Confirmed and Valid attendees, rate | Report summary queries | 7.4 | 7.1 | removed; demo attendance source remains under M-05/M-07 |
| M-13b | Report no-shows and course/year breakdowns | No-show and distribution queries | 7.4 | 7.2 | open |
| M-13c | Report paged detail rows | Paged detail query | 7.4 | 7.3 | open |
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
| Command contract `IEventStateCommands` (`OpenCheckInAsync`, `CloseCheckInAsync`, `CompleteAsync`) | `ASI.Basecode.Services/Interfaces/IEventStateCommands.cs` |
| Overlay contract `IEventStateOverlay` | `ASI.Basecode.WebApp/DemoData/IEventStateOverlay.cs` |
| Options (`Enabled`, `AllowDatabase`, `TokenKey`) | `ASI.Basecode.WebApp/DemoData/DemoDataOptions.cs` |
| Startup guard | `ASI.Basecode.WebApp/DemoData/DemoDataStartupGuard.cs` |
| In-memory store and its records | `ASI.Basecode.WebApp/DemoData/DemoStore.cs` |
| Start-up seed (`IHostedService`) | `ASI.Basecode.WebApp/DemoData/DemoSeedService.cs` |
| Demo commands | `ASI.Basecode.WebApp/DemoData/DemoEventStateCommands.cs` |
| Switch-off commands (standard refusal) | `ASI.Basecode.WebApp/DemoData/UnavailableEventStateCommands.cs` |
| Demo overlay | `ASI.Basecode.WebApp/DemoData/DemoEventStateOverlay.cs` |
| Switch-off overlay | `ASI.Basecode.WebApp/DemoData/NoOpEventStateOverlay.cs` |
| One registration method | `ASI.Basecode.WebApp/DemoData/DemoDataRegistration.cs` |
| Events surface registration file | `ASI.Basecode.WebApp/DemoData/DemoEventsRegistration.cs` |
| Shared check-in control and its model | `Views/Shared/_CheckInControl.cshtml`, `ASI.Basecode.Services/ServiceModels/CheckInControlViewModel.cs` |
| Operator pass-token switch | `ASI.Basecode.WebApp/Program.cs` (`--demo-pass-token <registrationId>`) |

**Store.** One singleton keyed by real ids: check-in-open event ids, completed event ids,
attendance by registration id, and certificate review and issue state. `InvalidateAttendance` requires a reason, is refused once a certificate is
issued, and turns a pending or approved review not eligible with that reason. `GetPassToken` is
the lowercase hex HMAC-SHA256 of the invariant registration id under `DemoData:TokenKey`; the
`--demo-pass-token` switch prints the same value and writes nothing.

**Seeded starting state (for `demo-script.md`).** The seed composes `IEventRepository.GetPageAsync`
and `IRegistrationRepository.GetReviewQueueAsync(Confirmed)`; it never writes.

- *Completed event* — the started published event with the most confirmed registrations (ties on
  the lowest event id). Check-in is closed, up to eight confirmed registrations are checked in,
  the last one is invalidated with "Pass could not be verified at the door." and its review is not
  eligible with that reason, the first remaining valid check-in has an issued certificate
  (`AT-<year>-<registrationId:D6>`), the second is pending review, and the third is declined (not
  eligible with a review reason). Fewer registrations degrade in that order.
- *Ready to complete* — the next started published event, unchanged in SQL.
- *Check-in open* — the next published event not used above, with the store's check-in window open.
- Fewer rows degrade gracefully: missing scenarios are skipped and the active register ids are
  logged at start-up. No SQL row, stored file or Activity Log entry is written.

**Screens, real source and demo source.**

| Screen | Real read / command | Demo source | Files |
|---|---|---|---|
| Officer event list | `IEventLifecycleService.GetPageAsync`; `OpenCheckIn`/`CloseCheckIn`/`Complete` POST | `IEventStateOverlay` after the read; `IEventStateCommands` | `Controllers/EventController.cs`, `Views/Event/Index.cshtml` |
| Member event feed | `IMemberEventService.GetFeedAsync` | `IEventStateOverlay` after the read | `Controllers/MemberEventsController.cs`, `Views/MemberEvents/Index.cshtml` |
| Member event detail | `IMemberEventService.GetDetailAsync` | `IEventStateOverlay` after the read | `Controllers/MemberEventsController.cs`, `Views/MemberEvents/Details.cshtml` |
| Check-in control | — | Posts to `IEventStateCommands` | `Views/Shared/_CheckInControl.cshtml` (reused by 3.16 and 4.11) |

The overlay changes only `EventStatusId` (shown as Completed) and `IsCheckInOpen`, and only for
ids the store marks. Real reads run first; a failing real read is never replaced by demo data.
Refusal rules: open/close check-in only on a Published event that is not demo-completed; complete
only on a Published event that is not demo-completed and whose start time has passed.

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
| Demo scan provider | `ASI.Basecode.WebApp/DemoData/DemoCheckInService.cs` |
| Demo lookup and manual check-in provider | `ASI.Basecode.WebApp/DemoData/DemoCheckInLookupService.cs` |
| Demo invalidation provider | `ASI.Basecode.WebApp/DemoData/DemoAttendanceInvalidationService.cs` |
| Switch-off providers | `UnavailableCheckInService.cs`, `UnavailableCheckInLookupService.cs`, `UnavailableAttendanceInvalidationService.cs` |
| Check-in surface registration file | `ASI.Basecode.WebApp/DemoData/DemoCheckInRegistration.cs` |
| Store additions | `DemoStore.GetAttendanceForEvent`, `DemoAttendance.MemberName`/`StudentNumber` |
| Screen, adapters and decoder | `Controllers/CheckInController.cs`, `Views/CheckIn/Index.cshtml`, `wwwroot/js/features/scanner.js`, `scanner-camera.js`, `scanner-manual.js`, `wwwroot/lib/jsqr/jsQR.js` (Apache-2.0, 1.4.0) |

**What it composes.** The screen reads the published event page, selects the first event with an
open window, and reads the store's attendance for the log. A scan matches the rule-10 token of a
confirmed registration of the event, reloads `IsConfirmedAsync`, and returns `success`, `repeat`
(the original time), `invalid` (unknown, wrong-event, cancelled or invalidated) or `closed` (not
published, completed or window closed). The manual lookup reads the event's confirmed rows (bounded
at 500), filters by name or student number in memory, and pages at 25; the explicit Check in records
`student_number` when the query equals the student number, otherwise `manual_search`. Invalidation
requires a reason, checks that the registration belongs to the event, refuses an issued certificate,
and leaves a pending or approved review not eligible with that reason. Searching or viewing never
records. Nothing writes SQL, a stored file or the Activity Log.

**Scanner follow-up (#138, after PR #170).** Form-post scan and manual check-in results now
survive the redirect as an event-scoped, server-rendered outcome panel (identity, student number,
original time and refusal text), including without JavaScript. The camera adapter discards and
stops streams from permission requests superseded by a mode change or page exit. Lookup table
text uses an explicit dark foreground on its white rows. M-05/M-06/M-07 retain the same contracts,
providers, shared store and removal owners; these changes add no demo state or real writes.

**Removal (4.3, 4.5, 4.6).** 4.3 replaces `ICheckInService`, 4.5 replaces
`ICheckInLookupService`, and 4.6 replaces `IAttendanceInvalidationService`; each deletes its
provider and its line in `DemoCheckInRegistration.cs`, and the screen keeps its shape.

## M-08 — demo seam detail (4.12)

**Contract and classes.** The contract is in `ASI.Basecode.Services`; the provider is
`ASI.Basecode.Services/Services/MyEventsService.cs`, whose `GetAttendanceAsync` returns an empty
attendance page until 4.7.

| Piece | File |
|---|---|
| Read contract `IMyEventsService.GetAttendanceAsync` | `ASI.Basecode.Services/Interfaces/IMyEventsService.cs` |
| Method label rule `CheckInMethodDisplay` | `ASI.Basecode.Services/ServiceModels/CheckInMethodDisplay.cs` |
| Real provider (returns an empty page until 4.7) | `ASI.Basecode.Services/Services/MyEventsService.cs` |

**What it composes.** 3.13 removed the M-02 demo and switch-off providers and kept
`MyEventsService.GetAttendanceAsync` as an empty page. 4.7 replaces that method with the real
owner-scoped attendance query behind the same contract. The tab stays read-only: no check-in,
correction, edit or delete control. Until 4.7 the tab shows its empty state.

**Hand-over to 4.7 (register rule 12).** 3.13 removed M-02's `DemoMyEventsService`,
`EmptyMyEventsService` and `DemoMyEventsRegistration`, and kept `GetAttendanceAsync` on the real
`MyEventsService` returning an empty page. 4.7 replaces that method with the real owner-scoped
query instead of deleting a class.
## M-13a, M-13b and M-13c — participation report detail (7.4)

**Contract and classes.** All demo code is in `ASI.Basecode.WebApp/DemoData/`; the contract is in
`ASI.Basecode.Services`.

| Piece | File |
|---|---|
| Read contract `IParticipationReportService.GetReportAsync(eventId, page, ct)` | `ASI.Basecode.Services/Interfaces/IParticipationReportService.cs` |
| View model, row model, breakdown model and paging metadata | `ASI.Basecode.Services/ServiceModels/ParticipationReportViewModel.cs` |
| Real summary service and repository | `ASI.Basecode.Services/Services/ParticipationReportService.cs`, `ASI.Basecode.Data/Repositories/ParticipationReportRepository.cs` |
| Demo overlay provider (M-13b, M-13c and the M-05/M-07 attendance handover) | `ASI.Basecode.WebApp/DemoData/DemoParticipationReportService.cs` |
| Reports surface registration file | `ASI.Basecode.WebApp/DemoData/DemoReportsRegistration.cs` |
| Screen and styles | `Views/Reports/Participation.cshtml`, `Styles/features/reports.css` |

**Real summary.** `ParticipationReportService` reads the selector options and selected event through
`IEventRepository`, then reads the event summary from
`ParticipationReportRepository.GetSummaryFactsAsync`, which composes `ParticipationFacts.SeatHolders`,
`ValidAttendances` and `AttendanceRates` with `AsNoTracking`. The switch-off path uses the same real
service; there is no empty provider.

**Demo overlay.** `DemoParticipationReportService` calls the real service first, then composes
`IRegistrationRepository.GetReviewQueueAsync` once per registration status (Pending, Confirmed,
Waitlisted, Cancelled) to overlay the still-open M-13b/M-13c rows, breakdowns and no-shows.
`RegistrationReviewRow` projects `course_snapshot` and `year_level_snapshot`, so the breakdowns read
the signup snapshot rather than the member's current course. While M-05/M-07 remain open,
`DemoStore.TryGetAttendance` supplies check-ins and invalidations, and `DemoStore.GetAttendanceForEvent`
drives the demo valid count and rate when it holds attendance for the event (rule 12).
`DemoStore.IsCompleted(eventId)` supplies the effective Completed status while M-09 remains open.
Nothing is written. Because 7.3's paged query does not exist yet, the demo provider reads the whole
event and slices the detail page in memory; the real read pages in SQL.

- *M-13a summary* — served by `ParticipationReportService` and `ParticipationReportRepository` from
  the SQL dataset: status counts, Confirmed, Valid attendees and the unrounded Valid/Confirmed rate,
  N/A at zero Confirmed, "Not final" until the event is Completed, and N/A for a cancelled event.
  The demo store's attendance-derived valid count and rate only override this while M-05/M-07 remain
  open and the store holds attendance for the event.
- *M-13b no-shows and breakdowns* — a No-show is a Confirmed registration without valid attendance,
  counted only for a demo-completed, non-cancelled event; a cancelled event shows no figure. Course
  and year breakdowns group valid attendees by their registration signup snapshot and show each
  group's share of the valid attendees.
- *M-13c detail rows* — every registration of the event, ordered by member name then registration
  id, 25 per page; totals and breakdowns are computed before the slice. Each row carries the
  registration status label/badge, the attendance label/badge (Checked in, Invalidated, No-show,
  Not checked in) and the Manila check-in time.

**Seeded starting state.** The completed event seeded by M-01 (up to eight check-ins, the last one
invalidated) drives the populated report: counts, rate, no-shows, breakdowns and rows agree with
those check-ins. The ready-to-complete and check-in-open events show the not-final markers, and a
cancelled event in the connected database shows counts and rows with no no-show figure. Fewer rows
degrade gracefully; nothing is written.

**Removal.** 7.1 has replaced the summary computation (M-13a) with `ParticipationReportService` and
`ParticipationReportRepository`; 7.2 replaces the no-show and breakdown computation (M-13b) and 7.3
replaces the paged detail read (M-13c) behind the same contract, so the screen does not change
visibly. Each ticket sets its row to `removed` with the commit.

**Hand-over (rule 12).** From the 4.3 commit (real check-in) and the 4.6 commit (real invalidation),
the still-open M-13b/M-13c rows read the real attendance and invalidation source instead of
`DemoStore.TryGetAttendance`, so the report keeps matching recorded attendance until 7.2/7.3 remove
the rows. The demo provider is otherwise only the M-13b/M-13c overlay; the 7.1 summary always comes
from the real service.

## M-10a, M-10b, M-11a and M-11b — certificate ledger (6.12)

**Contract and registration.** `ASI.Basecode.Services/Interfaces/ICertificateLedgerService.cs`
defines `GetPageAsync`, `DecideAsync`, `IssueAsync`, `GetViewerAsync` and `GetPdfAsync`.
`DemoData/DemoCertificatesRegistration.cs` registers `DemoCertificateLedgerService` only when
the central switch is enabled and no real provider is already registered. With the switch off,
`UnavailableCertificateLedgerService` supplies empty tabs and explicit command/document refusals.
All three classes live under `ASI.Basecode.WebApp/DemoData/`.

| Row | Implementation and handoff |
|---|---|
| M-10a / removed by 6.4 | `DemoStore.MarkCompleted` creates pending reviews for every valid stored check-in through `EnsureCertificateReviews`; the query also ensures reviews for seeded or real completed events. Existing decisions are never overwritten. Replace the creation call/adapter with the completion hook and real attendance facts. |
| M-10b / removed by 6.6 | `DemoCertificateLedgerService.GetPageAsync` composes existing event and confirmed-registration repository reads with shared review state; `DecideAsync` reloads participation/event state and calls guarded `DemoStore.DecideCertificate`. Pending/valid attendance is required; decline needs a trimmed reason of 1–1,000 characters. Actor and time come from the authenticated request and server clock. |
| M-11a / removed by 6.7 | `IssueAsync` requires completion, confirmation, valid attendance and approval, creates a unique `AT-<year>-<registrationId:D6>` number and snapshot, calls the real `RenderSnapshotAsync`, rechecks real participation, then atomically publishes via `DemoStore.PublishCertificate`. Invalidation and duplicate issues are rechecked inside the store gate. Render failure publishes nothing. No SQL, private storage or audit write occurs. |
| M-11b / removed by 6.8 | `GetViewerAsync`/`GetPdfAsync` require Management and issued state; the controller streams `application/pdf` with no-store caching and an optional attachment name. A seeded issue renders its captured snapshot on first download, then reuses those bytes. No caller-supplied path is accepted. Replace with authorized retained-file retrieval, not current-data regeneration. |

**Seed and shared state.** The original seed's issued attendee now carries a snapshot of recipient,
event title/start, number and issue time. All other valid check-ins receive pending reviews before
the existing declined/pending scenarios are applied. Issued snapshots and PDF bytes stay in the
same `DemoStore` issue record used by 6.13; nothing is retained across restarts. Existing invalidated
reviews remain visible as not eligible with their reason. Later SQL edits do not rewrite issued
snapshot values or rendered bytes.

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
`GetPageAsync` for event titles and schedules. Confirmed registrations are joined to the same
`DemoStore` reviews and issues that 6.12 updates. Pending, eligible, declined (with reason), issued,
and unavailable (no review or missing issued snapshot) are displayed. Ordering is event start
descending then registration id descending, at the shared page size of 25. The existing My Events
certificate action and pager remain independent of Registrations and Attendance.

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
- Check-ins, review decisions and issued certificates last only until the app restarts; passes
  keep scanning.
- A pass scanned in the demo is matched against the real `registrations.qr_token`, the same value
  the server-rendered PNG encodes, so a cancelled registration's cleared token stops scanning.
