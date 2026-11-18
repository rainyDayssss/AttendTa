# Release #269 verification — blocked, not a release candidate

### Current seed and package check, 2026-11-19

The owner approved the appearance of the original illustrations. On the current dirty
working tree, an isolated Release solution build succeeded with zero warnings and errors;
411 service tests and 25 focused integration tests passed. The latter included the seed,
asset, credential, target, ownership and setup guards. The seed test checks 40 members,
eight events, BSCS/BSIT, the venue associations and distinct password hashes, and
authenticates an administrator and a member against selected read surfaces. Development
test logs included Data Protection, HTTPS and EF model warnings.

The package script completed using separate artifact directories, producing Windows,
Linux and portable ZIPs and a standalone Windows executable. All four recorded SHA-256
checksums matched, and the standalone bundle structure check passed. The script verified
that each package contains the five venue and eight event banners. Its metadata records
baseline `9ce223a05787e7231ea28046c81f604726c91008` and a dirty tree, not a reproducible
committed candidate. The package was not published.

The new standalone executable's `--extract-only` path was used to provision a fresh
setup with independently generated process-only credentials on a newly created,
unshared LocalDB instance with no user databases. SQL confirmed 40 members, eight events,
five venues and only BSCS/BSIT member courses. This run did **not** launch the standalone
interactively, sign in to that setup, retrieve images or perform transactions; the
process-only credentials were not retained for those subsequent checks. Earlier image
and transaction results below remain evidence only for their respective earlier builds.
No browser, Linux runtime or complete end-to-end rehearsal was performed in this run.
Automated reset remains unavailable; host readiness, offline operation and a clean
committed-candidate verification remain open.

The disposable database's exact physical file paths were checked against the newly
created private root before dropping it; the empty instance and only this run's
extraction, setup root and integration test copy were removed. Existing databases and
preview files were not changed.

### Original illustration preview, 2026-11-19

Thirteen original demo banners replace the photographic assets; they are fictional
illustrations, not depictions of the named facilities. See `database/demo-assets/README.md`.
An isolated Release solution build succeeded without warnings. Thirteen focused asset,
seed, target and setup-guard tests passed on a new LocalDB instance. The first test
attempt failed because the isolated build output was outside the solution tree; copying
the test output to a separate `bin` directory inside the tree resolved the test host's
solution lookup. Development-host Data Protection, HTTPS and EF warnings remained.

A fresh preview was provisioned on a separate LocalDB instance and private root using
the previously held credentials; existing previews were not modified. Administrator
sign-in succeeded, and all five venue and eight event image endpoints returned HTTP
200 PNG content byte-for-byte matching their corresponding bundled files. This is not
a visual browser review or a presenter-scenario rehearsal.

The isolated package script built Windows, Linux and portable publish directories and
ZIPs but timed out during the standalone bundle build, before writing its final build
metadata. A separate invocation completed the standalone executable, whose bundle
structure check passed. The full package script did not complete and should be rerun
before release; no package or release was published.

### Photographic preview, 2026-11-19

Fresh setup on a newly created private LocalDB instance and root used the existing
separately held preview credentials. The current Release solution build succeeded
without warnings. The service suite passed 411 tests; 12 focused seed, target and
setup-guard integration tests passed on another new LocalDB instance.

After administrator sign-in on the new preview, all five `/Venue/Photo?id=1..5`
and eight `/EventCover/Image?id=1..8` requests returned HTTP 200 and `image/png`.
Each response was decoded as PNG, was at most 2 MiB, and matched its mapped bundled
file byte-for-byte. This is an HTTP/content check, not a browser visual review. The
initial attempt used the cover-management route rather than the image route; the
successful checks used the routes stated above. Existing databases were not changed.
See `database/demo-assets/README.md` for source, license and modification details.
The package build produced Windows and Linux publish directories but failed at the
portable build because the live preview processes held the build DLL open. The package
run is incomplete and must be repeated with a separate build output or stopped preview;
no package or release was published.

### Eight-event local preview, 2026-11-19

