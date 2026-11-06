# Independent quick-review handoff

No subagent tool is exposed in the implementation session. Run the following prompt in a separate session and return its findings before finalizing this preliminary build. Thorough/adversarial lenses are skipped on the one-shot route. No independent reviewer has run yet.

Read `C:/Users/JEHRON/Documents/Programming/ALLIANCE/AttendTa/_bmad-output/initiative-attendta/tickets/epic-release-acceptance/tickets/story-final-frontend-walkthrough-targeted-refactor-and-impeccable-plan.md` — the plan holding the acceptance criteria and intent — and the files its frontmatter `context:` lists, resolved under `C:/Users/JEHRON/Documents/Programming/ALLIANCE/AttendTa/`. Read the agent instruction files (`AGENTS.md`, `CLAUDE.md`, or equivalents) at the repository root and in the directories the diff touches; those are the rules. Then read the unified diff at `C:/Users/JEHRON/AppData/Local/Temp/attendta-153-review-b4a9776a3ffc42febfbbc7f985bbcf50.diff` — it is the change under review.

Report unmet acceptance criteria, broken rules, and bugs. Each finding: the location, what goes wrong, and evidence checked against the surrounding code. Output a Markdown list of findings only — no severity, priority, or ranking.

Review by inspection only. Do not modify code, run builds or tests, or invoke quality gates. Do not invoke any skill, and do not spawn subagents of your own — you are the reviewer. Return your findings as text in your final message; do not route them through any findings-reporting tool the host may offer.

Scope clarification from the user: this is the authorized preliminary shared-dialog polish for issue #153 / BMAD 8.12, not the final integrated walkthrough. The known #152/#135 gates remain blocked and must not be treated as accepted or implemented here. Do not remove the demo seam or implement missing integrations. No commits or pushes are authorized. The unrelated untracked Master Data quick plan is intentionally excluded from the review diff and must remain untouched.
