---
title: 'Frontend member pass cards and QR download states'
type: 'feature'
ticket: '10'
created: '2026-11-07'
status: 'in-review'
baseline_revision: '5cb053d67f34ea8e544b92dbdde8ef3c18a38500'
route: 'full'
route_source: 'auto'
risk: 'low'
review: ''
review_source: ''
lenses_ran: []
review_loop_iteration: 0
context:
  - '_bmad-output/initiative-attendta/demo-mock-register.md'
  - '_bmad-output/initiative-attendta/ux-attendta/DESIGN.md'
  - 'docs/DESIGN.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A confirmed member has no pass to show at the door. The `_PassPartial` slot 1.13 placed on My Events cards renders nothing, and there is no My Passes screen for the profile action 4.13 will add. The real QR renderer (4.1) and the real owner-scoped registration query (3.13) do not exist yet.

**Approach:** Fill `_PassPartial` with the DESIGN §13 pass card and add a member-only `Passes` screen with Active and Past sections. A demo provider composes existing reads for the actor's confirmed registrations and takes the rule-10 token from `DemoStore`; the browser draws the QR from a locally served library and saves it as PNG.

## Boundaries & Constraints

**Always:** Authorize with the `Member` policy. Return only the actor's own registrations. Only a Confirmed registration on a published event yields a usable pass. The QR encodes only the 64-character token and keeps its quiet zone and aspect ratio. No visible word says the data is demo data. Reuse existing reads. No SQL, file or Activity Log write. Keep the Razor/Tailwind identity and local assets only.

**Never:** Change `MyEventsController` or the My Events host/card. Add demo rows other than M-03. Add a real service implementation in Services or Data. Add automated tests or run per-ticket critique/polish. Add a print control, a gate code or a second credential scheme.

**Decision (2026-11-07):** Add the "My Passes" link to the member navigation in `_NavLinks.cshtml`. It is the demo entry point until 4.13 adds the profile action.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Confirmed upcoming registration | signed-in member | Active card; QR decodes to the rule-10 token; Download PNG saves a PNG | No error expected |
| Confirmed past registration | event start has passed | Card in Past passes | No error expected |
| Pending, waitlisted or cancelled | status is not Confirmed | No card and no usable pass | No error expected |
| Cancelled or unpublished event | `GetPublishedEventAsync` returns null | No card | No error expected |
| Another member's registration | different user id | Never returned | No error expected |
| Demo switch off | `DemoData:Enabled=false` | Empty state, no error | No error expected |
| No registrations | no rows | Empty state | No error expected |
| Phone and desktop | 390px / 1440px | One column / two columns, no page overflow | No error expected |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Services/ServiceModels/MyEventsViewModels.cs` — `PassViewModel` (lines 52–66) is the pass contract. Add `EventId`, `EventTypeName`, `VenueName`, `StartsAt`, `StatusLabel`, `AccentClass`, `QrToken`, `DownloadFileName`; keep `QrImageUrl`/`DownloadUrl` for 4.1. Leave the other tab models alone.
- `ASI.Basecode.WebApp/Views/MyEvents/_PassPartial.cshtml` — the slot to fill; keep this path because 3.17 and 4.1 reference it. `_RegistrationsTab.cshtml:23` already calls it; do not change it.
- `ASI.Basecode.WebApp/Controllers/MyEventsController.cs` — leave untouched (story boundary).
- `ASI.Basecode.WebApp/Controllers/ProfileController.cs` — pattern for a `Member` controller.
- `ASI.Basecode.WebApp/DemoData/DemoDataRegistration.cs` — add one call to `DemoPassesRegistration`; `DemoEventsRegistration.cs` is the per-surface pattern.
- `ASI.Basecode.WebApp/DemoData/DemoStore.cs` — `GetPassToken(registrationId)` is the rule-10 token.
- `ASI.Basecode.Data/Interfaces/IRegistrationRepository.cs` — `DiscoverOpenEventIdsForUserAsync` and `GetMemberRegistrationAsync` (owner-scoped).
- `ASI.Basecode.Data/Interfaces/IEventRepository.cs` — `GetPublishedEventAsync` for title, type, venue and start.
- `ASI.Basecode.Data/Interfaces/IMemberRepository.cs` — `GetMemberRecordByUserIdAsync` for holder name and student number.
- `ASI.Basecode.Services/Interfaces/ICurrentActor.cs` — the actor's `UserId`.
- `ASI.Basecode.WebApp/Views/Shared/_NavLinks.cshtml:30-35` — member links; shared file owned by 1.13.
- `ASI.Basecode.WebApp/Views/Shared/_Layout.cshtml:54` — `Scripts` section already exists; do not change the layout.
- `ASI.Basecode.WebApp/Styles/features/passes.css` — pass styles, already imported by `Styles/app.css`.
- `ASI.Basecode.WebApp/wwwroot/lib/` — vendored jQuery lives here; put the QR library beside it. Vendor `qrcode-generator` 1.4.4 (MIT, Kazuhiko Arase) from `https://cdn.jsdelivr.net/npm/qrcode-generator@1.4.4/qrcode.js` with its `LICENSE` file. API: `qrcode(0, 'M')`, `addData(token)`, `make()`, `getModuleCount()`, `isDark(row, col)`.
- `ASI.Basecode.WebApp/Views/CheckIn/Index.cshtml:4` — `ViewData["Feature"]` example.