The uncommitted working tree builds in Release with zero warnings and errors.
The SQL-backed `DemoAccountSeederTests` passed on a newly created, unshared
LocalDB instance, covering 40 members, eight distinct cover paths, preserved
scenarios, three future computing activities and their registrations. The fixture
removed its database; the instance was verified empty and deleted. Development-host
Data Protection, HTTPS and EF model warnings appeared during the test.

The locally built Windows package was provisioned on a different new dedicated
LocalDB instance and private store. All twelve presenter accounts obtained HTTP
authentication cookies, and all eight event cover endpoints returned HTTP 200
and `image/png` to the administrator. SQL confirmed 40 members and eight events.
An initial request used a path-style ID and returned 404; the application's
query-string route succeeded. These are HTTP and SQL checks, not visual browser
review, a full scenario rehearsal, or release approval. The standalone bundle was
built but was not launched in this preview; the extracted package app was used.
The seed is proposed for owner review, not approved for release.

### Expanded seed verification, 2026-11-18

On the uncommitted working tree, the Release solution build passed with zero warnings
and errors. On a newly created isolated LocalDB instance, 24 focused seeder,
credential, setup guard, target and ownership tests passed. The SQL-backed seeder test
checks all twelve active presenter hashes, account/request history, registrations,
attendance, certificate review states and no issued file. It also signs in an admin
and member and checks authenticated HTML for the request queues, accounts, pending and
waitlisted registrations, report and certificate event listings, My Events, attendance
and passes. It does not prove browser rendering or mutations on this seeded state.

A separate run of 177 related feature integration tests passed on another new isolated
instance before the final change that assigned the inactive example an independently
generated random password; those tests use their own fixtures, not this seed. The
focused seeder suite passed again after that change. Each fixture dropped its own
generated database; each instance was confirmed empty before it was stopped and
deleted. Test-host logs included unencrypted Data Protection key, HTTPS-port and EF
default-value warnings. No existing preview database or credential file was touched.
The fresh setup, browser walkthrough, actual issuance/PDF and visual inspection of the
expanded seed remain outstanding; this is not release approval.

### Standalone executable runtime smoke, 2026-11-18

Tested the supplied `AttendTa-Demo-Standalone.exe` from the uncommitted build
(SHA-256 `DAC345A80147AADDFB6F71A01ED6B90CC14DCE4CBA2D4B0A1AF414BBA0F59F`).
Its bundle check passed. On newly created LocalDB instance
`AttendTaDemo_60cc3a562df64111a7cc5749b922f441`, owned by the current user,
unshared, and verified to have zero user databases, `--extract-only` extracted
the embedded package. The extracted app completed fresh setup with independently
generated credentials supplied via redirected standard input; no credentials
were recorded.

Running the standalone executable normally created a separate extraction and
started its embedded launcher and app. With redirected interactive responses,
the launcher reached its loopback login page. An administrator signed in over
HTTP and received an authentication cookie; `/Venue/Photo?id=1` returned HTTP
200, `image/png`, and 2,553 bytes. No representative state-changing transaction
was exercised. This was not a physical double-click or browser interaction.
The app logged unencrypted Data Protection key and missing HTTPS-port warnings.
The test harness timed out waiting for shutdown while not draining the redirected
output; subsequent process inspection found no remaining test app or launcher.
Therefore graceful launcher shutdown was not verified.

Before cleanup, SQL inspection confirmed the sole user database's two physical
files were exactly under the newly created private test root. The database was
dropped only after a SQL-side check of the user-database count and both exact
paths; the now-empty instance was stopped and deleted. The uniquely named test
root, including both extractions, was removed after checking for reparse points.
No pre-existing database was touched. No release, publication or push occurred.

### Standalone bundle structure check

On the uncommitted working tree, `scripts/build-demo-packages.ps1` built the
Windows, Linux and portable ZIPs and `AttendTa-Demo-Standalone.exe`. Running the
standalone executable with `--check-bundle` reported that its embedded package
structure was valid. This check did not extract or launch the app; setup,
sign-in, images and double-click behavior through this standalone executable
remain unverified. These artifacts are not an approved release candidate.

