- source_plan: `_bmad-output/initiative-attendta/tickets/epic-platform-access/tickets/story-caller-owned-transaction-unit-of-work-and-append-only-activi-plan.md`
  summary: Remove venue image paths from cleanup-failure diagnostics.
  evidence: `VenueController.TryDeleteVenueImage` predates this story and logs the relative image path and exception, which may contain a filesystem path.
- source_plan: none
  summary: Improve the public account-request form with course choices, password guidance, field hints, and validation feedback.
  evidence: Split from management account-request review so approval and rejection can be delivered independently first.
- source_plan: `_bmad-output/initiative-attendta/plan-gh-34-signup-deadline.md`
  summary: Show precise Manila signup deadlines when stored values include seconds or milliseconds.
  evidence: Existing ManilaTimeFormatter truncates to minutes while EventLifecycleService accepts more precise timestamps, potentially displaying a deadline earlier than the actual cutoff.
- source_plan: `_bmad-output/initiative-attendta/handoffs/story-refactor-sweep-follow-up.md`
  summary: Add reviewer, reviewed-at, and reason metadata to decided account-request rows and keep the table shell on empty tabs.
  evidence: Impeccable critique on 2026-11-06 scored the queue 17/40; decided rows show only "Reviewed" and empty tabs drop the toolbar and column context.
- source_plan: `_bmad-output/initiative-attendta/tickets/epic-check-in/tickets/story-frontend-member-attendance-history-partial-plan.md`
  summary: Give one demo member enough attendance rows for the Attendance tab's pager to be demonstrable.
  evidence: `DemoSeedService` checks in at most eight confirmed registrations of the single completed event (`MaxSeededCheckIns = 8`) and a member has one registration per event, so a member's attendance history holds one row and `_Pager` never renders on `/MyEvents/Attendance`. The tab pages correctly for 26+ rows (verified by a temporary integration check), but the demo cannot reach that volume until 4.11's scanner and 4.7's real query supply more records.
- source_plan: `_bmad-output/initiative-attendta/tickets/epic-participation-reports/tickets/story-event-selector-and-participation-summary-kpis-plan.md`
  summary: Until 7.3 supplies real detail rows, the switch-off report shows its summary KPIs beside the unchanged view's "No registrations"/"No attendee data" empty-state copy.
  evidence: `ParticipationReportService` returns empty `Rows` and breakdowns by design while 7.2/7.3 remain open, and the accepted intent excludes paged detail rows as 7.3 scope. 7.3 closes it when the detail table is filled.
- source_plan: `_bmad-output/initiative-attendta/tickets/epic-check-in/tickets/story-member-attendance-view-on-my-events-plan.md`
  summary: Seeded demo-store check-ins no longer appear in the member Attendance tab after 4.7; only real SQL attendance does.
  evidence: 4.7's approved intent replaces the demo attendance source with the owner-scoped SQL query, so `DemoSeedService`'s in-memory check-ins remain visible on the scanner log and the report overlay (M-13c seam) but not in `/MyEvents/Attendance`. A check-in made through the real 4.3 engine does appear. The demo store is removed by 8.13; 8.15/8.12 should not expect seeded rows on this tab.
- source_plan: `_bmad-output/initiative-attendta/tickets/epic-check-in/tickets/story-member-attendance-view-on-my-events-plan.md`
  summary: `MyEventsController.Attendance` (and `Registrations`) lacks the `ResponseCache(NoStore)` attribute that the sibling `Certificates` action carries.
  evidence: `docs/CODING_STANDARDS.md` §5 asks sensitive responses to use the agreed no-store policy, and `Certificates` sets `NoStore = true` while `Attendance` and `Registrations` do not. The gap predates 4.7 and the approved intent freezes the controller action, so it is recorded rather than patched; no cache leak was demonstrated.
