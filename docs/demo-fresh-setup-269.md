# Fresh local demo setup (experimental, not release-verified)

Fresh setup and basic sign-in/image checks have been exercised on an isolated test
instance, but the full demo has not. Do not use this as proof of release readiness.
It supports Windows and a dedicated, private LocalDB
instance only; it does not reset or reuse any database or private store.

1. Obtain approval for a dedicated demo host and identify the operator. Confirm no other
   workload uses the named LocalDB instance. If it does not exist, create a private
   `(localdb)\AttendTaDemo` instance using `SqlLocalDB`; inspect it independently.
   Alternatively, set `ATTENDTA_DEMO_LOCALDB_INSTANCE` to `AttendTaDemo_` followed by
   32 hexadecimal characters for a uniquely named private instance.
   The setup rejects an instance containing *any* user database. A name alone does not
   establish isolation.
2. Build or publish the application. The `demo/` directory alongside the application
    must contain the canonical schema, venue images, eight event images and their license. Set up an
   existing private parent directory outside the source checkout, deployment tree and
   web root. Restrict access to the operator and runtime identity. Choose a **new**
   child path as `ATTENDTA_DEMO_SETUP_ROOT`; setup refuses an existing child.
3. Prepare an independently delivered JSON file outside the checkout and deployment,
   accessible only to the operator. The exact required keys and password rules are in
   [release verification](release-269-verification.md). Do not place credentials in
   commands, logs, source control, package assets or issue comments. The program does
   not verify file or directory ACLs; the operator must do so.
4. In a protected PowerShell session, with `$app` the path to the built
    `ASI.Basecode.WebApp.dll` and `$credentialFile` the private file path:

   ```powershell
   $env:ATTENDTA_DEMO_LOCALDB_INSTANCE = '<dedicated instance name>'
   $env:ATTENDTA_DEMO_SETUP_ROOT = '<new private child directory>'
   Get-Content -Raw -LiteralPath $credentialFile | dotnet $app --setup-demo --confirm-dedicated-localdb
   ```

   The confirmation asserts that the operator verified the instance is dedicated; it
   does not prove isolation. Input is validated before filesystem or database writes.
   A success message means setup completed, not that sign-in, assets or scenarios passed.
 5. Configure the application for `(localdb)\<dedicated instance name>`, database `AttendTa_Demo`,
    integrated authentication, and the same `Common__SetupRoot`. Use exactly the instance
    selected in step 4, including any custom suffix. Apply appropriate
    runtime access controls before starting. Use the account mapping below; credential
    keys are not login emails. Validate every login, image and presenter scenario
   before calling the demo ready.

On any failure, **stop**. Setup does not delete a partial database or private root and
will refuse a retry while either exists. Inspect both and obtain separate explicit
approval before removing or resetting anything. Do not reuse the old rehearsal seed.
The seeded pending request is `applicant@gmail.com` (student number `24000099`);
after approval it uses the independently supplied `applicant` password. The setup
creates workshop attendance and certificate review rows, but no issued certificate PDFs. The historical guide's assertions about those states are
not applicable. Reset remains unimplemented.

### Read-only ownership inspection

Newly completed setups write a matching random ownership identifier to the database
and `.attendta-demo-owner` in the private root. Older and partially completed setups
are not adopted or marked automatically. With the same protected environment variables
used for setup, an operator who independently confirmed the LocalDB instance is dedicated
may run:

```powershell
dotnet $app --inspect-demo-ownership --confirm-dedicated-localdb
```

This read-only check refuses missing or mismatched markers, other user databases,
non-LocalDB targets, unsafe root paths and database files outside the supplied root.
It is **not** reset authorization or proof of isolation: markers can be copied or
tampered with, ACLs are not verified, and state can change after inspection. It does
not modify, repair, adopt, or delete anything. No destructive reset tool is provided.

All profiles, names, student numbers, contact details and activities are fictional.
The five presenter accounts retain the Gabutan, Taguiam, Talisic, Sinco and Pinca
labels; presenter accounts and the pending request use `gmail.com`. These fictional addresses may
belong to real people: use them only in the isolated demo and **never send email** to
them. Do not use actual production data. The academic profiles,
campus locations and activities are illustrative, not records of actual people or events.

The venue list includes Covered Court, UC–Main Jones Building Auditorium, Doña Alicia Gotianuy Audio-Visual Room (AVR),
room 530-ABC, and Cebu Coliseum. Their respective demo capacities (250, 300, 100, 60,
and 2,000), location details and Cebu Coliseum contact label are **fictional**, not
official venue facts or contact information. All venues use original illustrative
banners, not photos or depictions of the actual venues; see `demo/ASSET-LICENSE.md`.
All five image files are staged in the private setup root.