Baseline: `9ce223a05787e7231ea28046c81f604726c91008`. The working tree also
contains this preliminary documentation; it is not an exact committed candidate.
Results below are from 2026-11-18 and must be repeated for the exact candidate.

| Check | Result |
|---|---|
| `dotnet build ASI.Basecode.sln --configuration Release` | PASS, zero warnings and errors, on baseline before these documentation changes |
| Service test command in plan | PASS, 411 tests, baseline before documentation changes |
| Integration tests | PASS: full suite, 683/683 on isolated LocalDB; see run details below |
| Isolated setup, logins and images | PASS on working-tree build; full scenario walkthrough remains blocked |
| Package smoke, Windows/Linux/portable | Local ZIPs built; Windows and portable validation-only smoke passed; Linux execution blocked |
| Main reconciliation | Preliminary comparison only; see [reconciliation](release-269-reconciliation.md) |
| GitHub #242 and #243 | Both reported CLOSED; closure alone does not establish this candidate's verification |
| Owner approval for private credential handover | APPROVED: the owner replied “yes” to keeping passwords out of the public executable and using a separately distributed private credential file |
| Reset, merge to main, tag, publication | Not performed; each requires separate authorization |

Follow-up on the uncommitted working tree: Release build passed with zero warnings;
411 service tests and 20 credential, target and setup-root guard tests passed. A
framework-dependent `dotnet publish` to a temporary directory succeeded and its
`demo/` directory contained the schema, two PNGs and asset license. This is an asset
presence check, not a package or application smoke test. SQL-backed scenario testing
and a fresh setup had not yet been performed at that stage. Later checks are below.
The ZIPs are not a reproduction of the v1.0.0 Windows installer.
Automated reset remains blocked: existing deployments have no independently verified
database-to-private-store ownership marker, and a database or directory name alone
cannot prove a safe destructive target. New setups now have a read-only ownership
inspection; it does not authorize reset.

### Isolated LocalDB follow-up, 2026-11-18

On an explicitly approved newly created private instance
`AttendTaDemo_893d1b7e99c446dd8b5c03a3acb6275f`, `sqllocaldb info` reported
the current user as owner, no shared name, and `sqlcmd` reported zero user databases
before testing. No existing instance was selected. The SQL-backed and guard tests ran:

```powershell
$env:ATTENDTA_TEST_SQL = 'Server=(localdb)\AttendTaDemo_893d1b7e99c446dd8b5c03a3acb6275f;Database=master;Integrated Security=true;TrustServerCertificate=true'
dotnet tests/ASI.Basecode.IntegrationTests/bin/Release/net10.0/ASI.Basecode.IntegrationTests.dll -class ASI.Basecode.IntegrationTests.DemoAccountSeederTests -class ASI.Basecode.IntegrationTests.DemoCredentialInputTests -class ASI.Basecode.IntegrationTests.DemoTargetTests -class ASI.Basecode.IntegrationTests.DemoSetupGuardTests -parallelMode none
```

Result: **22 passed**. A fresh setup with random process-only passwords succeeded; HTTP
sign-in succeeded for all 12 existing accounts and both venue image requests returned
200. SQL inspection found 12 users, 11 linked members, one pending request, and the
documented event registration counts (10 confirmed summit, 3 confirmed/1 pending
assembly, 3 cancelled outreach). This is a working-tree test, not a committed release
candidate or package smoke. Request approval, applicant sign-in, event operations,
certificate flow, browser rendering and mobile scanning were not exercised.

An initial setup exposed a LocalDB default-file collision; setup now specifies file
paths under the new private root. One smoke attempt used an incorrect image URL; the
correct route is `/Venue/Photo?id=<id>`. After checking each database's physical paths
against its exact new temporary root, the test databases were dropped, the instance
was confirmed empty, stopped and deleted, and only the five named temporary roots
created for this run were removed after reparse-point checks. No public release or
merge was performed.

### Package and scenario follow-up, 2026-11-18

On the uncommitted working tree, ran:

