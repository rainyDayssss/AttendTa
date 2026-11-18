# Release #269 reconciliation — preliminary

Compared local `upstream/main` at `1d469dc47b5beb647ab572fec3bb7641ccf15628`
with candidate baseline `9ce223a05787e7231ea28046c81f604726c91008`
on 2026-11-18. Remote refs were not refreshed; reconfirm before release.

The two main-only commits are merge `1d469dc47b5beb647ab572fec3bb7641ccf15628`
and submission-tree recut `cbaa2406f17eb33f1e044d2e3b444a24be5be328`.
The latter deliberately pruned the main tree. It is not safe to replace the current
tree with that older snapshot: in particular it uses obsolete profile values in the
scenario seed. Its camera troubleshooting addition should be reconciled with the
current scanner before release. Its README removes links to material absent on main;
keep those links if that material is shipped, otherwise provide equivalent public guidance.
The inventory and profile migration guidance present on dev must not be silently discarded.
The owner has authorized fictional Gmail-style addresses for the isolated new demo.
These addresses may belong to real people; never send email to them or use actual
production data. This does not authorize reuse of the old seed.

Top-level entries present in both trees: `.gitignore`, `ASI.Basecode.Data`,
`ASI.Basecode.Resources`, `ASI.Basecode.Services`, `ASI.Basecode.WebApp`,
`ASI.Basecode.sln`, `README.md`, `database`, `docs`, `global.json`, `tests`.
Present only on the candidate baseline: `.agents`, `.github`, `.gitmodules`,
`.impeccable`, `.opencode`, `AGENTS.md`, `AttendTa-User-Stories.md`, `DESIGN.md`,
`PRODUCT.md`, `_bmad-output`, `_bmad`, `bruno`, `docsss`, `newdocs`,
`opencode.jsonc`, `skills-lock.json`, `spec`, `tools`. None are removed in this
preliminary change; inclusion in a public release tree remains a decision, not an
approval to publish private or development-only material. No root entries are
present only on main.

The GitHub v1.0.0 release lists `AttendTa-1.0.0-win-x64.exe`,
`attendta-linux-x64.zip`, and `attendta-portable.zip`. Their build and packaging
provenance remains unconfirmed; preserve all three experiences and do not claim
reproducibility until their sources and smoke tests are established. No asset was
downloaded, modified, or published during this comparison.

Owner clarification: v1.0.0 is an old release and an experience reference only. The
new executable is built from the current worktree, not from the old binary or its
packaging source. Reproducing the old installer is not a requirement.