The management event list includes these associations (use `pageSize=8` to
inspect all eight seeded events together):

| Event | Venue | Participant limit / venue capacity |
|---|---|---|
| CCS Computing Community Assembly | UC–Main Jones Building Auditorium | 100 / 300 |
| CCS Accessible Web Workshop | Doña Alicia Gotianuy Audio-Visual Room (AVR) | 40 / 100 |
| CCS Cybersecurity Capture the Flag | room 530-ABC | 60 / 60 |
| CCS AI and Responsible Computing Summit | Cebu Coliseum | 120 / 2,000 |
| CCS Coding Peer Mentoring Lab | room 530-ABC | 2 / 60 |
| CCS Data Science and AI Studio | room 530-ABC | 50 / 60 |
| CCS Game Development Jam | Doña Alicia Gotianuy Audio-Visual Room (AVR) | 80 / 100 |
| CCS Community Tech Outreach | UC–Main Jones Building Auditorium | 80 / 300 |

| Private credential key | Demo name | Login email | Role |
|---|---|---|---|
| `admin` | Alex Mercado | `alexmercado@gmail.com` | Administrator |
| `officer` | Jamie Villareal | `jamievillareal@gmail.com` | Officer |
| `gabutan` | Jerhon Gabutan | `jerhongabutan@gmail.com` | Member |
| `taguiam` | Jasper Taguiam | `jaspertaguiam@gmail.com` | Member |
| `talisic` | Jhon Talisic | `jhontalisic@gmail.com` | Member |
| `sinco` | John Paul Sinco | `johnpaulsinco@gmail.com` | Member |
| `pinca` | Seth Pinca | `sethpinca@gmail.com` | Member |
| `member06` | Riley Sarmiento | `rileysarmiento@gmail.com` | Member |
| `member07` | Drew Alonzo | `drewalonzo@gmail.com` | Member |
| `member08` | Casey Valdez | `caseyvaldez@gmail.com` | Member |
| `member09` | Jules Navarro | `julesnavarro@gmail.com` | Member |
| `member10` | Noel Soriano | `noelsoriano@gmail.com` | Member |

## Starting scenarios and order

### CCS event preview (fictional; pending owner approval)