```powershell
./scripts/build-demo-packages.ps1 -OutputDirectory "$env:TEMP\opencode\attendta-packages-3a487ebdfb3d499b91fecc7e93e5b3cf"
```

All three ZIP candidates built; archive inspection confirmed the canonical schema,
two PNG images and license in each. The Windows apphost and portable DLL each ran
`--validate-demo-credentials` successfully with generated process-only input. This
does not exercise application startup, sign-in or the setup from a ZIP. Linux execution
was not tested; the host exposes only the Docker Desktop WSL distribution. The
Windows ZIP is not the v1.0.0 installer and no packaging provenance was established.
The Linux ZIP was produced by Windows `Compress-Archive`; executable permissions
on extraction are unverified and must be checked on Linux before distribution.
The unique package output directory was removed after a reparse-point check.

Created a separate private LocalDB instance
`AttendTaDemo_3a487ebdfb3d499b91fecc7e93e5b3cf`, verified owner, absence of
sharing and zero user databases, then ran:

```powershell
$env:ATTENDTA_TEST_SQL = 'Server=(localdb)\AttendTaDemo_3a487ebdfb3d499b91fecc7e93e5b3cf;Database=master;Integrated Security=true;TrustServerCertificate=true'
dotnet tests/ASI.Basecode.IntegrationTests/bin/Release/net10.0/ASI.Basecode.IntegrationTests.dll -class ASI.Basecode.IntegrationTests.DemoAccountSeederTests -class ASI.Basecode.IntegrationTests.AccountRequestDecisionTests -class ASI.Basecode.IntegrationTests.EventCompletionFlowTests -class ASI.Basecode.IntegrationTests.CheckInFlowTests -parallelMode none
```

**34 passed.** The demo seed test now checks event dates and status, distinct pass
tokens, empty attendance/certificate ledgers, and the pending applicant hash. The
other classes test approval, check-in and completion against their own test fixtures,
not the seeded demo walkthrough. Test output included Data Protection and EF model
warnings; no test failed. Afterwards the instance had zero user databases and was
stopped and deleted. No existing instance or database was touched.

### Current-worktree Windows launcher follow-up, 2026-11-18

The owner clarified that v1.0.0 is an experience reference, not the source for the
new executable. `scripts/build-demo-packages.ps1` built the current worktree's
self-contained Windows app and source-built `AttendTa-Demo.exe` launcher. The launcher
is included in the Windows ZIP alongside the application; the ZIP must be extracted.
An extracted earlier candidate passed `--check-package`. A later candidate, including
the fictional UC-style seed, successfully ran packaged fresh setup and launched the
app to a loopback HTTP 200 login page from the interactive launcher, then stopped it.
The smoke used newly created private LocalDB instance
`AttendTaDemo_61b5458a361d4c768fa79d787ad69ba7` and generated process-only
credentials; focused SQL and guard tests passed 22/22. The test database's physical
paths were verified before removal; the empty instance was stopped and deleted and
only uniquely named temporary test roots were removed after reparse checks.

The launcher was subsequently hardened to override inherited hotspot configuration.
No binary was downloaded or published. The package is not a single-file installer,
and real-host double-click, browser interaction, code signing and full presenter flows
remain open.

### Latest extracted-package smoke, 2026-11-18

Built all three candidates from the then-current uncommitted working tree with
`./scripts/build-demo-packages.ps1 -OutputDirectory <new private temporary directory>`.
Extracted the Windows ZIP, ran `AttendTa-Demo.exe --check-package`, and ran the
extracted app's `--setup-demo --confirm-dedicated-localdb` with generated process-only
credentials. The extracted launcher then started the app and reached the loopback
login page (HTTP 200), even with `Hotspot__Enabled=true` inherited by the launcher.
Inputs were piped to simulate the interactive prompts; a physical double-click and
browser sign-in were not tested in this run.

