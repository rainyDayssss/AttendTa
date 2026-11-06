---
title: 'Published event limited edits and immutable states'
type: 'feature'
ticket: '4'
created: '2026-11-06'
status: 'built'
baseline_revision: '74bbd29400d2e88b7078635d7562905b7e86424a'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - 'AGENTS.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Management cannot edit published event text, and lifecycle restrictions must remain enforceable against stale or manipulated requests.

**Approach:** Allow authorized published-event title and description edits and existing cover replacement, with atomic, path-free audit records; preserve draft behavior and make terminal states read-only.

## Boundaries & Constraints

**Always:** Enforce management authorization, antiforgery, state and immutable fields server-side. Discover references, lock event type → venue → event, recheck identities and state, and persist edits and `event.updated` audit together. Record old/new text; represent cover changes without file paths. Preserve private storage and its existing cleanup. Published edits must work even when existing masters have become inactive.

**Never:** Allow published schedule, type, venue, limit or deadline changes, cover removal, or any Completed/Cancelled mutation. Do not trust posted status or expose paths. Do not implement other lifecycle stories, schema changes, or file I/O while locks are held.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Published edit | Authorized officer changes title/description | Persist text and one audit with old/new values | Success notification |
| Tampering | Changed type, venue, schedule, limit or deadline | No mutation or audit | Validation refusal |
| Cover replacement | Published event and valid image | Replace through existing private store; audit change without path | Preserve existing cleanup and errors |
| Stale or terminal | State or reference changed since discovery; Completed/Cancelled | No mutation or audit | Stale or validation refusal |
| Unauthorized or invalid | Member, anonymous, missing antiforgery or invalid data | No mutation or audit | Existing authorization or validation response |
| Persistence failure | Audit or save fails | Event and audit roll back | Existing failure handling |

</frozen-after-approval>

## Code Map

- `ASI.Basecode.Services/Services/EventLifecycleService.cs`, `Interfaces/IEventLifecycleService.cs` — `UpdateDraftAsync`, `PublishAsync`, `UploadCoverAsync`, snapshot and actor helpers establish service, transaction, lock-order and audit patterns; cover already permits Published, not terminal states.
- `ASI.Basecode.Data/Repositories/EventRepository.cs` — `GetEventAsync` discovers references; `LockEventAsync` refreshes locked state. Existing master locks and cover lock helper avoid requiring active references unnecessarily.
- `ASI.Basecode.WebApp/Controllers/EventController.cs`, `Views/Event/Index.cshtml`, `Views/Event/Edit.cshtml`, `Views/Event/_Form.cshtml` — current draft-only edit and Manila/UTC conversion; make Published edits accessible without trusting hidden status or disabled inputs.
- `ASI.Basecode.Services/ServiceModels/EventViewModel.cs` — existing shared form fields include all immutable values; compare submitted values to the locked row, not posted status.
- `tests/ASI.Basecode.Services.Tests/EventLifecycleServiceTests.cs`, `EventCoverServiceTests.cs` — service fake actor, lock order, audit and private-file patterns.
- `tests/ASI.Basecode.IntegrationTests/EventDraftFlowTests.cs`, `EventCoverFlowTests.cs`, `EventPublicationFlowTests.cs` — authenticated HTTP, SQL rollback, antiforgery and cover flows. Update assertions that currently reject all Published edits.

## Tasks & Acceptance

**Execution:**
- [x] `ASI.Basecode.Services/Interfaces/IEventLifecycleService.cs`, `ASI.Basecode.Services/Services/EventLifecycleService.cs` — implement management-only published edit using locked state, immutable-field comparisons, stale detection and atomic old/new audit; preserve drafts and cover semantics.
- [x] `ASI.Basecode.WebApp/Controllers/EventController.cs`, `ASI.Basecode.WebApp/Views/Event/Index.cshtml`, `ASI.Basecode.WebApp/Views/Event/Edit.cshtml`, `ASI.Basecode.WebApp/Views/Event/_Form.cshtml` — expose state-appropriate editing, retain posted immutable values for validation, and keep terminal states read-only.
- [x] `tests/ASI.Basecode.Services.Tests/EventLifecycleServiceTests.cs`, `tests/ASI.Basecode.Services.Tests/EventCoverServiceTests.cs` — exercise matrix, each immutable field, roles, lock order, stale references, inactive masters, rollback and cover audit privacy.
- [x] `tests/ASI.Basecode.IntegrationTests/EventDraftFlowTests.cs`, `tests/ASI.Basecode.IntegrationTests/EventCoverFlowTests.cs`, `tests/ASI.Basecode.IntegrationTests/EventPublicationFlowTests.cs` — exercise real HTTP and SQL, server-side tampering, status transitions, antiforgery, audit privacy and rollback; adapt obsolete published-edit assertions.

**Acceptance Criteria:**
- Given a Published event and Officer, when title and description are submitted, then the persisted values change and the activity log contains their old and new values.
- Given a Published event, when any immutable field is changed by a forged request, then neither the event nor activity log changes.
- Given a Published event, when its cover is replaced, then the activity log records the replacement without any storage path; cover removal is refused.
- Given a Completed or Cancelled event, when any edit or cover mutation is attempted, then no event or audit changes.
- Given a stale reference or status, missing antiforgery, unauthorized actor, invalid input, or audit failure, when editing is attempted, then the request is refused or rolled back without partial changes.

## Implementation Notes

- Build passed with no warnings; service tests passed 161/161; SQL integration tests passed 189/189 with LocalDB, Playwright Chromium and Poppler available. Tests cover the matrix including SQL rollback, state and reference races simulated at the service lock, immutable-field tampering, authorization and cover audit privacy.
- Final verification after review patch: build passed without warnings, 162/162 service tests and 189/189 SQL integration tests passed. Published edits extend the existing lifecycle method rather than adding an interface method; SQL coverage is in `PublishedEventEditFlowTests.cs`.

## Plan Change Log

## Review Triage Log

- Medium / patch: controller-observed Draft can become Published before service discovery, letting a stale draft request edit published text. Pass the controller-observed state through the existing model and reject a mismatch at service discovery; retain the locked-state and immutable-field checks.

## Design Notes

The server compares submitted immutable values against the locked persisted event, independently of UI controls and posted status. Preserve the existing local-time conversion and account for SQL datetime precision. Reference locks precede the event lock even for text-only edits; inactive existing masters are not a reason to refuse text changes.

## Verification

- `dotnet build` — succeeds.
- `dotnet test tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj` — all pass.
- With `ATTENDTA_TEST_SQL` set to a local SQL Server master connection, `dotnet test tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj` — all pass.
