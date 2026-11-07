# AttendTa demo script

Presenter document for the 8.15 rehearsal and the client demo. It covers the repeatable
starting state, the pre-flight, the per-role beats, the 390px checks, the known limits, and
the demo-ticket coverage map. The evidence from the 7 November 2026 rehearsal is in
`demo-rehearsal-findings.md`.

## 1. What the demo shows

One auditable lifecycle for a student-organization event: a membership request is approved,
master data is maintained, an event is created and published, a member registers and gets a
door pass, an officer scans the pass at the door, the event is completed, certificates are
reviewed and issued, the member downloads the PDF, and the per-event report shows who
registered, who attended, and who earned a certificate.

The demo build runs with `ASPNETCORE_ENVIRONMENT=Production` so the development certificate
tracer and its watermarked sample are not reachable. Demo data is on only through the
`DemoData__Enabled` switch; the committed `appsettings*.json` keeps it off.

## 2. Pre-flight (do this once on the demo machine)

### 2.1 Host and tools

- .NET 10 SDK/runtime, SQL Server (LocalDB is fine for the rehearsal), `sqlcmd`, Node 22 for
  the CSS build.
- A private `Common__SetupRoot` outside both the deployment tree and `wwwroot`. Create
  `keys`, `logs`, `temporary`, `uploads/venues`, `uploads/events`, and `certificates` under it.
- A disposable database named `AttendTaMVP_Test` (rehearsal) or `AttendTa_Demo` (demo host).
  Never run the reset or seed scripts against a shared or production database.

### 2.2 Database

The scripts are guarded to `AttendTaMVP_Test`, `AttendTa_Demo`, and `AttendTa_IT_<guid>`.
The rehearsal used `AttendTaMVP_Test`.

```powershell
# 1. Reset only the disposable test database.
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -i database/reset-local-mvp-test-db.sql

# 2. Apply the canonical schema.
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -d AttendTaMVP_Test -i database/attendta-mvp-schema.sql
```

Generate three Argon2id hashes with the built app (the only stdout is the PHC hash):

```powershell
dotnet build "ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj"
dotnet run --project "ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj" --no-build -- --hash-password "<admin secret>"
dotnet run --project "ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj" --no-build -- --hash-password "<officer secret>"
dotnet run --project "ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj" --no-build -- --hash-password "<member secret>"
```

Run the admin seed, the event-type seed, and the scenario seed. Use a small wrapper `.sql`
file with `:setvar` lines and `:r` includes so PowerShell does not split the hashes; keep the
wrapper and the plaintext passwords outside the repository.

```sql
:setvar AdminName "Maria Clara Dizon"
:setvar AdminEmail "maria.dizon@uc.edu.ph"
:setvar AdminPasswordHash "<admin hash>"
:setvar OfficerName "Rafael Santos"
:setvar OfficerEmail "rafael.santos@uc.edu.ph"
:setvar OfficerPasswordHash "<officer hash>"
:setvar MemberName "Liza Fernandez"
:setvar MemberEmail "liza.fernandez@uc.edu.ph"
:setvar MemberPasswordHash "<member hash>"
:r F:\AttendTaBMAD\database\seed-local-admin.sql
:r F:\AttendTaBMAD\database\seed-event-types.sql
:r F:\AttendTaBMAD\database\seed-demo-scenario.sql
```

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -d AttendTaMVP_Test -i "<wrapper>.sql"
```

The scenario seed is idempotent. Re-running it keeps the same accounts, members, venues,
events, and registrations, re-anchors the three event dates around the current UTC time, and
fills a mobile number only where one is still empty. It creates the pending account request
for Camille Torres only when that email has no account and no pending request.

### 2.3 Private images

The seed stores relative keys under `Common__SetupRoot`. Copy the tracked prototype assets
into the setup root (the event covers must use the 32-character GUID file names the event
cover service accepts):

```powershell
$root = "<Common__SetupRoot>"
$assets = "F:\AttendTaBMAD\_bmad-output\initiative-attendta\ux-attendta\imports\prototype-assets"
New-Item -ItemType Directory -Force "$root\uploads\venues","$root\uploads\events","$root\temporary","$root\certificates" | Out-Null
Copy-Item "$assets\bento-summit.jpg" "$root\uploads\venues\university-auditorium.jpg" -Force
Copy-Item "$assets\bento-lab.jpg"    "$root\uploads\venues\covered-court.jpg" -Force
Copy-Item "$assets\event-workshop.jpg"  "$root\uploads\events\1a2b3c4d5e6f47a8b9c0d1e2f3a4b5c6.jpg" -Force
Copy-Item "$assets\event-outreach.jpg"  "$root\uploads\events\2b3c4d5e6f7a48b9c0d1e2f3a4b5c6d7.jpg" -Force
Copy-Item "$assets\event-assembly.jpg"  "$root\uploads\events\3c4d5e6f7a8b49c0d1e2f3a4b5c6d7e8.jpg" -Force
```

If a venue thumbnail is missing, re-upload the photo through Venues → Edit; the venue list
shows a broken image only when the file is absent.

### 2.4 Environment and start

Set the values in the process environment, not in a tracked file. The token key and the
passwords are local secrets; never commit them.

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:DemoData__Enabled = "true"
$env:DemoData__AllowDatabase = "AttendTaMVP_Test"   # or AttendTa_Demo
$env:DemoData__TokenKey = "<local random secret>"
$env:ConnectionStrings__DefaultConnection = "Server=(localdb)\MSSQLLocalDB;Database=AttendTaMVP_Test;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
$env:Common__SetupRoot = "<private setup root>"
$env:ASPNETCORE_URLS = "http://localhost:5080"      # add an HTTPS binding on the demo host
```

