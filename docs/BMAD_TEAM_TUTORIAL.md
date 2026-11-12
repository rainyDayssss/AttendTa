# AttendTa: from a GitHub issue to a tested pull request with BMAD

For Group 7. Written against this checkout and its installed BMAD skills on **2026-11-05**.

BMAD gives your AI coding assistant a repeatable way to plan, build, test, and review a task. You approve the plan and check the result. Start with the route below; you do not need to read every reference before doing one story.

## 1. Start here

### Build one existing story — quick start

If your tools are ready, paste this into your **AI chat**, replacing the placeholders with your own task:

```text
Use bmad-build for BMAD story [EPIC.STORY] in initiative-attendta.
GitHub issue: [FULL_ISSUE_URL]

Read the existing story and its epic. Check that the required code is available,
the feature branch is correct, and the working tree is clean.
Inspect any existing implementation before planning so completed work is reused.
Work only on this story. Explain the plan and required tests in simple words.
Stop for my plan approval before implementation.
```

For example, replace the placeholders with story **`3.7`** and **`https://github.com/rainyDayssss/AttendTa/issues/36`** only if that is your assigned, ready task. Use the [ownership tables](#15-who-owns-each-epic-and-story) to find your own IDs.

**Before starting:** your tools must be set up, the existing issue must be claimed and synced, its prerequisite code must be available, and you must have a clean feature branch based on the team's `dev`. If you already have work that BMAD has not seen, [compare that progress first](#3-bring-existing-progress-into-bmad). If any of these checks are unclear, use the routes below before running the build.

**After the plan appears:** read it, answer its questions, and say **“Approve and continue”** when ready. Then [test the result](#7-test-it-and-keep-the-evidence), [review it](#8-understand-and-review-the-change), and [open a PR](#9-open-the-pr-and-handle-feedback). The [full build instructions](#6-build-with-bmad) explain each step.

### Choose your route

| Your situation | Where to go next |
|---|---|
| I already wrote code before BMAD knew about it | [Bring existing progress into BMAD](#3-bring-existing-progress-into-bmad) before starting a new build. |
| This is my first time using this checkout and BMAD | Read the [essential team rules](#2-essential-team-git-and-qa-rules), then complete [setup](#4-install-and-check-the-tools-once). |
| My tools are ready and I need to start a task | Check the [ownership table](#15-who-owns-each-epic-and-story) and [dependencies](../_bmad-output/initiative-attendta/dependency-graphs.md), then [start the issue](#5-start-one-issue-correctly). |
| I am returning to unfinished work | [Resume the saved plan](#continue-tomorrow-without-starting-over); do not create a second implementation. |
| My code is ready for review | Follow [testing](#7-test-it-and-keep-the-evidence), [review](#8-understand-and-review-the-change), and [PR submission](#9-open-the-pr-and-handle-feedback). |

### The everyday workflow

1. Choose an existing issue that belongs to your work.
2. Check its prerequisites and agree who will work on it.
3. Update your checkout and create a feature branch from the team's `dev` branch.
4. Ask BMAD's product owner to sync and start the existing ticket.
5. Run `bmad-build` with the **BMAD story ID and GitHub issue URL**.
6. Read the plan, settle questions, then approve implementation.
7. Read the result, run the required tests, and try the feature yourself.
8. Use a walkthrough or another review when helpful.
9. Push your feature branch and open a PR to the team's `dev` branch.
10. Address review comments and failed checks, then get peer approval and merge.
11. Ask the product owner to sync the merged result and mark the issue done after verification.

**A PR is a pull request:** a request for someone to review and merge your branch. An issue describes the work; the PR delivers it. Opening a PR does not mean the issue is done.

### Know which task you are naming

| Term | Plain meaning | AttendTa example |
|---|---|---|
| Initiative | The whole planned body of work | `initiative-attendta` |
| Module | A part of the application | Authentication, Master Data, Attendance |
| Epic | A group of related stories | Epic 3: Events and registration |
| Story or ticket | One planned piece of work | `3.7`: registration review and seat allocation |
| GitHub issue | The published task on GitHub | [Issue #36](https://github.com/rainyDayssss/AttendTa/issues/36) represents story `3.7` |
| Prerequisite / blocked by | Something this task needs first | Story `3.7` needs `3.6` and `3.15` |
| Plan | The assistant's proposed implementation and test steps | A `story-...-plan.md` file beside the story |
| Verification | Evidence that the change works | Test results and a successful manual check |
| `hitl` | “Human in the loop”: a person must do part of it | Camera checks or supplying official signatory details |

**`3.7` and `#36` are different IDs.** Do not ask BMAD to build “ticket 36” when you mean BMAD story `3.7`. Use both IDs and the full issue URL so there is no confusion. The ownership tables below include both.

There are 75 published story issues. The epic names are in their bodies; separate GitHub epic issues were not created. GitHub's blocked-by links connect the story issues. The local BMAD files contain their GitHub IDs and URLs.

### Keep these open beside your task

- [Ownership and sequence overview](../_bmad-output/initiative-attendta/module-epic-story-ownership.md): find the module owner and assigned story IDs.
- [Dependency graphs and start waves](../_bmad-output/initiative-attendta/dependency-graphs.md): see what must come first. Confirm the live issue's blockers too.
- Your existing GitHub issue, local story, and epic: use their requirements and verification steps as the task's starting point.

### Find the detailed instructions

**Before coding:** [2. Team rules](#2-essential-team-git-and-qa-rules) · [3. Existing progress](#3-bring-existing-progress-into-bmad) · [4. Setup](#4-install-and-check-the-tools-once) · [5. Start an issue](#5-start-one-issue-correctly).

**Deliver the task:** [6. Build](#6-build-with-bmad) · [7. Test](#7-test-it-and-keep-the-evidence) · [8. Review / walkthrough](#8-understand-and-review-the-change) · [9. Open a PR](#9-open-the-pr-and-handle-feedback) · [10. Finish and sync](#10-finish-and-choose-the-next-task).

**Keep for reference:** [11. Daily checklist](#11-checklist-for-every-story) · [12. Commands](#12-command-guide-what-to-ask-for-and-when) · [13. Important files](#13-important-project-files-and-reading-order) · [14. Troubleshooting](#14-if-something-fails) · [15. All owners and task IDs](#15-who-owns-each-epic-and-story) · [16. Sources](#16-where-this-tutorials-instructions-come-from).

## 2. Essential team Git and QA rules

This section carries the team's [Git and QA guidelines](git_qa_guidelines.pdf) into the AttendTa/BMAD workflow, with the workspace adjustments explained below. Use it alongside the practical steps in this tutorial. It updates the written guidance here; it does not claim that GitHub branch protections, CI jobs, or test tools have already been configured.

### Branches and PRs

- **`main`:** reviewed release/submission code. Release to it through a PR after the integrated checks pass.
- **`dev`:** the team's integration branch. Feature and bug-fix PRs target it.
- **`feature/*`:** one focused story or agreed small task. Include the GitHub issue number when useful, such as `feature/gh-36-registration-review`.
- **`fix/*` or `bugfix/*`:** a focused bug fix, linked to the relevant issue.
- Do not push directly to `main` or `dev`. Require successful applicable checks and at least one peer approval before merging. The author does not supply their own peer approval; an AI review does not supply it either.
- Never force-push to shared `main` or `dev`, or use a hard reset as a routine way to resolve a confusing Git state. Ask the maintainer for help. For feature-branch history changes, coordinate with anyone using that branch first.

**Maintainer setup:** protect `main` and `dev` with rules requiring PR review and the agreed CI checks, and disallow force pushes and branch deletion there. Confirm the actual settings in GitHub. A rule written in this document is not enforced automatically.

### Keep your feature branch current using fresh remote data

The original PDF's `git merge dev` example can merge an old local copy of `dev`. Fetch first and merge the correct remote branch. For a direct clone whose `origin` is the team repository, from your feature branch with local work safely committed:

```powershell
git fetch origin
git merge origin/dev
```

For this workspace's `upstream` arrangement:

```powershell
git fetch upstream
git merge upstream/dev
```

Choose the example matching `git remote -v`; do not run both blindly. Resolve conflicts by understanding both changes, ask the other owner when behavior is unclear, and rerun the affected tests. Do not accept one side of every conflict just to make Git finish. These update commands do not authorize pushing to a disabled remote.

### Review and stage only this task's files

Use small, meaningful commits. Before staging, inspect both local changes and untracked files:

```powershell
git status --short
git diff
```

Stage selected changes with `git add -p`, or use `git add --` followed by the specific paths you reviewed. Then inspect the staged result:

```powershell
git diff --cached --stat
git diff --cached
```

The PDF's `git add .` is not the default recommendation for this workspace: there may be unrelated BMAD files, plans, local configuration, or another task's changes. Include the relevant shared story/plan updates, but exclude secrets, generated build output, personal configuration, and unrelated work. BMAD's local commits need the same scope review.

Use Conventional Commits:

```text
feat: add registration review with seat checks
fix: refuse duplicate attendance
docs: explain the BMAD issue workflow
test: cover competing certificate issuance
refactor: reuse the shared cancellation service
chore: configure the CI build
style: format the venue views
```

Use `feat`, `fix`, `docs`, `style`, `refactor`, `test`, and `chore` with a short description of what the commit does. A `style` commit changes formatting, not application behavior. Inspect the commit produced by an assistant before pushing it.

### The three QA tiers, with clear timing

| Tier | When | Who | Required evidence |
|---|---|---|---|
| **1. Local checks** | During development and before pushing/requesting review | Author | The solution builds; applicable tests and configured formatting/static checks pass; manual checks from the plan are recorded. |
| **2. PR checks and peer review** | When the PR opens and after relevant updates | CI and a teammate | The agreed CI jobs pass, the diff is reviewed, comments are resolved, and at least one peer approves. Run feature-specific checks on the branch before merge where possible. |
| **3. Integrated checks** | On merged team `dev`, before release to `main` | Team, QA, and lead | Cross-module flows, end-to-end scenarios, schema setup/change verification, and release-specific checks pass on the integrated build. |

Tier 1 replaces the ambiguous “Pre-Commit” heading: check throughout development and before review. Frequent local commits do not replace checks before delivery. A small documentation change needs appropriate document/link checks; a seat-allocation change needs real database and concurrency checks. Apply the checks that prove the task, and record any limits.

**What is available now versus planned:** the basic `dotnet restore` and `dotnet build` commands are in section 7. Discover actual test project files before invoking them. In this checkout, the shared test projects and GitHub Actions baseline from **story `1.10` / [issue #9](https://github.com/rainyDayssss/AttendTa/issues/9)** are still planned, and `.github/workflows/` is absent. Story **`1.2` / [issue #10](https://github.com/rainyDayssss/AttendTa/issues/10)** supplies the planned SQL fixture. Verify the current target branch, since another developer may have delivered them after this guide was written.

Do not claim “green CI” when no CI job ran, or “lint passed” when no lint tool is configured. The maintainer needs to supply and document the required jobs/tools and their exact commands. If a required check is unavailable, record the blocker and resolve the setup or obtain an explicit, documented team decision; do not silently remove the check or label an unverified feature ready. Local output is useful evidence, but it is not a GitHub Actions run.

**Database checks:** AttendTa's planned approach is database-first SQL scripts and EF mappings, without EF migrations. Read the PDF's “database migrations” tier as **schema setup/change verification** here. Test the canonical script on an isolated allowed database, mapping correctness, required seeds/constraints, rollback, and relevant concurrency cases. Never reset shared or production data to make a test pass.

### Minimum PR and acceptance record

Every feature PR should identify the BMAD story and GitHub issue, describe the behavior delivered, link any prerequisite PRs, and include actual local/CI/manual results. State missing integration work. Attach safe screenshots when a visible change needs them. A previously written plan or old test count is not fresh verification evidence.

Keep the sequence explicit: **existing issue → readiness/assignment → approved plan → implementation and local checks → PR checks and peer review → merge to `dev` → integrated verification → accepted issue state and BMAD sync**. Use the existing-progress procedure in section 3 when code predates the ticket. Do not mark stories done because an assistant says `built`, a PR was merely opened, or a different repository contains similar code.

After merge, clean up the remote feature branch when no other work depends on it. First ensure its commits are included in the intended target and the shared plan/handoff evidence is retained. Do not delete unmerged dependent work or remove BMAD plans as branch cleanup. Release from `dev` to `main` through a separate reviewed PR after Tier 3.

## 3. Bring existing progress into BMAD

**Do not start over just because a new ticket says backlog.** BMAD planning may have been written without access to your feature branch, uncommitted changes, earlier PR, or test results. Publishing a story as backlog did not inspect every teammate's private work. First bring the existing work and the ticket into agreement.

This matters in AttendTa: the earlier [ADM-01 handoff](implementation-plans/ADM-01_Phase1_Handoff.md) and [CERT-01 handoff](implementation-plans/CERT-01_Phase1_Handoff.md) describe implemented foundations on feature branches, with later database and HTTP integration still pending. Those are records of earlier work, not proof that the whole corresponding BMAD story is complete or present in today's `dev`.

### A. Preserve and identify your work

Before switching branches or running a fresh build, collect:

- Your repository URL and branch name.
- The relevant commit hashes and PR URL, if any.
- Whether each PR is open, closed without merge, or merged, and its target branch.
- Uncommitted or untracked files that belong to the feature.
- The plan or handoff you followed, plus actual test commands and results with their dates.

Safe inspection commands include:

```powershell
git status --short --branch
git remote -v
git branch -a
git log -8 --oneline
git diff --stat
```

Untracked files do not appear in `git diff`; read `git status` too. Save local work before branch changes. Do not use `git reset --hard`, delete a branch, or overwrite a folder to match the new ticket. Do not merge or cherry-pick old work automatically. The owner and reviewer must first decide what is safe to reuse.

### B. Ask for a comparison before implementation

Send this in the AI chat, filling in the actual values:

```text
I already made progress before these BMAD tickets were written.
Repository: [repository URL]
Branch and commits: [branch name and hashes]
PR: [URL, or no PR yet]
Local changes: [paths, or none]
Earlier plan/handoff: [path]
Relevant BMAD stories and GitHub issues: [IDs and URLs]

Use bmad-agent-po to compare this existing work with the current stories.
Inspect the available code and PR evidence first. If needed, use bmad-deep-recon
to understand the existing implementation, and bmad-code-review to review it.
Show what is implemented, what is tested, what is merged into team dev,
what is reusable but needs changes, and what is still missing.
Propose the remaining work and tracking updates for my review.
Do not rebuild the feature, discard changes, create duplicate issues,
or mark a story done based only on an old plan or test count.
```

These are plain-language instructions to existing BMAD skills, not a new “adopt progress” command. If the branch or repository is unavailable, supply access through the normal workflow or give the assistant the relevant diff. It must report what it could not inspect rather than assume the work exists or is missing.

### C. Compare behavior, not just filenames

Ask for a short table like this:

| Story / issue | Existing evidence | Where it exists | Still needed | Proposed next step |
|---|---|---|---|---|
| Your story and issue IDs | Commit, code, PR, and fresh test results | Local changes, feature branch, or merged `dev` | Remaining verification or integration | Continue, adapt, review, or accept after verification |

One old feature can cover parts of several BMAD stories; one BMAD story can require more than the old feature supplied. Map the actual behavior to each story's requirements and verification steps. An interface, a DTO, or a passing pure-rule test does not prove that a database write, authenticated endpoint, or screen exists.

Check older code against current shared decisions too: schema, authentication, transactions, audit logging, private storage, image limits, paging, and framework versions. Keep compatible tested behavior. Agree adaptations with the relevant owner instead of shipping a second cancellation service, audit writer, or file store.

### D. Choose what to do from the evidence

| Actual situation | What to do next |
|---|---|
| Only a design or plan exists | Reuse valid decisions as input; plan and implement the missing code. |
| Code exists only as local changes | Preserve it, review it, and agree how to commit it on the intended feature branch before continuing. |
| Partial implementation exists on a feature branch | Keep the useful work and plan only the remaining changes and checks. Do not call the entire story done. |
| Implementation exists but differs from current rules | Show the differences and agree whether to adapt the code or change the requirements through the product owner. |
| An open PR already delivers the same task | Link the existing PR to the existing issue and continue its review. Avoid opening a competing implementation PR. |
| An earlier PR was closed without merging | Inspect its code and reason for closure; do not treat “closed” as “delivered.” |
| Work was merged to another repository or branch | Verify where the code is available. Arrange its delivery to the team's `dev` through review; it is not integrated merely because a different branch has it. |
| Work is merged to team `dev` but lacks required checks | Run the missing verification and record any follow-up work before acceptance. |
| Work is merged, meets the current story, and passes the required checks | The human confirms acceptance; the product owner updates the existing issue and mirrors the result. No new implementation is needed. |

### E. Update the existing records, then continue

Review the proposed mapping. Ask `bmad-agent-po` to record the accepted evidence, existing PR links, remaining work, and any agreed dependency changes in the existing tickets. Keep IDs stable; a move out of backlog is a tracking update, not a reason to republish the story under another issue number.

If delivery order or requirements must change, ask the product owner to replan with the evidence and your decision. Do not change the sequence only to hide a blocker. Keep completed work and historical decisions visible. Changing a prerequisite can affect other owners, so include those effects in the proposal.

An existing BMAD plan should be resumed from its saved state. For older work with **no BMAD plan**, ask `bmad-build` to inspect the agreed existing implementation and create a plan for only the remaining work. Record the real baseline commit and tests; do not invent an old plan, fake a `built` status, or rewrite history to make it look as if BMAD produced the earlier code.

```text
Use bmad-build for [BMAD story ID and GitHub issue URL].
The accepted existing-work comparison is at [path or ticket reference].
Reuse the compatible work from [branch/commit/PR]. Inspect it before changing it.
Plan only the missing behavior, required adaptations, and verification.
Explain what you will keep and what you will change. Stop for my plan approval.
```

After approval, follow the same test, review, PR, and acceptance steps as a new story. Earlier progress reduces the remaining work; it does not remove the requirement to verify it.

## 4. Install and check the tools once

### A. An AI coding tool that can load the project's skills

Open the **repository root**, the folder containing `ASI.Basecode.sln`, `_bmad`, and `.agents`. In this workspace it is `F:\AttendTaBakcend\AttendTa`; your own clone may have another path.

The skills live in `.agents/skills/`. The `.opencode/commands/` folder contains command wrappers, including some older names. Different AI tools display skill commands differently. The chat examples below name the skill in plain text; select its skill or slash-command entry if your tool provides one.

**Chat prompts go in the AI chat. PowerShell commands go in the terminal.** Typing `bmad-build` into PowerShell does not run the coding workflow.

### B. Git, GitHub CLI, and your own GitHub login

Check in PowerShell:

```powershell
git --version
gh --version
gh auth status
```

If GitHub CLI is missing, install it using the [official GitHub CLI installation instructions](https://github.com/cli/cli#installation), then open a new terminal. Sign in with your own account:

```powershell
gh auth login
gh auth status
```

Your GitHub login is separate from BMAD. It lets the assistant read issues, update assignments, and open PRs when you ask. Repository permissions still apply. Never paste a token or password into a story, chat transcript intended for sharing, commit, or PR.

If you installed `gh` but the terminal cannot find it, restart the terminal and the coding app. Windows may still be using the PATH from before installation. On this machine the executable exists at `C:\Program Files\GitHub CLI\gh.exe`.

### C. Astral's `uv` and Python

`uv` is a tool from **Astral** that runs Python scripts and supplies their required packages. BMAD uses Python scripts to read settings, resolve tickets, and prepare skill instructions. For example, its skill renderer needs the Python package `jinja2`. This is why `uv` is needed even though AttendTa itself is a C# application.

Install using the Windows command listed in [Astral's official installation guide](https://docs.astral.sh/uv/getting-started/installation/):

```powershell
winget install --id=astral-sh.uv -e
```

Open a new terminal, then check and install a compatible Python:

```powershell
uv --version
uv python install 3.12
uv run --python 3.12 python --version
```

This project's renderer requires Python 3.11 or newer. `uv` can download the runtime and packages; the first run may need internet access to the Python download service and PyPI. If your AI tool blocks downloads in its sandbox, allow the specific required download through the tool's normal approval flow. Do not turn off firewall or certificate checks. Installing `uv` alone does not make those network permissions available.

### D. .NET SDK, SQL Server, and tools required by your story

Install the **.NET SDK**, which builds code, rather than only a runtime, which runs already-built code. Check:

```powershell
dotnet --list-sdks
dotnet --list-runtimes
```

Use the [official Windows .NET setup instructions](https://learn.microsoft.com/en-us/dotnet/core/install/windows). The project files in this checkout currently target `net9.0`; story `1.10` moves them to `net10.0`. Check the actual project files in the branch you are building. Before that upgrade, use a compatible .NET 9 environment; after it, use the .NET 10 SDK and ASP.NET Core runtime. The local `DOTNET_ROLL_FORWARD=LatestMajor` note is a temporary machine-specific workaround for the old target, not the upgrade itself.

Database work needs the team's agreed SQL Server setup and an isolated database. Follow the canonical scripts and the prerequisite story's handoff. Never run a reset script on a teammate's database or a shared server. Use local environment variables or user secrets for connection settings.

Other tools are story-specific: Node.js and the local Tailwind build for `1.9`, SQL Server test infrastructure from `1.10`/`1.2`, Playwright browser setup from `6.1`, and camera/HTTPS setup for attendance and release checks. Do not install every optional tool before reading your story.

### E. Check BMAD and select the existing initiative

In the AI chat:

```text
Use bmad status to check this repository's installed skills.
Then use bmad to select the existing initiative-attendta.
Do not create a new initiative or regenerate the existing stories.
```

If setup is missing, use `bmad setup` in the AI chat. If the skills themselves are absent, first check that your checkout includes them and your AI tool loads that folder. The installed BMAD help gives `npx skills add bmad-code-org/BMAD-METHOD` as an installation option; that requires Node.js/npm. Coordinate installation or upgrades so everyone uses the intended project version.

Each developer's active initiative is a personal setting in `_bmad/custom/config.user.toml`. Selecting it on one person's PC does not select it on everyone else's PC. The shared GitHub store is configured in [`_bmad/custom/ticketing-store-config.toml`](../_bmad/custom/ticketing-store-config.toml).

Before teammates rely on this guide, the maintainer needs to share the guide, store configuration, and published BMAD story files through the normal PR process. Files existing only on one PC are not available to someone else's clone.

## 5. Start one issue correctly

### Step 1: Read it and check whether you can start

Use the task tables below and [GitHub Issues](https://github.com/rainyDayssss/AttendTa/issues). Read the description, verification steps, blockers, and any comments. Confirm that nobody else is already implementing it.

Example terminal command for story `3.7`:

```powershell
gh issue view 36 --repo rainyDayssss/AttendTa
gh issue view 36 --repo rainyDayssss/AttendTa --json number,title,state,assignees,blockedBy
```

Ask in the AI chat:

```text
Use bmad-agent-po for the existing initiative-attendta.
Sync GitHub issue https://github.com/rainyDayssss/AttendTa/issues/36 and its prerequisites.
It is BMAD story 3.7. Explain whether it is ready and what must be merged first.
Do not create a duplicate issue and do not start implementation yet.
```

A prerequisite can be in review in BMAD while its code is still unavailable in `dev`. For work that needs those changes, wait for the merge or agree an explicit branch arrangement with the owner. A closed issue alone is not proof the required code is in your branch. Read the prerequisite's PR and handoff.

If you are blocked, record which issue or decision you need and choose another ready task with the team. Never remove a real dependency to make the board look ready.

### Step 2: Create your feature branch

First inspect your checkout:

```powershell
git status --short --branch
git remote -v
git fetch --all --prune
```

The team repository is `rainyDayssss/AttendTa`. Its integration branch is **`dev`** and its default branch is `main`. This workspace is on **`development`** in `Wynnwrath/AttendTav2`; here, `origin` is that repository and `upstream` points to the team repository with pushes disabled. Do not assume your remotes match this PC.

For a direct clone where `origin` is the team repository, after your working tree is clean:

```powershell
git switch dev
git pull --ff-only origin dev
git switch -c feature/gh-36-registration-review
```

For this workspace's arrangement, after your working tree is clean:

```powershell
git fetch upstream
git switch -c feature/gh-36-registration-review upstream/dev
```

These commands are examples for issue #36, not commands to run for every task. Use your issue number and a short meaningful branch name. If the branch already exists, switch to it rather than creating it again. If `git status` shows changes, inspect and preserve them before switching. Do not discard another task's files or commit unrelated files to get past BMAD's clean-tree check.

### Step 3: Claim and start the existing ticket

In the AI chat, replacing the account placeholder with your real GitHub username:

```text
Use bmad-agent-po. For existing BMAD story 3.7 / GitHub issue #36,
assign it to MY_GITHUB_USERNAME and start it if its prerequisites are ready.
Sync the GitHub result into the local BMAD ticket. I will build only this story.
```

The product owner changes the existing issue's state/labels and mirrors the result locally. GitHub and the Markdown files **do not sync automatically**. Request a sync when starting work, after remote changes, and when finishing. Do not run “publish all tickets” again; these stories already have GitHub issue IDs.

Sync may change the local story file. If this leaves the working tree dirty, review and commit only that intended tracking update, or coordinate it with the maintainer, before starting `bmad-build`. Do not hide tracking changes or bypass the clean-tree check.

## 6. Build with BMAD

Start a fresh AI chat for the story when possible. Give it the repository and exact task:

```text
Use bmad-build for BMAD story 3.7 in initiative-attendta:
https://github.com/rainyDayssss/AttendTa/issues/36

Read the local story, its epic, and the relevant architecture decisions.
Check the branch and prerequisite code first. Work only on this story.
Make a plan with the tests required by its verification steps.
Use plain language and explain unfamiliar words. Stop for my plan approval.
```

This is an example, not a recommendation that everyone start at `3.7`. Choose a ready story from your own module. On a newly started project, foundational story `1.10` is first.

### What happens next

1. The assistant resolves the existing story and its plan location.
2. It reads the story and epic, checks the branch and working tree, and studies the relevant code.
3. It writes a plan beside the story's `tickets.toml`.
4. It shows the plan and any questions that need an answer.
5. You read the plan before approving. Check the intended behavior, touched areas, dependencies, and tests.
6. Say **“Approve and continue”** to implement, or **“Approve and stop”** to leave the plan ready for a later session. You can ask to review or change the plan first.
7. It implements, runs the planned checks, reviews the changes, and reports the result.

If the plan tries to implement several unrelated stories, ask it to keep this story's scope. If the ticket is wrong, use `bmad-agent-po` to update the existing task and its dependencies; do not quietly change the feature's meaning inside the implementation plan.

The installed build workflow can make a **local commit**, and it does **not push automatically**. Earlier Pinca-specific guidance says commit only when requested, so make your intent explicit if needed: “You may commit only this story's reviewed changes; ask before pushing.” Tell the assistant about any local rule that changes the expected workflow.

For risky work, you can ask for a deeper review:

```text
Use bmad-build for story 3.7 with a thorough review.
Include real SQL Server tests for competing last-seat approvals.
```

The normal local default is a quick review. A deeper review costs more time; use it for a reason. Running a review does not replace the story's required tests.

### Continue tomorrow without starting over

Keep the story's plan file. In a new chat, give its actual path:

```text
Use bmad-build to resume the existing plan at PATH_TO_THIS_STORY_PLAN.
Read its saved status and continue from there. Do not create another story or plan.
```

Ask the assistant for the plan path in its handoff. Its state controls what happens next: a draft still needs planning; a ready plan can be implemented; `built` can receive a follow-up review. If the plan is blocked, resolve the recorded reason with the product owner first.

## 7. Test it and keep the evidence

Read the story's `Verify:` section and the plan's test list. Ask what each result proves. A successful build proves that the code compiles; it does not prove login permissions, seat limits, or certificate behavior.

From the repository root, these are the basic application commands:

```powershell
dotnet restore ASI.Basecode.sln
dotnet build ASI.Basecode.sln --no-restore
dotnet run --project "ASI.Basecode.WebApp/ASI.Basecode.WebApp.csproj"
```

Use the full WebApp project filename because older backup project files can make folder-based commands ambiguous. The running application needs the right local configuration and database. Read the address printed by the app rather than guessing its port.

### Test choices

| Check | What it helps prove | AttendTa example |
|---|---|---|
| Unit test | A rule or calculation works with known inputs | Last administrator cannot be removed; a report handles a zero denominator |
| SQL integration test | The code and real database work together | Duplicate registration is rejected; a failed transaction leaves no partial write |
| Concurrency test | Competing requests cannot break a shared limit | Two officers approve the final seat: only one participant gets it |
| HTTP/API test | Routes, input validation, permissions, and responses work | A member cannot call an administrator-only action |
| Manual browser test | A person can use the feature | Required-field errors appear and a successful action shows the expected page |
| End-to-end test | A whole user path works | Account approval → login → signup → attendance → certificate |

Find which test projects actually exist:

```powershell
Get-ChildItem tests -Recurse -Filter *.csproj
```

Then run the relevant project by its full path. After stories `1.10` and `1.2` supply the planned test projects and SQL fixture, the intended commands include:

```powershell
dotnet test "tests/ASI.Basecode.Services.Tests/ASI.Basecode.Services.Tests.csproj"
dotnet test "tests/ASI.Basecode.IntegrationTests/ASI.Basecode.IntegrationTests.csproj"
```

Those two project files are **not present in this checkout at the time of writing**. The integration fixture is planned to use `ATTENDTA_TEST_SQL`; use the actual merged fixture's setup instructions and a private local connection value. A folder name or an older handoff's test count is not proof that tests exist or ran. If there is no test project, report that gap rather than presenting `dotnet test` with zero discovered tests as a pass.

For manual API checks, the existing [Bruno collection](../bruno) includes login and Master Data requests. Check their target URL and payloads against the code you are testing. They are not a full test suite for every module.

### Useful test prompts

```text
Before finishing story 3.7, run the checks in the story and approved plan.
Test the normal case, invalid input, unauthorized access, the exact time cutoff,
and competing approvals on a real SQL Server database where required.
Report each command, its actual result, and anything you could not run.
```

For an already-working UI or API that needs automated user-path tests:

```text
Use bmad-qa-generate-e2e-tests for the existing feature delivered by story 4.5.
Use the project's current test setup. Cover successful manual check-in,
duplicate attendance, wrong event, and unauthorized access as the ticket requires.
```

This optional skill writes tests for existing features. It is not a substitute for the unit and database tests needed during implementation. Keep a test that exposes a bug; fix the bug rather than weakening the expected result.

### What to record

Record the commands, pass/fail counts when available, the database/environment used, and manual steps with expected and actual results. State missing checks honestly. Use test data and never include real passwords, hashes, QR tokens, private connection strings, or actual member records in screenshots or PR notes.

## 8. Understand and review the change

### Use `bmad-walkthrough` when you want to learn the code

Before opening a PR, you can review the commit:

```text
Use bmad-walkthrough on my latest commit for story 3.7.
Explain the purpose, the important files, and the flow in simple words.
Take one part at a time. Let me inspect or test it before moving on.
```

After opening a PR, you or a reviewer can pass its actual URL:

```text
Use bmad-walkthrough on THIS_PR_URL.
Start with the issue it solves. Explain the main code path, then the tests.
Ask me to check each part. Do not change code unless I ask.
```

A walkthrough is a guided **human review**. The assistant explains and keeps track of what you reviewed. It is useful before or after PR creation, but it does not itself merge the PR, grant peer approval, run every test, or mark the ticket done.

### Use `bmad-code-review` for a separate code review

```text
Use bmad-code-review on THIS_PR_URL with a quick no-code review.
Compare it with BMAD story 3.7 and its plan. Explain each finding and its impact.
```

`no-code` asks for findings to hand off rather than applying fixes in the review session. If fixes are needed, decide which findings to address, make the changes on the same feature branch, and rerun the checks affected by those changes. Choose a thorough review when the change's risk justifies it. A fresh thorough review immediately after an unchanged thorough build review is usually unnecessary.

For a fresh follow-up review while a build plan is still `built`, give that plan to `bmad-build`. For reviewing another person's PR or already-done work, use `bmad-code-review`.

## 9. Open the PR and handle feedback

Inspect what you will submit:

```powershell
git status --short --branch
git diff --stat
git log -3 --oneline
```

If BMAD has already committed your work, the working-tree diff can be empty. Check the branch's committed changes against `dev` too; an empty working-tree diff does not mean the PR has no changes. Include the relevant story plan and tracking updates, while keeping unrelated work and personal settings out.

For a direct clone with permission to push to the team repository:

```powershell
git push -u origin feature/gh-36-registration-review
gh pr create --repo rainyDayssss/AttendTa --base dev --head feature/gh-36-registration-review --web
```

In this workspace, `origin` is a different repository and `upstream` pushes are disabled. Do not copy that push command without checking the remotes. Agree whether to use a branch on the team repository or an allowed cross-repository PR. Do not enable a disabled push remote just to bypass the arrangement. The GitHub website can also open a PR after the feature branch is pushed.

Or ask the assistant, giving it the actual destination:

```text
Prepare a PR for story 3.7 / issue #36 against rainyDayssss/AttendTa:dev.
Check the remotes and branch first. Show the title, description, test evidence,
and issue link. Push the intended feature branch and open the PR when ready.
```

The PR description should say:

```text
Implements BMAD story 3.7: registration review and safe seat allocation.
Related issue: rainyDayssss/AttendTa#36.

Behavior: [what now happens when an officer approves or declines a request]
Verification: [actual commands, results, and manual checks]
Dependencies: [required merged work or explicit remaining blockers]
Remaining limits: [anything not verified or intentionally outside this story]
```

Link the issue explicitly. GitHub's automatic issue-closing behavior depends on the target/default branch; the repository default is `main` while feature PRs target `dev`. Do not rely only on `Closes #36` to update completion when merging to `dev`. Use the team's explicit verification and sync step below.

After PR creation, you can use `bmad-walkthrough` to learn or explain it, or ask a peer to review it. Check CI, answer review comments, and rerun affected tests after fixes. An AI review supplements the team's requirement for a peer approval. Follow the agreed merge rules; do not merge directly to `main` as part of a normal feature task.

## 10. Finish and choose the next task

After the PR is merged to `dev`, verify the expected behavior on the integrated build. Then tell the product owner exactly what was accepted:

```text
Use bmad-agent-po. Story 3.7 / GitHub issue #36 was merged in THIS_PR_URL.
The integrated checks passed: [give actual evidence].
Mark this existing issue done and sync its GitHub state into BMAD.
Check for any remaining local plan-status differences and report them.
Then show which of my stories are ready next; do not start one automatically.
```

The build's `built` status means its implementation/review process finished. The human decides completion. On the GitHub store, use the product owner to update the issue and mirror the remote state. Do not use the repo-only `tickets.py mark` command as a substitute; it refuses tracker stores. Remote completion does not automatically rewrite every build-plan status.

Keep the story files, plans, review evidence, and handoffs. They help the next developer understand what exists. Closing every story does not automatically accept an epic: when its stories are finished, ask for:

```text
Use bmad-retrospective on epic-check-in in initiative-attendta.
Compare the finished stories with the epic's Done when requirements.
Report gaps and suggested follow-up work. Do not close the epic automatically.
```

## 11. Checklist for every story

- [ ] I know my BMAD story ID and GitHub issue number.
- [ ] I read the story, epic, blockers, and relevant shared decisions.
- [ ] If I already had progress, it was compared with the current ticket and the remaining work was agreed.
- [ ] The required code is available in my branch and no teammate is duplicating my task.
- [ ] I have the intended feature branch and a clean starting tree.
- [ ] My issue is claimed and its current GitHub state has been synced.
- [ ] I read and approved the implementation plan.
- [ ] The story's required tests ran, and missing checks are recorded honestly.
- [ ] I checked fresh remote `dev` changes and staged only the intended task files.
- [ ] I can explain the change; I used a walkthrough if needed.
- [ ] My PR targets team `dev`, links the issue, and includes actual test evidence.
- [ ] Review comments and CI failures are addressed, and a peer approved it.
- [ ] After merge and integrated verification, the product owner synced the accepted result.

## 12. Command guide: what to ask for and when

These are **AI chat requests**, not standalone terminal programs. A working example is “Use bmad-build for story 4.5 in initiative-attendta.”

| Skill | Use it when | What to expect |
|---|---|---|
| `bmad status` | You need to check installation | Installed/missing/outdated skill information |
| `bmad setup` | Scripts or module setup are missing | Setup or repair instructions; inspect any proposed changes |
| `bmad` | You need help or to select the initiative | Advice from installed help or a personal initiative selection |
| `bmad-agent-po` | You need readiness, assignment, state changes, sync, or a corrected ticket | Changes to the existing ticket tree and configured GitHub tracker as requested |
| `bmad-build` | You are implementing one story with a human present | Plan checkpoint, implementation, tests, review, and local handoff |
| `bmad-walkthrough` | You want to understand or personally review a commit/PR | A guided review, one part at a time |
| `bmad-code-review` | A PR/diff needs another bug and requirements check | Findings, and selected fixes or a no-code handoff |
| `bmad-qa-generate-e2e-tests` | An existing feature needs API/user-path test coverage | Tests following the current project setup |
| `bmad-retrospective` | An epic's implementation is finished | An epic-wide acceptance review and follow-up suggestions |
| `bmad-advanced-elicitation` | A plan needs deeper questioning | Help finding missing decisions before approval |
| `bmad-party-mode` | A design choice benefits from several viewpoints | A discussion; its opinions are not test evidence |
| `bmad-project-context` | Team instructions have drifted from the code | A coordinated review/update of agent instructions, with human approval |
| `bmad-build-auto` | An experienced maintainer intentionally sets up unattended work | One unattended task; unclear work halts as blocked. It needs extra setup and subagent support. It is not the beginner path. |

Some `.opencode` wrappers still name older workflows such as `bmad-create-epics-and-stories`, `bmad-sprint-planning`, `bmad-spec`, and `bmad-correct-course`. This installed version's help routes those planning/ticket duties through **`bmad-agent-po`**. Use the installed skills rather than assuming an old command from a video or wrapper is present.

### Optional terminal checks for the local BMAD tree

These inspect local files; they do not fetch GitHub state by themselves:

```powershell
uv run _bmad/method/scripts/tickets.py status _bmad-output/initiative-attendta
uv run _bmad/method/scripts/tickets.py find _bmad-output/initiative-attendta 3.7
```

On the GitHub store, ask the product owner to sync before asking what is ready. Do not add `--synced` to a readiness command unless a real sync just happened. `tickets.py pull` creates a local story file; it does not mean “pull from GitHub,” and `git pull` only updates Git files.

## 13. Important project files and reading order

### The two planning guides for choosing work

1. [Module, epic, and story ownership](../_bmad-output/initiative-attendta/module-epic-story-ownership.md): **who owns what**, including shared work and the copied sequence waves.
2. [BMAD dependency graphs](../_bmad-output/initiative-attendta/dependency-graphs.md): **what must come first**, with story diagrams, start waves, and the critical path. It is a snapshot labelled with its source commit, so also check the current ticket and GitHub blockers.

A wave groups work that could start at the same stage if the prerequisites are available. It is not a deadline, proof of completion, or permission to start every task in that row. Story IDs are identifiers, not execution order: `1.10` comes before `1.2`, and `3.15` comes before `3.7`.

### Files to open when building

| File | What you use it for |
|---|---|
| [Initiative](../_bmad-output/initiative-attendta/initiative-attendta.md) | Project outcome, shared requirements, limits, and known conflicts |
| [Epic index](../_bmad-output/initiative-attendta/tickets/tickets.toml) | The epics and their planned order |
| Your epic's Markdown file and `tickets/tickets.toml` | Requirements, story details, and prerequisites for your module; links are in the task tables below |
| Your `story-*.md` | The published task's local working copy; includes `tracker_id`, `remote`, `after`, and verification details |
| Your `story-*-plan.md`, once created | What the assistant will change, the checks to run, progress, and evidence |
| [Architecture decisions](../_bmad-output/initiative-attendta/architecture-attendta/architecture-attendta.md) | Shared rules for transactions, data ownership, authorization, storage, time, and tests |
| [PRD](../_bmad-output/initiative-attendta/prd-attendta/prd-attendta.md) and [addendum](../_bmad-output/initiative-attendta/prd-attendta/addendum.md) | Product rules, delivery ownership, and agreed changes |
| [System specification](AttendTa-System-Specification.md) | Detailed feature behavior and acceptance scenarios |
| [Application sequence diagrams](AttendTa-Sequence-Diagrams.md) | How requests move through the application; these are different from the development-order diagrams |
| [Root user stories](../AttendTa-User-Stories.md) | User-facing scenarios and expected behavior |
| [UX design](../_bmad-output/initiative-attendta/ux-attendta/DESIGN.md) and [experience guide](../_bmad-output/initiative-attendta/ux-attendta/EXPERIENCE.md) | Screen design and user flows |
| [AGENTS.md](../AGENTS.md) | Instructions the coding assistant reads in this repository |
| [Canonical SQL script](../database/attendta-mvp-schema.sql) | Database setup source; coordinate changes with its owner |
| [Coding standards](CODING_STANDARDS.md) and [integration guide](INTEGRATION_GUIDE.md) | Existing guidance for Pinca's slices, useful for coordination; they are not automatically approved team-wide rules |
| [Older phased-plan index](implementation-plans/README.md) | Pinca's earlier ADM/CERT/REP plans and handoffs; check their dates, phase, and superseded notices before reusing code or decisions |

The builder should start from its story and epic and open linked material as needed. You do not need to paste every document into every chat.

**Known differences in this checkout:** `AGENTS.md` describes a .NET 9/Bootstrap starter and a Master Data assignment, while later BMAD decisions plan .NET 10/Tailwind and all modules. Some source links still say `docsss/`, which is absent here; corresponding documents exist under `docs/`. `docs/database-setup.md` is mentioned by older instructions but is also absent here. Ask the assistant to show a conflicting rule and its source before changing behavior. A feature owner should not silently rewrite shared rules to make their own story easier.

## 14. If something fails

| Problem | What to do |
|---|---|
| `uv` or `gh` is not recognized | Check installation, open a fresh terminal, and restart the AI app to refresh PATH. |
| PyPI download retries / socket access denied | Identify the required package and request the specific network access in the AI tool. A blocked BMAD renderer is not proof the skill is absent. |
| BMAD says no initiative is active | Select the existing `initiative-attendta` using `bmad`. |
| Skill missing or old wrapper fails | Run `bmad status`; use the installed product-owner/build skill, and coordinate setup if necessary. |
| GitHub update fails | Check `gh auth status`, intended repository, and your permissions. Do not use someone else's credentials. |
| “Working tree is dirty” | Inspect changes. Commit only intended work or preserve it with an agreed method before continuing. |
| Wrong branch or remote | Check `git status --short --branch` and `git remote -v`; use the team's `dev` branch as the feature base. |
| Prerequisite not merged | Contact its owner, record the blocker, and agree another ready task or an explicit branch arrangement. |
| Mentioned test project/setup file is absent | Check the prerequisite story and actual files. Report the missing setup rather than inventing a successful test. |
| Build says `built` but GitHub still says backlog | Request product-owner sync/state update; there is no automatic background synchronization. |
| Conflicting rules or missing `docsss/` link | Show the conflicting sources and ask the owner to settle the rule; use verified existing paths for navigation. |

## 15. Who owns each epic and story?

The tables below follow the WBS module allocation and BMAD's existing ownership notes. A module owner is not necessarily already a GitHub assignee. At publication, ten issues were assigned to the two confirmed accounts; other personal account matches and shared work still need team confirmation. Check GitHub for current assignments.

| Member | Module owner | Main epics |
|---|---|---|
| 1 | Gabutan, Jerhon Clyde (`LucielCienAmir`) | AUTH; authentication part of Epic 1 |
| 2 | Taguiam, Jasper | MD; Epic 2 and shared pager |
| 3 | Sinco / Singco, John Paul | EVT/REG; Epic 3 |
| 4 | Talisic, Jhon Rosell (`rainyDayssss`) | ATT/QR; Epic 4 |
| 5 | Pinca | ADM/CERT/REP; Epics 5–7 and administrator setup |

The WBS uses “Sinco”; other project notes use “Singco.” The task titles below come from the existing BMAD entries. Shared assignments are marked explicitly rather than guessed. Use the linked epic for the full requirements and the GitHub issue for the published task.

### Epic 1 — Platform and authentication (AUTH-01–03)

[Epic requirements](../_bmad-output/initiative-attendta/tickets/epic-platform-access/epic-platform-access.md) · [Story definitions](../_bmad-output/initiative-attendta/tickets/epic-platform-access/tickets/tickets.toml)

| BMAD ID | GitHub issue | Task | Owner / shared part |
|---|---|---|---|
| 1.10 | [#9](https://github.com/rainyDayssss/AttendTa/issues/9) | Retarget solution to net10.0 LTS, remove legacy packages, stand up xUnit v3 test projects and CI container baseline | Shared platform; individual assignee to agree |
| 1.2 | [#10](https://github.com/rainyDayssss/AttendTa/issues/10) | Canonical database schema update and EF Core mapping for all ten tables with lookup foreign keys | Shared platform; individual assignee to agree |
| 1.1 | [#11](https://github.com/rainyDayssss/AttendTa/issues/11) | Provisioned administrator logs in by email via Argon2id into authenticated session with active role checks | Gabutan: authentication; Pinca: administrator setup |
| 1.9 | [#12](https://github.com/rainyDayssss/AttendTa/issues/12) | Compiled Tailwind CSS pipeline and role-routed layout shell replacing Bootstrap | Shared platform; individual assignee to agree |
| 1.3 | [#13](https://github.com/rainyDayssss/AttendTa/issues/13) | Caller-owned transaction unit of work and append-only Activity Log writer | Shared platform; individual assignee to agree |
| 1.11 | [#14](https://github.com/rainyDayssss/AttendTa/issues/14) | Master-data repositories and services write inside the caller-owned transaction | Shared platform; individual assignee to agree |
| 1.4 | [#15](https://github.com/rainyDayssss/AttendTa/issues/15) | Asia/Manila timezone display helper and UTC server-side time standard | Shared platform; individual assignee to agree |
| 1.5 | [#16](https://github.com/rainyDayssss/AttendTa/issues/16) | Public Account Request submission with field validation and Argon2id hash | Gabutan |
| 1.6 | [#17](https://github.com/rainyDayssss/AttendTa/issues/17) | Shared 25-row pager helper and Management Account Request queue | Gabutan: queue; Taguiam: pager |
| 1.7 | [#18](https://github.com/rainyDayssss/AttendTa/issues/18) | Management Account Request approval and rejection in caller transaction with audit | Gabutan; Taguiam supplies Member Profile creation |
| 1.8 | [#19](https://github.com/rainyDayssss/AttendTa/issues/19) | Refactor sweep | Shared platform; individual assignee to agree |


### Epic 2 — Master Data (MD-01–05): Taguiam

[Epic requirements](../_bmad-output/initiative-attendta/tickets/epic-master-data/epic-master-data.md) · [Story definitions](../_bmad-output/initiative-attendta/tickets/epic-master-data/tickets/tickets.toml)

| BMAD ID | GitHub issue | Task | Owner / shared part |
|---|---|---|---|
| 2.1 | [#20](https://github.com/rainyDayssss/AttendTa/issues/20) | Event Type management with required description, audit logging, and paged Tailwind list | Taguiam |
| 2.2 | [#21](https://github.com/rainyDayssss/AttendTa/issues/21) | Shared IPrivateFileStore with venue, event, certificate and temporary areas and authorized photo route | Taguiam |
| 2.3 | [#22](https://github.com/rainyDayssss/AttendTa/issues/22) | Venue create and edit with required photo, replacement-on-commit, and capacity guard | Taguiam |
| 2.4 | [#23](https://github.com/rainyDayssss/AttendTa/issues/23) | Venue paged list with photo thumbnails, active toggle, and Tailwind views | Taguiam |
| 2.5 | [#24](https://github.com/rainyDayssss/AttendTa/issues/24) | Active Event Type and Venue read queries for event lifecycle | Taguiam |
| 2.6 | [#25](https://github.com/rainyDayssss/AttendTa/issues/25) | Member profile edit with student-number correction reason and audit | Taguiam |
| 2.7 | [#26](https://github.com/rainyDayssss/AttendTa/issues/26) | Member paged list with search and Tailwind shell | Taguiam |
| 2.8 | [#27](https://github.com/rainyDayssss/AttendTa/issues/27) | Member read-only self profile with ownership authorization | Taguiam |
| 2.9 | [#28](https://github.com/rainyDayssss/AttendTa/issues/28) | Refactor sweep | Taguiam |


### Epic 3 — Events and registration (EVT-01–04, REG-01–05): Sinco / Singco

[Epic requirements](../_bmad-output/initiative-attendta/tickets/epic-events-registration/epic-events-registration.md) · [Story definitions](../_bmad-output/initiative-attendta/tickets/epic-events-registration/tickets/tickets.toml)

| BMAD ID | GitHub issue | Task | Owner / shared part |
|---|---|---|---|
| 3.1 | [#29](https://github.com/rainyDayssss/AttendTa/issues/29) | Draft event creation, edit, and management event list | Sinco / Singco |
| 3.2 | [#30](https://github.com/rainyDayssss/AttendTa/issues/30) | Event Cover upload, replacement, and draft removal via shared storage | Sinco / Singco |
| 3.3 | [#31](https://github.com/rainyDayssss/AttendTa/issues/31) | Event publication with active master revalidation and activity audit | Sinco / Singco |
| 3.4 | [#32](https://github.com/rainyDayssss/AttendTa/issues/32) | Published event limited edits and immutable states | Sinco / Singco |
| 3.5 | [#33](https://github.com/rainyDayssss/AttendTa/issues/33) | Member event browsing and registration submission with academic snapshot | Sinco / Singco |
| 3.6 | [#34](https://github.com/rainyDayssss/AttendTa/issues/34) | Signup deadline enforcement at exact local boundary | Sinco / Singco |
| 3.15 | [#35](https://github.com/rainyDayssss/AttendTa/issues/35) | Registration-owned participation cancellation service (AD-6) | Sinco / Singco |
| 3.7 | [#36](https://github.com/rainyDayssss/AttendTa/issues/36) | Registration review and transactional seat allocation with SQL Server concurrency test | Sinco / Singco |
| 3.8 | [#37](https://github.com/rainyDayssss/AttendTa/issues/37) | Explicit waitlist promotion across full queue | Sinco / Singco |
| 3.9 | [#38](https://github.com/rainyDayssss/AttendTa/issues/38) | Member self-cancellation and re-registration refusal | Sinco / Singco |
| 3.10 | [#39](https://github.com/rainyDayssss/AttendTa/issues/39) | Event start cutoff enforcement and Not confirmed before cutoff status | Sinco / Singco |
| 3.11 | [#40](https://github.com/rainyDayssss/AttendTa/issues/40) | Event cancellation with atomic registration cancellation and check-in closure | Sinco / Singco |
| 3.12 | [#41](https://github.com/rainyDayssss/AttendTa/issues/41) | Event completion closing check-in | Sinco / Singco |
| 3.13 | [#42](https://github.com/rainyDayssss/AttendTa/issues/42) | My Events personal registrations list | Sinco / Singco |
| 3.14 | [#43](https://github.com/rainyDayssss/AttendTa/issues/43) | Refactor sweep | Sinco / Singco |


### Epic 4 — Attendance and QR (ATT-01–03, QR-01): Talisic

[Epic requirements](../_bmad-output/initiative-attendta/tickets/epic-check-in/epic-check-in.md) · [Story definitions](../_bmad-output/initiative-attendta/tickets/epic-check-in/tickets/tickets.toml)

| BMAD ID | GitHub issue | Task | Owner / shared part |
|---|---|---|---|
| 4.1 | [#44](https://github.com/rainyDayssss/AttendTa/issues/44) | On-demand QR Pass PNG rendering and download | Talisic |
| 4.2 | [#45](https://github.com/rainyDayssss/AttendTa/issues/45) | Check-in window toggle with audit logging | Talisic |
| 4.3 | [#46](https://github.com/rainyDayssss/AttendTa/issues/46) | Check-in validation engine and attendance recording | Talisic |
| 4.4 | [#47](https://github.com/rainyDayssss/AttendTa/issues/47) | In-browser camera QR scanner on localhost | Talisic |
| 4.5 | [#48](https://github.com/rainyDayssss/AttendTa/issues/48) | Manual check-in fallback by name and student number | Talisic |
| 4.6 | [#49](https://github.com/rainyDayssss/AttendTa/issues/49) | Attendance invalidation with confirmation and reason | Talisic |
| 4.7 | [#50](https://github.com/rainyDayssss/AttendTa/issues/50) | Member attendance view on My Events | Talisic |
| 4.9 | [#51](https://github.com/rainyDayssss/AttendTa/issues/51) | Refactor sweep | Talisic |


### Epic 5 — Administration (ADM-01–02; MD-02 status changes): Pinca

[Epic requirements](../_bmad-output/initiative-attendta/tickets/epic-administration/epic-administration.md) · [Story definitions](../_bmad-output/initiative-attendta/tickets/epic-administration/tickets/tickets.toml)

| BMAD ID | GitHub issue | Task | Owner / shared part |
|---|---|---|---|
| 5.1 | [#52](https://github.com/rainyDayssss/AttendTa/issues/52) | Console accounts list with role and status badges | Pinca |
| 5.2 | [#53](https://github.com/rainyDayssss/AttendTa/issues/53) | Role assignment with last-active-Administrator app lock | Pinca |
| 5.3 | [#54](https://github.com/rainyDayssss/AttendTa/issues/54) | Upcoming registration impact preview for deactivation | Pinca |
| 5.4 | [#55](https://github.com/rainyDayssss/AttendTa/issues/55) | Account deactivation with registration cancellation cascade and session refusal | Pinca |
| 5.5 | [#56](https://github.com/rainyDayssss/AttendTa/issues/56) | Account reactivation restoring login access only | Pinca |
| 5.6 | [#57](https://github.com/rainyDayssss/AttendTa/issues/57) | Member membership deactivation with preview, reason and cancellation cascade | Pinca; coordinate member screen with Taguiam |
| 5.7 | [#58](https://github.com/rainyDayssss/AttendTa/issues/58) | Member membership reactivation restoring eligibility only | Pinca; coordinate member screen with Taguiam |
| 5.8 | [#59](https://github.com/rainyDayssss/AttendTa/issues/59) | Read-only newest-first paged Activity Log viewer | Pinca |
| 5.10 | [#60](https://github.com/rainyDayssss/AttendTa/issues/60) | Refactor sweep | Pinca |


### Epic 6 — Certificates (CERT-01–02): Pinca

[Epic requirements](../_bmad-output/initiative-attendta/tickets/epic-certificates/epic-certificates.md) · [Story definitions](../_bmad-output/initiative-attendta/tickets/epic-certificates/tickets/tickets.toml)

| BMAD ID | GitHub issue | Task | Owner / shared part |
|---|---|---|---|
| 6.1 | [#61](https://github.com/rainyDayssss/AttendTa/issues/61) | Playwright .NET setup and certificate PDF issuance tracer | Pinca |
| 6.2 | [#62](https://github.com/rainyDayssss/AttendTa/issues/62) | Fixed-template landscape A4 Razor certificate rendering with embedded fonts | Pinca |
| 6.3 | [#63](https://github.com/rainyDayssss/AttendTa/issues/63) | Configure official authorized signatory details | Pinca configures; PM Talisic supplies signatory details |
| 6.4 | [#64](https://github.com/rainyDayssss/AttendTa/issues/64) | Create certificate reviews in event completion transaction | Pinca |
| 6.5 | [#65](https://github.com/rainyDayssss/AttendTa/issues/65) | Attendance invalidation review disqualification and issued-certificate lock | Pinca |
| 6.6 | [#66](https://github.com/rainyDayssss/AttendTa/issues/66) | Management certificate review queue and approval decisions | Pinca |
| 6.7 | [#67](https://github.com/rainyDayssss/AttendTa/issues/67) | Transactional certificate issuance pipeline and audit | Pinca |
| 6.8 | [#68](https://github.com/rainyDayssss/AttendTa/issues/68) | Authorized certificate streaming download endpoint | Pinca |
| 6.9 | [#69](https://github.com/rainyDayssss/AttendTa/issues/69) | Member certificate download links in My Events | Pinca |
| 6.11 | [#70](https://github.com/rainyDayssss/AttendTa/issues/70) | Refactor sweep | Pinca |


### Epic 7 — Participation reports (REP-01): Pinca

[Epic requirements](../_bmad-output/initiative-attendta/tickets/epic-participation-reports/epic-participation-reports.md) · [Story definitions](../_bmad-output/initiative-attendta/tickets/epic-participation-reports/tickets/tickets.toml)

| BMAD ID | GitHub issue | Task | Owner / shared part |
|---|---|---|---|
| 7.1 | [#71](https://github.com/rainyDayssss/AttendTa/issues/71) | Event selector and participation summary KPIs | Pinca |
| 7.2 | [#72](https://github.com/rainyDayssss/AttendTa/issues/72) | Completed-event no-shows and signup snapshot attendee distributions | Pinca |
| 7.3 | [#73](https://github.com/rainyDayssss/AttendTa/issues/73) | Paged participant detail rows with full-dataset totals | Pinca |


### Epic 8 — Release acceptance: shared team work

[Epic requirements](../_bmad-output/initiative-attendta/tickets/epic-release-acceptance/epic-release-acceptance.md) · [Story definitions](../_bmad-output/initiative-attendta/tickets/epic-release-acceptance/tickets/tickets.toml)

| BMAD ID | GitHub issue | Task | Owner / shared part |
|---|---|---|---|
| 8.1 | [#74](https://github.com/rainyDayssss/AttendTa/issues/74) | Demo host runtime environment provisioning | Team; individual assignee to agree |
| 8.2 | [#75](https://github.com/rainyDayssss/AttendTa/issues/75) | Hotspot network binding and trusted HTTPS mobile camera scanning | Team; Gabutan owns hotspot/HTTPS decision |
| 8.3 | [#76](https://github.com/rainyDayssss/AttendTa/issues/76) | Database and persistent file storage backup, restore, and redeploy survival | Team; individual assignee to agree |
| 8.4 | [#77](https://github.com/rainyDayssss/AttendTa/issues/77) | Whole IntegrationTests suite execution on SQL Server | Team; individual assignee to agree |
| 8.5 | [#78](https://github.com/rainyDayssss/AttendTa/issues/78) | Automated SPEC §20 acceptance test suite execution | Team; individual assignee to agree |
| 8.6 | [#79](https://github.com/rainyDayssss/AttendTa/issues/79) | Recorded manual SPEC §20 acceptance checklist verification | Team; individual assignee to agree |
| 8.7 | [#80](https://github.com/rainyDayssss/AttendTa/issues/80) | Full functional demo script walkthrough across all 26 feature IDs | Team; individual assignee to agree |
| 8.8 | [#81](https://github.com/rainyDayssss/AttendTa/issues/81) | Tier 3 manual system integration and regression verification | Team; individual assignee to agree |
| 8.9 | [#82](https://github.com/rainyDayssss/AttendTa/issues/82) | Dev to main release pull request and submission preparation | Team; individual assignee to agree |
| 8.10 | [#83](https://github.com/rainyDayssss/AttendTa/issues/83) | Refactor sweep | Team; individual assignee to agree |

## 16. Where this tutorial's instructions come from

- Local BMAD help: [implementation](../.agents/skills/bmod-method/help/implementation-skills.md), [validation](../.agents/skills/bmod-method/help/validation-skills.md), [ticket setup and sync](../.agents/skills/bmod-method/help/ticketing-setup.md), and [unattended builds](../.agents/skills/bmod-method/help/unattended-builds.md).
- Installed skill instructions: [build](../.agents/skills/bmad-build/SKILL.md), [product owner](../.agents/skills/bmad-agent-po/SKILL.md), [code review](../.agents/skills/bmad-code-review/SKILL.md), and [walkthrough](../.agents/skills/bmad-walkthrough/SKILL.md).
- Team planning references: [WBS](WBS_J4S_ATTENDTA_20261103.pdf), the [ownership addendum](../_bmad-output/initiative-attendta/prd-attendta/addendum.md), and the two planning guides linked near the start.
- Official tool references: [uv installation](https://docs.astral.sh/uv/getting-started/installation/), [uv Python management](https://docs.astral.sh/uv/guides/install-python/), [GitHub CLI installation](https://github.com/cli/cli#installation), [issue inspection](https://cli.github.com/manual/gh_issue_view), [PR creation](https://cli.github.com/manual/gh_pr_create), [GitHub issue-closing rules](https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/linking-a-pull-request-to-an-issue), and [.NET on Windows](https://learn.microsoft.com/en-us/dotnet/core/install/windows).

This guide explains the existing workflow and suggests a team delivery order. It does not change the application requirements, approve unimplemented features, publish more issues, or replace peer review.
