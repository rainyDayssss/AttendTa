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
