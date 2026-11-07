---
title: 'Member certificate history and download-link states'
type: 'feature'
ticket: 13
created: '2026-11-07'
status: 'built'
baseline_revision: 'e99a7b8b7bfc5caec1ac013147b9c5d293470c9d'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'none'
review_source: 'pinned'
lenses_ran: []
review_loop_iteration: 0
followup_review_recommended: false
context: ['C:/Users/vv/AppData/Local/Temp/opencode/attendta-demo-146/.opencode/skills/impeccable/reference/craft-floor.md']
warnings: ['oversized']
deferred: []
---

<intent-contract>

## Intent

**Problem:** The member Certificates tab is an empty slot despite shared officer review and issued-document state already existing.

**Approach:** Add an owner-scoped certificate history provider using the existing store, real repository reads and real snapshot renderer; show pending, eligible, declined, issued and unavailable states with issued-only View/Download.

## Boundaries & Constraints

**Always:** Use only this isolated worktree. Derive identity from ICurrentActor. Recheck real registration ownership for every PDF request. Reuse DemoStore issues, snapshots and cached bytes; consolidate seeded-document rendering with the ledger instead of duplicating it. Use existing service contracts/models where available and add only the missing member contract. Preserve the host's active-tab pager. Keep the product's restrained design and phone/keyboard usability. DemoData:Enabled off yields an empty list and a refusal for document reads. Existing real registrations take precedence. No visible demo/sample/mock/prototype wording. Update only M-12's register details and PRODUCT.md.

**Never:** SQL, private-file or audit writes; schema/backend integration; pass/attendance UI edits; whole-host replacement; delivery-queue or other story edits; new automated tests; per-ticket critique/polish/separate review. User authorized commit/push/PR, performed by orchestrator after verification.

</intent-contract>

## Code Map

- `ASI.Basecode.WebApp/Controllers/MyEventsController.cs`: only certificate action loading plus indispensable constructor injection; other tab actions unchanged.
- `ASI.Basecode.Services/ServiceModels/MyEventsViewModels.cs`: existing certificate row needs explicit review state/reason and nullable issue date; host already has FromCertificates and independent pagination.
- `ASI.Basecode.WebApp/Views/MyEvents/_CertificateLinkPartial.cshtml` and `Styles/features/certificate-history.css`: owned surface. CSS entry already imports this feature. Index.cshtml already renders empty state and pager; do not change whole host.
- `ASI.Basecode.WebApp/DemoData/DemoCertificateLedgerService.cs`: GetPdfAsync loads store issue/snapshot, renders seeded PDF once via RenderSnapshotAsync then attaches bytes. Extract shared document helper if needed, retain Management authorization.
- `ASI.Basecode.WebApp/DemoData/DemoCertificatesRegistration.cs`: existing TryAdd registration surface, add member provider and switch-off provider here.
- `ASI.Basecode.WebApp/DemoData/DemoStore.cs`: existing TryGetReview, TryGetIssuedCertificate, AttachCertificateDocument, EnsureCertificateReviews; no new store or duplicated seed.
- `ASI.Basecode.Data/Interfaces/IRegistrationRepository.cs`: DiscoverOpenEventIdsForUserAsync/GetMemberRegistrationAsync for owner reads; DiscoverReviewAsync returns UserId/EventId for direct document ownership. Existing IEventRepository CountAsync/GetPageAsync supplies event titles/start/status.
- `ASI.Basecode.WebApp/Controllers/CertificateLedgerController.cs`: existing PDF content type/no-store patterns. Member endpoint may stream inline PDF for View and attachment for Download with friendly refusal view.
- `docs/DESIGN.md`: visual authority read by orchestrator; white compact records, status text and semantic colors, Outfit, restrained navy actions, wrapping phone controls, amber focus.

## Tasks & Acceptance