Build and start:

```powershell
dotnet build ASI.Basecode.sln
cd ASI.Basecode.WebApp; npm run css:build; cd ..
dotnet run --project "ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj" --no-build
```

The start-up log must show:

```
Demo data active (M-01, M-07). Ready to complete event 2.
```

If it says "Demo data seeded nothing", the connected database has no events; re-run the
scenario seed. If the app refuses to start, read the stderr reason: the database name must be
`AttendTa_Demo` or exactly `DemoData__AllowDatabase`, and `DemoData__TokenKey` must be set.

Confirm the development-only surfaces are not reachable. Sign in as an Administrator first,
then open `/CertificateTracer` and submit the tracer form (or POST `/CertificateTracer/Trace`
with the page's `RequestVerificationToken`). Both must return 404. An unauthenticated request
is challenged by the Management policy before the action runs, so the 404 check needs the
authenticated session.

### 2.5 Sign-in accounts

| Role | Email | Password |
|---|---|---|
| Administrator | `maria.dizon@uc.edu.ph` | the local admin secret used for the hash |
| Officer | `rafael.santos@uc.edu.ph` | the local officer secret used for the hash |
| Member | `liza.fernandez@uc.edu.ph` | the local member secret used for the hash |

The seven supporting participants share the member hash; they are not signed in during the
demo. The pending request is `camille.torres@uc.edu.ph`; approving it creates the account with
the password hash stored on the request (the member hash for the seeded request, or the
password typed on the public form if the presenter submits a fresh request).

## 3. Starting state

| Event | SQL status | Demo state | Registrations |
|---|---|---|---|
| Leadership Summit 2026 | Published | Ready to complete; attendance and certificate data seeded | 8 confirmed; 8 check-ins; last invalidated; 1 issued, 1 pending, 1 declined, 4 pending reviews |
| Community Outreach Drive | Cancelled | Cancelled | 3 cancelled registrations (Liza, Joshua, Angela) |
| Founders' Week Assembly | Published | Upcoming, check-in open | Liza, Mark, Sofia confirmed; Daniel pending |

The demo completes Leadership Summit live in O3 through the real 3.12 service; until then it stays
Published. The cancelled Community Outreach event shows the cancelled state and the completion
refusal.

- Venues: University Auditorium (400), Covered Court (250).
- Event types: Seminar, Outreach, Meeting, Competition, General Assembly, Training.
- Accounts: 1 Administrator, 1 Officer, 8 member accounts (Liza plus seven participants).
- Pending account request: Camille Torres, `UC-2026-0110`.
- The demo store starts empty for check-ins, reviews, and issues; the seed fills it from the
  real rows above. A restart resets the store; the SQL rows and pass tokens do not change.

## 4. Administrator beats

Sign in as `maria.dizon@uc.edu.ph`. The Home page shows the Administrator shortcut cards.

| # | Beat | Route / action | Expected outcome | 390px check |
|---|---|---|---|---|
| A1 | Home | `/Home/Index` | "Welcome back, Maria Clara Dizon", the "Run the organization" and "Records and requests" cards, including Accounts and Activity log | Cards stack to one column; the mobile Menu opens every link |
| A2 | Account requests | `/AccountRequest` → Approve Camille Torres → Approve request | Queue shows 1 pending; the dialog names the applicant and the consequence; the notice says "Account request approved." | Dialog fits, reason/actions reachable |
| A3 | Members | `/Member` → search Camille | Camille Torres, `UC-2026-0110`, Active, with a mobile number | Table scrolls inside its container |
| A4 | Master data | `/EventType` → New Event Type; `/Venue` → New Venue; `/Member` → Edit | Each centered dialog loads its fields and Cancel/Save; no placeholder copy | Dialogs are single-column and fit the viewport |
| A5 | Create and publish | `/Event` → Create draft event → fill → Save draft → Cover → upload a JPEG/PNG → Publish | Draft appears; "Event cover updated."; "Event published." | Form fields stack; the cover upload is reachable |
| A6 | Access changes | `/admin/accounts` → Preview deactivation; Deactivate; `/Member` → Deactivate | Preview lists affected registrations; the dialog shows the consequence and a required reason; the admin row shows the last-active-Administrator block | Dialogs fit; the last-admin explanation is readable |
| A7 | Activity log | `/admin/auditlogs` | Newest first; the real account-request approval and member creation appear; demo-store check-ins/certificates do not | Table scrolls; timestamps stay readable |

Note for A5: publishing requires a cover image. The create form saves a draft; the Events list
publishes it after the cover upload. The refusal message is generic if the cover is missing.

## 5. Officer beats

Sign in as `rafael.santos@uc.edu.ph`.

| # | Beat | Route / action | Expected outcome | 390px check |
|---|---|---|---|---|
| O1 | Home | `/Home/Index` | Officer cards; no Accounts or Activity log | Cards stack; Menu opens |
| O2 | Event dossier | `/Event/1`, `/Event/2` and `/Event/3` | Metadata, capacity, status-aware roster; Leadership Summit shows Complete event; Community Outreach is cancelled; Founders' Week shows the check-in control | Roster scrolls; actions stay reachable |
| O3 | Completion | `/Event/1` → Complete event → Confirm | "Event completed."; status Completed; roster read-only. Completing Founders' Week before its start is refused with "An event cannot be completed before it starts."; completing the cancelled Community Outreach is refused | Dialog fits |
| O4 | Registration review | `/RegistrationReview/Index` | Daniel Navarro pending on Founders' Week; approve/decline with a reason | Cards stack; buttons reachable |
| O5 | Door scan | `/CheckIn` → Founders' Week → Pass code → paste Liza's pass token → Check in | "Liza Fernandez checked in." with name, student number, Manila time, and "Check-in recorded."; a second scan says "Already checked in. This is the original time." | Tabs, input, and outcome panel fit |
| O6 | Manual lookup | `/CheckIn` → Manual lookup → `Liza` → Search | Liza is found with "Checked in"; searching alone does not record | Table scrolls |
| O7 | Invalidation | Recorded check-ins → Invalidate | Dialog names the participant and requires a reason; Cancel leaves the check-in intact | Dialog fits |
| O8 | Certificate review | `/Certificates?eventId=1` | Pending reviews; approve one; decline another with a required reason; the declined row shows "Decision final" | Table scrolls; dialogs fit |
| O9 | Certificate issue and PDF | Eligibility Review → Issue certificate → Issued Registry → View PDF | "Certificate issued."; the registry lists the new number; the landscape A4 PDF opens with the configured signatory | Viewer is usable; the download link is reachable |
| O10 | Report | `/Reports/Participation?eventId=1` and `?eventId=2` | Event 1 (completed in O3): 8 confirmed, 7 valid, 87.5%, 1 no-show, course/year breakdowns; Event 2 (cancelled): no final rate and no no-shows | Summary cards stack; tables scroll |

The member's pass token for Founders' Week can be read from the pass page. The operator can
also print the same value with
`dotnet run --project "ASI.Basecode.WebApp\ASI.Basecode.WebApp.csproj" --no-build -- --demo-pass-token <registrationId>`
under the same `DemoData__TokenKey`.

## 6. Member beats

Sign in as `liza.fernandez@uc.edu.ph`.

| # | Beat | Route / action | Expected outcome | 390px check |
|---|---|---|---|---|
| M1 | Home | `/Home/Index` | Member cards: Browse events, My Passes, My Events, Profile | Cards stack; Menu opens |
| M2 | Browse events | `/Events` | Three seeded events plus the newly published Peer Tutoring Session; covers load; badges show Completed / Check-in open / Signup open | One column; covers keep their aspect ratio |
| M3 | Event detail | `/MemberEvents/Details?id=3` | Confirmed registration panel with Cancel; `?id=2` shows the cancelled state; `?id=1` shows the completed state | Status panel stacks under the hero |
| M4 | Signup | Open the newly published event → Submit registration | "Registration submitted. Your request is pending review."; the panel changes to Pending review | Button and panel fit |
| M5 | My Events | `/MyEvents/Registrations` | Registrations tab lists the confirmed and cancelled records with View event | Tabs wrap; cards stack |
| M6 | Attendance | `/MyEvents/Attendance` | **Known limit:** the tab shows its empty state until 4.7 (M-08); the demo store's check-ins appear on the scanner and report | Empty state fits |
| M7 | Certificates | `/MyEvents/Certificates` | Issued Leadership Summit certificate with View/Download PDF; Founders' Week shows Unavailable; no demo sign | Table scrolls; links reachable |
| M8 | Passes | `/Passes` | Active Founders' Week and Peer Tutoring cards, past Leadership Summit card, QR canvas, Download PNG; no pass for the cancelled registration | One column; QR keeps its quiet zone |
| M9 | Profile | `/Profile` | Identity strip, active badge, account/academic/membership details, mobile number, View my passes | Record grid collapses to one column |

## 7. 390px checks (run after the desktop beats)

1. Open the mobile Menu on each role; every link is present and the current item is marked.
2. Tables scroll inside their own container; the document itself does not scroll horizontally.
3. Dialogs stay centered, fit the viewport, and keep their actions reachable.
4. Event cards and pass cards collapse to one column; the QR is not cropped or stretched.
5. The scanner tabs, pass-code input, and outcome panel are usable without horizontal scroll.
6. The report summary cards stack; the participant table scrolls; breakdown bars stay readable.

## 8. Known limits

- **Member attendance history is empty until 4.7.** The demo store records check-ins for the
  scanner and report, but `MyEventsService.GetAttendanceAsync` is still the real empty read
  (register M-08). Show the scanner log or the report for attendance during the demo.
- **Completion is live.** The demo completes Leadership Summit in O3 through the real 3.12
  service, so completion writes SQL and the Activity Log. A restart returns the scenario's
  events to their seeded status and resets the in-memory store.
- **The Activity Log records real service actions only.** Demo-store check-ins, certificate
  decisions, and issuance do not write SQL or the Activity Log (register rule 4). The
  account-request approval, member creation, and event completion are real and do appear.
- **The demo store resets on restart.** Check-ins, reviews, and issued documents last only for
  the process lifetime; a completed event stays completed in SQL. Passes keep scanning because
  the token is the HMAC of the registration id under `DemoData__TokenKey`.
- **Publishing requires a cover.** The create form saves a draft; add a cover from the Events
  list, then publish. The generic refusal message does not name the missing cover.
- **A pass scanned in the demo uses the rule-10 HMAC token**, not `registrations.qr_token`.
- **`--demo-readiness` needs a provisioned host.** On an unprovisioned machine the runtime SQL
  identity and localhost login checks fail; 8.14 owns host provisioning and evidence.

## 9. Coverage map

| Ticket | Surface | Beats | Rehearsal result |
|---|---|---|---|
| 1.13 / #136 | Shared shell, routes, role navigation, My Events host, access-action slots | A1–A7, O1–O10, M1–M9 | Pass |
| 1.14 | Demo seam, startup guard, seed, event-state commands, check-in control | Pre-flight, O3, O5–O7, known limits | Pass |
| 3.16 | Officer event dossier, roster, review/promote/cancel, completion, check-in window | O2–O4, O7 | Pass |
| 3.17 | My Events host and Registrations tab | M5 | Pass |
| 4.10 | Pass cards, QR, PNG download | M8 | Pass |
| 4.11 | Door scanner, camera/manual modes, outcomes, invalidation | O5–O7 | Pass |
| 4.12 | Member attendance history partial | M6 | Partial: empty until 4.7 (M-08) |
| 5.11 | Account and membership access dialogs | A6 | Pass |
| 6.12 | Certificate ledger, decisions, issuance, PDF viewer | O8–O9 | Pass |
| 6.13 | Member certificate history and download links | M7 | Pass |
| 7.4 | Per-event participation report | O10 | Pass |
| 3.18 | Member feed/detail alignment and registration states | M2–M3 | Partial: waitlisted/cutoff and self-cancellation not exercised (8.12) |
| 4.13 | Profile action to My Passes | M9 | Partial: inactive-member login and certificate access not exercised (8.12) |
| 2.12 | Shared dialog and Event Type editor | A4 | Partial: create/edit, duplicate-name retention, focus and 390px not exercised (8.12) |
| 2.13 | Member editor in the shared dialog | A4 | Partial: correction reason, inline errors, cancel focus and phone width not exercised (8.12) |
| 2.14 | Venue editor in the shared dialog | A4 | Partial: create/edit, photo, invalid upload recovery and keyboard cancel not exercised (8.12) |
| 2.10 | List filters, counts and loading/error states | A3, A4 | Partial: delayed/failed request, retry, no-records vs no-matches not re-triggered (8.12) |
| 8.15 | This rehearsal | All | Pass with the recorded limits |

The six demo-polish rows are Partial: the rehearsal opened their surfaces but did not run their
full Verify lines. They are recorded against their tickets and go to 8.12.
