---
title: 'On-demand QR Pass PNG rendering and download'
type: 'feature'
ticket: '1'
created: '2026-11-07'
status: 'in-review'
baseline_revision: '0a61d4e35f71c9c2ec15f538b876bfbee0bba1a2'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: ''
review_source: ''
lenses_ran: []
review_loop_iteration: 0
context:
  - '_bmad-output/initiative-attendta/demo-mock-register.md'
  - '_bmad-output/initiative-attendta/architecture-attendta/architecture-attendta.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The pass card's QR is drawn in the browser from a demo HMAC token, and no authorized PNG endpoint exists. The image is never vouched for by the server, and the code is not the real `registrations.qr_token`.

**Approach:** Add a real owner-scoped `PassService`, render the QR PNG on the server in `WebApp/Rendering` with `Net.Codecrete.QrCodeGenerator` (no `System.Drawing`), and serve it from one ownership-checked GET that streams in memory. The existing pass card shows the server image and links to the download. Remove the demo pass provider and the browser QR library.

## Boundaries & Constraints

**Always:** `Member` policy; only the actor's own Confirmed registration on a Published event with a 64-character lowercase hex token yields a pass. Unknown, foreign, cancelled, unconfirmed, unpublished and token-less requests all return the same 403 with the same message. The PNG encodes only the token. No writes, no token in logs or audit, `NoStore` on the image response. Rendering stays in `WebApp/Rendering`; the QR library is referenced by WebApp only. Keep the existing card layout and routes.

**Never:** Change `MyEventsController`, the My Events host/card, or add a second pass UI or credential. No print control, gate code, `System.Drawing`, or library that depends on it. No broad automation (8.5/8.4 own that), no per-ticket Impeccable critique or polish.

**Decision (2026-11-07):** `/Passes` is the only pass surface. The My Events host and its cards stay untouched, matching 4.10/4.13 and the story's "no second pass UI".