The newly created private instance was
`AttendTaDemo_2df0e29f9dbd4ac6a5b038300c847fa4`; its owner and unshared status and
zero initial user databases were checked. SQL inspection confirmed the five fictional
presenter labels, BSIT/BSCS and varied year/section profiles, two campus venues,
three seeded events and one pending request. After matching the database physical
files to its exact new private root, the database was removed, the instance was
confirmed empty, stopped and deleted, and only the two uniquely named test directories
were removed after reparse checks. No other database was modified.

The full named-beat results and sequencing are in
[the scenario matrix](demo-269-scenario-matrix.md). Automated reset remains blocked:
no destructive reset command is provided. Release approval, a committed candidate, signing and
remaining scenario and platform verification are still required.

### Fresh ownership marker, 2026-11-18

Newly provisioned setups record a random matching identifier in the database and
private root; `--inspect-demo-ownership --confirm-dedicated-localdb` only checks
evidence and never resets. On newly created, unshared LocalDB instance
`AttendTaDemo_563487b153d14501aee537f0def963ef`, with no initial user databases,
fresh setup and matching inspection succeeded. Missing confirmation, missing marker,
mismatched marker, a copied marker with different physical database files, and setup
against the occupied instance were refused. The occupied-instance refusal occurred
before creating its proposed new root. Guard unit tests passed 13/13 and the final
Release build completed without warnings. The exact owned database paths were checked
before deletion; the empty new instance was stopped and removed, and only the two
new test roots were removed after reparse checks.

This is evidence, not a security boundary or safe reset implementation: the marker
can be copied, inspection is subject to time-of-check/time-of-use races, and it does
not prove host isolation or filesystem ACLs. Existing or incomplete installations
are not automatically marked. A future reset must bind validation and destructive
actions safely, require fresh explicit approval, and preserve unrelated stores.

### Full integration suite, 2026-11-18

On the uncommitted working tree, `dotnet build ASI.Basecode.sln --configuration
Release` succeeded with zero warnings and errors. Created and started private LocalDB
instance `AttendTaDemo_abe8d30ed30846a88290c807a01e83e0`; `sqllocaldb info` showed
the current user as owner, no shared name and no auto-create, and a `sys.databases`
query showed zero user databases before the run.

```powershell
$env:ATTENDTA_TEST_SQL = 'Server=(localdb)\AttendTaDemo_abe8d30ed30846a88290c807a01e83e0;Database=master;Integrated Security=true;TrustServerCertificate=true'
dotnet tests/ASI.Basecode.IntegrationTests/bin/Release/net10.0/ASI.Basecode.IntegrationTests.dll -parallelMode none
```

The runner reported **683 total, 683 passed, 0 failed, 0 skipped, 0 not run**,
in 506.816 seconds. Output included development-host Data Protection and HTTPS
redirection warnings and a logged duplicate-key exception; none was reported as a
failed test. This does not establish production-host readiness or replace a
release-candidate demo.

Afterward `sys.databases` showed zero user databases on that instance; it was stopped
and deleted. A final `sqllocaldb info` listed only the pre-existing `MSSQLLocalDB`.
No pre-existing instance or database was reset or modified.

### Seeded packaged-app HTTP scenarios, 2026-11-18

Built current working-tree packages with `scripts/build-demo-packages.ps1`, extracted
the Windows ZIP, and provisioned its app into newly created, unshared LocalDB instance
`AttendTaDemo_86acd6d50f20417c9607ce69726882a1` and a new private temporary root.
The instance was owned by the current user and had zero user databases before setup.
Credentials were generated in the test process and were not recorded.

Against the running packaged app over loopback HTTP, administrator, officer and
Gabutan signed in. In sequence: approved the seeded applicant, verified the linked
user/member in SQL and signed the applicant in; searched for the applicant; loaded
all three officer dossiers and the pending review queue; opened assembly check-in,
searched for Gabutan, recorded and invalidated a manual check-in; completed the
summit and loaded its participation report; and loaded the administrator audit log.
Home, member event feed, registrations, passes and profile pages were also checked
for the expected identity or event text. The exact pass/partial/blocked scope is in
[the presenter scenario matrix](demo-269-scenario-matrix.md). SQL observations
confirmed the state-changing HTTP actions; a 200 response alone was not treated as
proof of a mutation.