## Tasks & Acceptance

**Execution:**
- [ ] `ASI.Basecode.Services/Interfaces/IPassService.cs` — one `GetMyPassesAsync` contract — the seam 4.1 replaces.
- [ ] `ASI.Basecode.Services/ServiceModels/PassesViewModel.cs` — `Active` and `Past` lists plus `Empty()`.
- [ ] `ASI.Basecode.Services/ServiceModels/MyEventsViewModels.cs` — extend `PassViewModel` with the fields above.
- [ ] `ASI.Basecode.WebApp/DemoData/DemoPassService.cs` — compose the existing reads, keep only Confirmed, split Active/Past on `TimeProvider`, attach `DemoStore.GetPassToken`, and set the accent class from the event id.
- [ ] `ASI.Basecode.WebApp/DemoData/UnavailablePassService.cs` — return `PassesViewModel.Empty()` when the switch is off.
- [ ] `ASI.Basecode.WebApp/DemoData/DemoPassesRegistration.cs` and `DemoDataRegistration.cs` — register the demo or switch-off provider.
- [ ] `ASI.Basecode.WebApp/Controllers/PassesController.cs` — `Member` controller at `Passes`/`Passes/Index`.
- [ ] `ASI.Basecode.WebApp/Views/Passes/Index.cshtml` — "Door Passes" with Active and Past sections, empty states, and the partial by path (`~/Views/MyEvents/_PassPartial.cshtml`).
- [ ] `ASI.Basecode.WebApp/Views/MyEvents/_PassPartial.cshtml` — DESIGN §13 card: event metadata, holder identity, QR canvas (or `QrImageUrl` when 4.1 supplies it), Download PNG.
- [ ] `ASI.Basecode.WebApp/Styles/features/passes.css` — one/two-column grid from 768px, thin event-colored top rule, 168px QR on white, accent classes.
- [ ] `ASI.Basecode.WebApp/wwwroot/lib/qrcode/qrcode.js` + `LICENSE` — vendor MIT `qrcode-generator` 1.4.4.
- [ ] `ASI.Basecode.WebApp/wwwroot/js/features/passes.js` — draw each canvas from `data-pass-token` with a 4-module quiet zone and wire Download PNG to `toDataURL('image/png')`.
- [ ] `ASI.Basecode.WebApp/Views/Shared/_NavLinks.cshtml` — add My Passes for members.
- [ ] `_bmad-output/initiative-attendta/demo-mock-register.md` — fill the M-03 detail block (interface, classes, files).

**Acceptance Criteria:**
- Given a signed-in Member with a confirmed upcoming registration, when they open `/Passes`, then an Active card shows the event, holder and student number with a QR that decodes to that registration's rule-10 token.
- Given that card, when they press Download PNG, then a PNG of the same QR is saved.
- Given a pending, waitlisted, cancelled or unpublished-event registration, when they open `/Passes`, then no usable pass appears for it.
- Given another Member's registration, when they open `/Passes`, then it never appears.
- Given `DemoData:Enabled=false`, when they open `/Passes`, then the empty state shows and no error occurs.
- Given 390px and 1440px, when the screen renders, then one and two columns show with no page overflow.
- Given the repository, when build, `css:build`, both test projects and `test:js` run, then all pass and the regenerated `wwwroot/css/app.css` is committed.