**Decision (2026-11-07):** the focused smoke proves ownership and a valid PNG; the decode-to-token proof stays with QR-01 at the finale. No decoder dependency is added to the test project.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Own confirmed pass | Published upcoming event, token set | 200 `image/png`; card shows QR and Download pass | No error expected |
| Own past pass | Event start passed | 200; card in Past passes | No error expected |
| Foreign / unknown / cancelled / unconfirmed / unpublished / no token | Any of these | 403, identical body, no image bytes | One indistinguishable refusal |
| Anonymous | No cookie | Redirect to login | Member policy |
| Demo switch on | `DemoData:Enabled=true` | Same behavior; demo scanner matches the real token | No error expected |
| No registrations | No rows | Empty state | No error expected |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Data/Interfaces/IRegistrationRepository.cs`, `Repositories/RegistrationRepository.cs` — add `GetMyPassRowsAsync(userId)` (one owner-scoped query: registrations → members → events → event_types → venues; Confirmed, Published, token not null), `GetOwnedPassAsync(registrationId, userId)` (id + owner only), and `FindConfirmedRegistrationIdByTokenAsync(eventId, token)` for the demo hand-over. Follow the existing projection-record style.
- `ASI.Basecode.Services/Services/PassService.cs` — new real `IPassService`: require the actor, map rows, split Active/Past on `TimeProvider`, accent from event id. `GetPassDownloadAsync` throws one `DomainException(Forbidden, "This pass is unavailable.")` for every refusal. Reuse `RegistrationStatusCode`, `RegistrationStatusDisplay`, `DomainException`.
- `ASI.Basecode.Services/Interfaces/IPassService.cs` — add `GetPassDownloadAsync`; keep `GetMyPassesAsync`.
- `ASI.Basecode.Services/ServiceModels/MyEventsViewModels.cs` — remove `QrToken`/`DownloadFileName`; keep `QrImageUrl`/`DownloadUrl`; add a small download result (token + file name).
- `ASI.Basecode.WebApp/Rendering/QrPassPngRenderer.cs` — new `IQrPassPngRenderer` + implementation: `QrCode.EncodeText(token, Ecc.Medium).ToPngBitmap(4, scale)`, scale chosen so the image is at least 168 px. No `System.Drawing`.
- `ASI.Basecode.WebApp/Controllers/PassesController.cs` — decorate the list model with `Url.Action("Qr", …)`; add `[HttpGet] [Route("Passes/Qr/{id:int}")]` with `bool download` and `[ResponseCache(NoStore = true)]`; catch `DomainException` → 403 + the same message.
- `ASI.Basecode.WebApp/Views/MyEvents/_PassPartial.cshtml` — render `<img src="@Model.QrImageUrl">` and the Download link only; delete the canvas/button branches.
- `ASI.Basecode.WebApp/Views/Passes/Index.cshtml` — delete the two script tags.
- `ASI.Basecode.WebApp/Startup.DI.cs` — register `IPassService` → `PassService` and `IQrPassPngRenderer` → `QrPassPngRenderer`.
- `ASI.Basecode.WebApp/DemoData/DemoDataRegistration.cs` — remove both `IPassService` lines.
- Delete `DemoData/DemoPassService.cs`, `DemoData/UnavailablePassService.cs`, `DemoData/DemoPassesRegistration.cs`, `wwwroot/lib/qrcode/`, `wwwroot/js/features/passes.js`.
- `ASI.Basecode.WebApp/DemoData/DemoCheckInService.cs` — match the real token through the new repository method instead of `DemoStore.GetPassToken` (register rule 12 hand-over; 4.3 deletes this provider).
- `ASI.Basecode.WebApp/ASI.Basecode.WebApp.csproj` — add `Net.Codecrete.QrCodeGenerator` 3.2.1 (MIT, no dependencies, no `System.Drawing`; QRCoder is rejected because it depends on `System.Drawing.Common`).
- `_bmad-output/initiative-attendta/demo-mock-register.md` — set M-03 to `removed` with the commit; update the known-limit line about rule-10 tokens.
- `_bmad-output/initiative-attendta/architecture-attendta/architecture-attendta.md` — add the QR library to the Stack table.
- `tests/ASI.Basecode.IntegrationTests/PassDownloadTests.cs` — new focused smoke.

## Tasks & Acceptance

**Execution:**
- [x] Repository methods above, with the owner-scoped pass query and the token lookup.
- [x] `PassService` and `IPassService.GetPassDownloadAsync`; remove the demo token fields from the model.
- [x] `QrPassPngRenderer`, controller action, partial, DI registration.
- [x] Delete the demo pass providers, browser QR library, and script tags.
- [x] Demo scanner hand-over to the real token.
- [x] Register, architecture Stack, and the focused integration test.

**Acceptance Criteria:**
- Given a signed-in member with a Confirmed registration on a Published event, when they open `/Passes`, then the card shows a server-rendered QR image and a Download pass link.
- Given that card, when they request the QR URL with `download=true`, then the response is 200 `image/png`, named `door-pass-<id>.png`, and the bytes are a valid PNG.
- Given another member's registration, an unknown id, or a cancelled, unconfirmed, unpublished or token-less registration, when they request the QR URL, then every response is 403 with the identical body `This pass is unavailable.` and no image bytes.
- Given the demo switch on, when an officer scans a pass from `/Passes`, then the demo scanner matches the real `registrations.qr_token`.
- Given the browser QR library and demo pass providers, when the change is applied, then they are deleted and no view references them.

## Verification

**Commands:**
- `dotnet build "ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj"` — expected: build succeeds.
- `$env:ATTENDTA_TEST_SQL="Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True"; dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj --filter FullyQualifiedName~PassDownloadTests` — expected: all pass.

**Manual checks (if no CLI):**
- Run with `DemoData__Enabled=false`, sign in as a member with a confirmed registration, open `/Passes`, see the QR, download it, and decode it with a phone scanner to the 64-character token. Request another member's id and confirm the same 403.

## Implementation Notes

- **Repository.** `GetMyPassRowsAsync` is one owner-scoped query (registrations → members → users →
  events → event_types → venues) filtered on Confirmed + Published + `qr_token IS NOT NULL`, ordered
  by start then registration id. `GetOwnedPassAsync` joins the event so it returns the event status
  too; `FindConfirmedRegistrationIdByTokenAsync` normalizes to lowercase and rejects anything that is
  not 64 hex characters before querying, so a malformed scan never reaches SQL.
- **Service.** `PassService` re-checks the canonical token shape in memory for both the list and the
  download, so a malformed stored value renders no card and downloads nothing. Every refusal is
  `DomainException(Forbidden, "This pass is unavailable.")`; the message is the constant
  `PassService.UnavailableMessage` so the controller and the test assert the same string.
- **Renderer.** `QrPassPngRenderer` uses `QrCode.EncodeText(token, Ecc.Medium)` and
  `ToPngBitmap(border, scale)`. The library's `border` parameter is a *factor of module size*, not a
  module count, so a border of 4 gives the 4-module quiet zone. Scale is
  `ceil(168 / (size + 8))`, which yields 172 px for a 64-character token at ECC Medium.
- **Controller.** `Passes/Qr/{id:int}` serves the inline image and `?download=true` from one action,
  mirroring `MyCertificatesController.Pdf`. `ResponseCache(NoStore = true)` covers the success path;
  the 403 path sets `Cache-Control: no-store` explicitly because it bypasses the result filter.
  `Index` fills `QrImageUrl`/`DownloadUrl` with `Url.Action`; routing renders the boolean as
  `download=True`, which the test matches case-insensitively.
- **Demo hand-over.** `DemoCheckInService` now matches the real token through
  `FindConfirmedRegistrationIdByTokenAsync` and reads the holder identity from the same confirmed
  row. `DemoStore.GetPassToken` and the M-01 `--demo-pass-token` switch have no remaining reader
  besides each other; both are left for 8.13, which removes the store, and the switch carries a
  comment saying so.
- **Test.** `PassDownloadTests` seeds upcoming, past, cancelled, unconfirmed, unpublished,
  token-less and foreign rows plus a member with no registrations. It asserts the card markup, the
  PNG signature and IHDR (≥ 168 px, square), the attachment name, `no-store`, and that all six
  refusal cases return the identical 403 body for both the inline and download URLs. Per the
  2026-11-07 decision it does not decode the QR; that proof stays with QR-01.

## Plan Change Log

- 2026-11-07 — `GetOwnedPassAsync` also returns the event status. The plan's download guard listed
  "unpublished" as a refusal, but an id+owner read alone cannot see the event; the join is the
  smallest change that keeps the single refusal without a second query.

## Review Triage Log

## Design Notes

The one GET serves both the inline image and the download (`?download=true`), mirroring `MyCertificatesController.Pdf`. The service authorizes and returns only the token; the WebApp renderer turns it into bytes, so Services never depends on an imaging library.