The full browser experience, camera scan, registration decision, event creation,
certificate issuance/PDF and mobile checks remain unperformed. After checking the
database physical paths against the exact new root, the test database was removed;
the now-empty instance was stopped and deleted. Only the two uniquely named package
and private-store test directories were removed after reparse-point checks. No
existing instance or database was touched, and no package was published.

### Registration, event and certificate HTTP follow-up, 2026-11-18

Built current working-tree Windows, Linux and portable candidates with
`scripts/build-demo-packages.ps1` and extracted the Windows ZIP. Provisioned its app
with generated process-only credentials on newly created unshared LocalDB instance
`AttendTaDemo_d587a1bdf7aa4d60acdb2c1afe1d2b66`, owned by the current user and
verified empty before setup. In the running packaged app:

1. The officer approved Sinco's seeded pending assembly registration; SQL confirmed
   it became confirmed.
2. The officer created `UC Peer Tutoring Session` via multipart HTTP with a staged
   PNG cover, then published it; SQL confirmed published status and a cover key.
   Gabutan registered via HTTP; SQL confirmed a pending registration.
3. The officer opened summit check-in, manually checked Gabutan in, completed the
   summit, and approved the generated certificate review; SQL confirmed attendance,
   completion and eligibility.
4. The officer issued the certificate; SQL confirmed issued status. The member's
   authenticated PDF endpoint returned HTTP 200, `application/pdf`, and nonempty
   content exceeding 100 bytes. Visual rendering and PDF text were not checked.

Results are reflected in [the presenter scenario matrix](demo-269-scenario-matrix.md).
These are HTTP and SQL checks, not physical browser, camera or mobile verification.
The owner explicitly approved keeping passwords out of the public executable and using
a separately distributed private credential file, replying “yes” after those two
recommendations. This approves the credential design, not a release, merge, tag,
publication or database reset. Do not reproduce credentials in evidence.

After checking its physical files against the exact newly created private root, the
test database was removed, the instance was verified empty, stopped and deleted, and
only the two uniquely named package and private-store test directories were removed
after reparse-point checks. No existing instance or database was touched.

### Package provenance check, 2026-11-18

`scripts/build-demo-packages.ps1` was run against the current uncommitted worktree
into a uniquely named temporary directory. Windows, Linux and portable ZIP builds
succeeded. `BUILD-INFO.txt` recorded the full Git HEAD and `working_tree=dirty`;
independent SHA-256 calculations matched all three entries in `SHA256SUMS.txt`.
These checks identify the output archives and their baseline, not the complete dirty
source tree or a committed release candidate. The test output directory was removed
after reparse-point checks; no database was accessed and no release was published.

## Implementation in progress

The application has a validation-only `--validate-demo-credentials` command. It accepts
JSON from redirected standard input, not command-line arguments. Required property names
are `admin`, `officer`, `gabutan`, `taguiam`, `talisic`, `sinco`, `pinca`, and
`member06` through `member10`, plus `applicant` for the pending request; each value is a distinct password of 8–128 characters
containing an ASCII letter and digit. No additional properties are permitted. Supply
the input from a file accessible only to the authorized operator; the command does not
inspect file permissions. Do not record the input, hashes or command output containing
them. Validation makes no database changes and does not provision the demo.

An experimental fresh-only Windows LocalDB setup command now wires the parameterized
seeder, canonical schema and original CC0 image staging together. It refuses an instance
with any user databases and an existing setup root. The operator must independently
establish host isolation and private ACLs. It is not a reset mechanism; failures may leave
a partial new database or store for separately approved cleanup. Basic isolated setup,
sign-in and image checks passed as recorded above; the full scenario remains unverified.
See [fresh local setup](demo-fresh-setup-269.md).

The old seed uses one member hash for multiple accounts, non-reserved email domains,
and can modify matching existing scenario records. A database name is not proof of
server isolation. The historical presenter guide also contradicts its seed about
attendance and certificate state. Do not claim any presenter beat passed until it is
re-run on the exact release with documented prerequisites and ordering.