## Design Notes

- A new controller, not `MyEventsController`: the story forbids it, and 4.13 consumes a passes route.
- A Services *interface* only, no service: register rule 9 wants the demo provider behind the contract 4.1 will implement; the demo queue forbids a new service in Services or Data.
- The token sits in a `data-` attribute because the browser draws the QR; it is not personal data and is never logged. 4.1 replaces it with a server-rendered PNG.
- The list composes existing reads because no owner-scoped cross-event query exists yet (M-02, removed by 3.13); register rule 1 allows composing reads.
- Gate code is omitted: no approved data source (D-08). The accent is deterministic from the event id and visual only.

## Implementation Notes

- `IPassService` is a contract only; `DemoPassService` composes `DiscoverOpenEventIdsForUserAsync`, `GetMemberRegistrationAsync`, `GetPublishedEventAsync` and `GetMemberRecordByUserIdAsync`, keeps only Confirmed registrations on published events, and takes the token from `DemoStore.GetPassToken`. `UnavailablePassService` returns the empty state when the switch is off.
- The QR library is `qrcode-generator` 1.4.4 (MIT) vendored at `wwwroot/lib/qrcode/`. The npm package ships no `LICENSE`, so the upstream MIT text is included. `passes.js` draws each canvas with a 4-module quiet zone and wires Download PNG to `toDataURL('image/png')`.
- `Views/Passes/Index.cshtml` loads the library before `passes.js` in its `Scripts` section. The layout's `ViewData["Feature"]` hook renders the feature script before `Scripts`, which would break load order, so the view owns both tags.
- Handoff for 3.17: when `card.Pass` is filled on My Events, that page must also load `~/lib/qrcode/qrcode.js` and `~/js/features/passes.js` (same order) or the slot's canvas stays blank. `MyEventsController` and the host card were left untouched as the story requires.
- Verification (2026-11-07): `dotnet build` 0 warnings/0 errors; Services 271/271; Integration 319/323 with only the four `CertificateTracerFlowTests` Poppler (`pdftotext`) environment failures; `css:build` idempotent with no drift; `test:js` 1/1.
- Playwright smoke against `AttendTaMVP_Test` with `DemoData__Enabled=true`: signed in as a member with one upcoming confirmed, one past confirmed, one pending, one cancelled and one draft-event registration. `/Passes` showed 1 Active and 1 Past card, 2 canvases at 180x180 with dark modules, and no card for the pending, cancelled or draft rows. `jsQR` decoded both the canvas PNG and the downloaded `door-pass-1.png` to the exact 64-character token, which equals the HMAC-SHA256 of registration id 1 under `DemoData:TokenKey`. The download was a valid 2739-byte PNG. At 390px the grid was one column with no horizontal overflow; at 1440px it was two columns; keyboard focus reached Download PNG. Smoke rows were added to the disposable local database only.

## Verification

**Commands:**
- `dotnet build ASI.Basecode.sln` — expected: 0 warnings, 0 errors.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — expected: all pass.
- `$env:ATTENDTA_TEST_SQL='Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True'; dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj` — expected: all pass (the PDF-inspection cases need Poppler locally).
- `npm --prefix ASI.Basecode.WebApp run css:build` then `git diff --exit-code -- ASI.Basecode.WebApp/wwwroot/css/app.css` — expected: no drift after the regenerated file is committed.
- `npm --prefix ASI.Basecode.WebApp run test:js` — expected: 1 test passes.

**Manual checks (if no CLI):**
- Start with `DemoData__Enabled=true`, `DemoData__TokenKey=<local secret>` and `DemoData__AllowDatabase=AttendTaMVP_Test`; sign in as a member with confirmed registrations.
- Open `/Passes`: Active and Past sections populate; scan a QR with 4.11 or a phone and expect the same token; Download PNG saves a readable PNG.
- Check a pending/waitlisted/cancelled registration shows no pass, then 390px and 1440px with keyboard focus and no horizontal overflow.