**Execution:**
- [x] `ASI.Basecode.Services/Interfaces/IMyCertificatesService.cs`, `ASI.Basecode.Services/ServiceModels/MyEventsViewModels.cs`, `ASI.Basecode.WebApp/DemoData/DemoMyCertificatesService.cs`, `UnavailableMyCertificatesService.cs` — add member certificate contract/providers and enrich certificate row model for owner-scoped list and PDF reads.
- [x] `ASI.Basecode.WebApp/DemoData/DemoCertificateDocuments.cs`, `DemoCertificateLedgerService.cs`, `DemoCertificatesRegistration.cs` — consolidate shared in-memory document retrieval and register member adapters behind the existing switch.
- [x] `ASI.Basecode.WebApp/Views/MyEvents/_CertificateLinkPartial.cshtml`, `Styles/features/certificate-history.css`, `Controllers/MyEventsController.cs` and certificate-scoped document endpoint/view — fill the partial/styles and wire only its action/authorized endpoint. Error/empty states offer a useful next step without duplicate host empty text.
- [x] `_bmad-output/initiative-attendta/demo-mock-register.md`, `PRODUCT.md` — fill M-12 interface/classes/composition/seed/handoff details and update product capabilities.

**Acceptance Criteria:**
- Given owned confirmed registrations and shared reviews, when the member opens Certificates, then review states and reasons are shown; only issued documents with snapshots expose View/Download.
- Given an issued certificate, when its owner views/downloads, then application/pdf uses the same snapshot and cached bytes as the officer ledger; another owner/unknown id gets identical refusal.
- Given no history or the switch disabled, when Certificates opens, then the existing empty state appears with no download links.
- Given a phone or keyboard user, when reading history and paging, then status/reasons/actions remain reachable and the pager stays on Certificates independently of other tabs.

## Implementation Notes

Route full: estimated over 100 changed lines across provider, scoped UI and document retrieval. Risk raised to medium because owner-scoped PDF authorization is involved. Impeccable context already ran; docs/DESIGN.md was read. Implement directly from incumbent visual identity, without critique/polish under the explicit demo exception. Run craft-floor before UI edits. Orchestrator handles actual screen smoke, git and PR after coding; do not commit/push or touch remote issues from the implementation agent.

2026-11-07 resume: nested delegation was interrupted by machine memory exhaustion and left no code changes. Per explicit user steering, the orchestrator implements directly instead of stopping on delegation infrastructure. Verification uses `dotnet build -m:1 -nr:false -p:UseSharedCompilation=false` and installed Edge for the browser smoke. No review lenses, critique, polish or broad new tests.

Indispensable shared changes: the host receives only certificate service injection/action loading, plus suppression of its generic empty/footer blocks when that tab failed to load; other tab actions and UI are untouched. The certificate row contract gains review/document state. The ledger's existing render/cache logic is extracted unchanged in purpose into `DemoCertificateDocuments` so both member and officer use one snapshot/PDF path; its existing test fixture constructor is mechanically updated to use that helper. Registration stays in the existing certificate surface file.

The real PDF renderer now accepts optional `Certificates:BrowserChannel` (environment `Certificates__BrowserChannel=msedge` for this smoke) so the installed Edge channel can render without a default Chromium installation. Unset keeps the existing Playwright default. No browser-directory probing, browser path override, alternate renderer or PDF stub was used.

## Plan Change Log

## Review Triage Log

## Verification

**Commands:**
- `dotnet build -m:1 -nr:false -p:UseSharedCompilation=false` — successful memory-limited serial solution build.
- `npm run css:build` in ASI.Basecode.WebApp — reproducible generated app.css.

**Screen smoke:** Run with DemoData__Enabled=true against an isolated disposable local database if needed; inspect the actual rendered Certificates screen. Record exact observed checks and any remaining human checks honestly. No broad tests or separate review per demo rescope.

## Auto Run Result

Implemented member certificate review/history and owner-authorized issued PDF View/Download, sharing 6.12's store, real snapshot renderer and in-memory document bytes. Review: none, pinned by the approved demo exception; no lenses launched. Interrupted nested implementation was replaced by direct implementation under the user's resume instruction.

### Files changed