Most dates are relative to fresh setup time; the three showcase events below have fixed
2026 dates (09:00 in the application's stored time convention). Their registration deadlines
are two days before their starts. Setup after a deadline or event date will not make them
upcoming or reopen registration; use an appropriately timed demo or plan new events.
Relative publication and registration timestamps also reflect setup time, so a late setup
can place those timestamps after the fixed event dates. Each event has
an original illustrated cover.
Existing previews are not changed.
All eight start with check-in closed; the officer can open check-in for the live
demonstration. The completed workshop retains its historical closed check-in.

| Event | Starts | Starting state |
|---|---|---|
| CCS AI and Responsible Computing Summit | 3 days before setup | Published, ready for live completion |
| CCS Community Tech Outreach | 1 day before setup | Cancelled |
| CCS Computing Community Assembly | 2 days after setup | Published, registration open |
| CCS Accessible Web Workshop | 14 days before setup | Completed, attendance and certificate reviews |
| CCS Coding Peer Mentoring Lab | 8 days after setup | Published, full with waitlist |
| CCS Cybersecurity Capture the Flag | November 25, 2026 | Published, six confirmed |
| CCS Data Science and AI Studio | November 28, 2026 | Published, six confirmed |
| CCS Game Development Jam | December 2, 2026 | Published, six confirmed |

For the complete named-beat status and dependencies, see the
[presenter scenario matrix](demo-269-scenario-matrix.md).

The SQL-backed seed and selected authenticated read surfaces have automated coverage;
these are **not** a browser rehearsal. Run each on the exact candidate and record pass
or blocked, including dependencies.

| Order | Starting point and action | Dependency / expected state |
|---|---|---|
| 1 | Administrator approves `applicant@gmail.com` in Account requests. | Pending request exists; approving it creates the linked member. Applicant sign-in requires its private password. |
| 2 | Administrator inspects members, venues and event types; creates and publishes an event. | Five privately staged venue images and six classifications; publishing a new event requires a cover. |
| 3 | Officer reviews CCS assembly registrations. | Gabutan, Taguiam and Talisic confirmed; Sinco pending. |
| 4 | Officer opens CCS assembly check-in, then scans Gabutan's pass. | The officer must open check-in first; camera requires supported browser and secure context, or use manual lookup. |
 | 5 | Officer completes CCS AI and Responsible Computing Summit. | Summit started three days before setup and has 38 confirmed registrations; completion is a live mutation. |
| 6 | Officer reviews eligibility and issues a certificate; member views the PDF. | A valid check-in must exist for the completed event before completion; current seed provides none. Chromium and certificate configuration are required. This path is blocked until its ordering and prerequisites are tested. |
| 7 | Officer views participation reports; administrator views activity log. | The workshop has seeded history; other results reflect the actions performed. |

The fresh seed additionally contains **CCS Accessible Web Workshop**, completed 14
days before setup: Gabutan, Taguiam and Talisic have valid attendance; Sinco's
check-in is invalidated and Pinca is a no-show. Their five registrations are confirmed.
Certificate review rows are pending for Gabutan, eligible for Taguiam and not eligible
for Talisic. No certificate is issued and no PDF is fabricated. Inspect the completed
workshop's report and certificate review before changing these decisions. Actual PDF
issuance still requires the app's configured signatory and working Chromium.

**CCS Coding Peer Mentoring Lab** starts eight days after setup with capacity two:
Gabutan and Taguiam are confirmed and Pinca is waitlisted. Inspect Pinca's My Events
and the officer registration queue; promotion requires a real vacancy and the normal
officer action. The existing summit remains published and available for live completion.
Three historical activity entries describe publication/completion; inspect alongside
the system provisioning entry. The seed was tested on a separately created isolated
LocalDB instance; a manual browser walkthrough remains outstanding. It does not modify
an existing demo.

In Account requests, Taylor Ramos remains pending for live approval. Historical
examples include approved Morgan Dizon (linked to an inactive account and inactive
membership) and rejected Robin Salcedo. The historical examples use reserved
`example.invalid` addresses, have no presenter credential, and are not intended for
sign-in. Morgan's random, discarded password is distinct from all presenter credentials.
All twelve presenter accounts remain active. Inspect Accounts and Members
for Morgan without changing access needed by the demonstration.

The fresh seed has 40 linked member profiles: the officer, ten presenter members,
28 additional active supporting members, and inactive Morgan. There are 41 users
including the unlinked administrator. Supporting accounts are not presenter logins:
their independently generated random passwords are discarded and never handed over.
The workshop has 25 confirmed registrations and 19 attendance records (one invalidated);
the assembly has 13 confirmed and five pending registrations. The summit has 38 confirmed,
the cancelled outreach has three cancelled, and the mentoring lab has two confirmed and
one waitlisted registration. These are fictional starting-state counts, not live results.
Additional Gmail-style addresses may belong to real people; never send to them.

An existing preview database is not updated by this change. To inspect the expanded
state, an operator must follow the fresh setup above on a **new** dedicated LocalDB
instance and a **new** private root, supplying the separately managed credentials
without disclosing them. Do not reset or overwrite the existing preview.

Actions alter the starting state. There is currently no automated reset: stop after a
run and obtain explicit approval and a verified disposable target before any removal
or recreation. Never use the historical destructive reset command on an assumed target.

## Local packaging

`docs/tooling/build-demo-packages.ps1` builds Windows x64, Linux x64 and
framework-dependent portable ZIP candidates into a new directory outside the checkout.
It requires Git, the .NET SDK, PowerShell 7 and the runtime packs needed for the selected
targets. It writes `BUILD-INFO.txt` with the full Git HEAD and clean/dirty state, and
`SHA256SUMS.txt` for the ZIPs alongside them. A dirty HEAD does not identify the full
source; preserve the reviewed diff separately and rerun verification on a committed
candidate. It does not upload or publish a GitHub release. Its Windows ZIP is **not**
the existing v1.0.0 installer: that installer's build provenance remains unresolved.
Extract the entire Windows ZIP, then double-click `AttendTa-Demo.exe`. Enter the
already-provisioned private root and dedicated LocalDB instance; the launcher starts
the app on a loopback port and opens the login page. It does not accept passwords,
provision accounts or reset databases. Keep its window open and press Enter to stop.
The executable needs the accompanying application files; it is not a standalone installer.
For first-time provisioning from the standalone executable, run it with `--extract-only`
and follow `FIRST-TIME-SETUP.md` in the extracted directory. That guide is also included
in the ZIP and contains the complete private credential input requirements.
v1.0.0 is an experience reference, not a source-code or binary baseline for this build.
The fresh-setup command currently supports only Windows LocalDB, even in the portable
and Linux application packages. Local candidate ZIPs have been built and Windows and
portable credential-validation commands smoke-tested, but full package and Linux
execution checks remain outstanding; do not represent these ZIPs as release-ready.