- `ASI.Basecode.Services/Interfaces/IMyCertificatesService.cs`: owner-history/document contract.
- `ASI.Basecode.Services/ServiceModels/MyEventsViewModels.cs`: explicit review state, optional issue date, snapshot schedule and document availability.
- `ASI.Basecode.WebApp/DemoData/DemoMyCertificatesService.cs`: real owner-registration composition plus shared review/issue state and fixed-size paging.
- `ASI.Basecode.WebApp/DemoData/UnavailableMyCertificatesService.cs`: switch-off empty history and document refusal.
- `ASI.Basecode.WebApp/DemoData/DemoCertificateDocuments.cs`: shared snapshot render/cache, validated PDF bytes and cloned document reads.
- `ASI.Basecode.WebApp/DemoData/DemoCertificateLedgerService.cs`: delegates existing render/cache work to the shared helper, retaining Management guards.
- `ASI.Basecode.WebApp/DemoData/DemoCertificatesRegistration.cs`: certificate-owned conditional member/helper registration with real-provider precedence.
- `ASI.Basecode.WebApp/Controllers/MyEventsController.cs`: certificate-only model loading/error handling and dependency injection.
- `ASI.Basecode.WebApp/Controllers/MyCertificatesController.cs`: member document route, per-request ownership via service, inline/attachment PDF and no-store/refusal handling.
- `ASI.Basecode.WebApp/Views/MyCertificates/Unavailable.cshtml`: accessible refusal/recovery screen.
- `ASI.Basecode.WebApp/Views/MyEvents/_CertificateLinkPartial.cshtml`: five statuses, reasons, issued-only actions and helpful empty/error content.
- `ASI.Basecode.WebApp/Views/MyEvents/Index.cshtml`: certificate error suppresses generic empty text and footer, preserving all existing tab content and paging.
- `ASI.Basecode.WebApp/Styles/features/certificate-history.css`: scoped density/wrapping, contained phone-width table scroll, reachable actions and selection color.
- `ASI.Basecode.WebApp/wwwroot/css/app.css`: generated by CSS build only.
- `ASI.Basecode.WebApp/Certificates/PlaywrightCertificatePdfRenderer.cs`: optional configured browser channel; existing default unchanged when absent.
- `tests/ASI.Basecode.IntegrationTests/CertificateLedgerTests.cs`: mechanical existing-fixture constructor update for the extracted document helper.
- `PRODUCT.md`: current member certificate capabilities.
- `_bmad-output/initiative-attendta/demo-mock-register.md`: M-12 contract/provider/composition/seed/removal handoff detail only.
- This plan: scope, direct-implementation substitution and honest evidence.

### Verification observed

- `dotnet build -m:1 -nr:false -p:UseSharedCompilation=false`: passed, 0 warnings and 0 errors, including all solution test projects. Repeated after optional browser-channel configuration was added.
- `npm ci` / `npm run css:build` in WebApp: passed; npm reported 0 vulnerabilities.
- Impeccable mechanical detector on certificate partial, refusal view and scoped CSS: `[]` (no findings). No critique or polish pass.
- Existing focused guard: `dotnet test --project tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj --no-build -- --filter-class ASI.Basecode.IntegrationTests.CertificateLedgerTests`: 8 passed, 0 failed/skipped. The initial legacy `--filter` invocation ran zero tests (exit 5); corrected to this repository's Microsoft.Testing.Platform filter syntax. No new automated tests or broad suite added/run.
- Temporary Edge interaction smoke used the real app, real login, `DemoData__Enabled=true`, canonical schema and disposable database `AttendTa_IT_146_6a72033c01fdf914`. Pending, eligible, declined with reason, issued, unavailable and empty states passed. Officer approval and issuance reflected on the next member read. Non-issued rows exposed no document actions. Unknown and other-owner documents both returned 404.
- Real `RenderSnapshotAsync` through installed Edge returned `%PDF-` / `application/pdf`; inline and attachment bytes matched, no-store was present, and officer/member reads matched the same bytes.
- Paging exercised 27 owned records as 25+2, preserved the Certificates route, and changing tabs reset the independent pager.
- Inspected actual desktop 1440px and phone 390px screenshots. No page-wide horizontal overflow; table scroll remained contained; keyboard focus reached PDF controls with the amber outline. Read actual eligible and declined screenshots. These are smoke observations, not human acceptance.
- Real SQL attendance/certificate/activity-log counts were all 0 after interactions. Disposable database was dropped; no shared database was mutated. Temporary runtime scripts, screenshots and credentials are outside the repository and are not part of the commit.

### Remaining limits

- Peer approval and CI remain PR gates; no human acceptance is claimed.
- Complete supported-browser/assistive-technology and physical-phone coverage remains human/finale work. This ticket used Edge desktop and a 390px emulated viewport.
- Backend owner query/retained-document integration remains 6.9 (M-12), with handoff from 6.8 documented. Shared in-memory state resets on restart by design.
- Switch-off and transient-error branches are implemented but were not separately exercised in this focused live smoke.
