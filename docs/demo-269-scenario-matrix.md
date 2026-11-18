# Release #269 presenter scenario matrix

These rows supersede the historical rehearsal guide's starting-state claims. Results
refer to the uncommitted working-tree checks in
[release verification](release-269-verification.md), not an approved release candidate.
Those checks predate the expanded workshop and mentoring-lab seed and do not verify it.
For the new starting state, see [fresh setup](demo-fresh-setup-269.md); repeat the
scenarios on a new approved isolated setup before relying on any result below.
`BLOCKED` means not exercised on the seeded release setup, even if separate feature
tests pass. Run in order on a fresh, approved isolated setup; every write changes it.
The fictional `@gmail.com` addresses may belong to real people: never send email to
them, and do not use actual production data.

| Beat | Scenario | Prerequisite / order | Result |
|---|---|---|---|
| A1 | Administrator home and navigation | Fresh setup, administrator sign-in | PARTIAL: home HTTP and identity passed; navigation interactions not checked |
| A2 | Approve Taylor Ramos's pending request | Before A3; use applicant's separate credential after approval | PASS: HTTP approval, linked account/member in SQL, applicant HTTP sign-in |
| A3 | Search approved applicant in members | A2 | PASS: HTTP search by student number returned applicant |
| A4 | Member, event-type and venue editing | Fresh setup; two venue photos | BLOCKED: venue photo HTTP retrieval passed, editing not exercised |
| A5 | Create, cover and publish an event | Upload valid cover before publishing; before M4 | PASS: HTTP multipart create with PNG and publish; SQL confirmed published status and cover |
| A6 | Preview and change account/membership access | After actions requiring that account; never deactivate last administrator | BLOCKED |
| A7 | Inspect activity log | After approval, check-ins and other writes | PARTIAL: HTTP page returned 200; audit entry contents not checked |
| O1 | Officer home and navigation | Officer sign-in | PARTIAL: home HTTP and identity passed; navigation interactions not checked |
| O2 | Inspect three event dossiers | Fresh setup; find events by title rather than fixed IDs | PASS: each dossier returned HTTP 200 |
| O3 | Complete CCS AI and Responsible Computing Summit | Start is in the past; before certificate review, after any required check-in | PASS: HTTP command followed by SQL completed status; no summit attendance seeded |
| O4 | Review Sinco's pending assembly registration | Fresh setup | PASS: HTTP approval and SQL confirmed registration |
| O5 | Scan Gabutan's assembly pass | Officer opens check-in first; camera requires a secure context | BLOCKED |
| O6 | Manual lookup | Before or after check-in | PASS: HTTP lookup showed Gabutan; manual check-in created attendance |
| O7 | Invalidate a check-in | After check-in; reason required | PASS: HTTP invalidation followed by SQL invalidated timestamp |
| O8 | Certificate eligibility review | Open summit check-in, record valid attendance, complete summit; no attendance is seeded | PASS: HTTP eligibility approval after manual check-in and completion; SQL confirmed eligible |
| O9 | Issue and open PDF | O8 approval, configured signatory and staged Chromium | PARTIAL: HTTP issuance, SQL issued status and member-authenticated PDF HTTP response passed; viewer and PDF visual layout not checked |
| O10 | Participation report | O3 and relevant attendance decisions | PARTIAL: completed summit report HTTP and title passed; figures not checked |
| M1 | Member home | Member sign-in | PARTIAL: home HTTP and identity passed; navigation interactions not checked |
| M2 | Browse event feed and covers | Existing events; A5 for the newly created event | PARTIAL: feed HTTP showed assembly; cover display and new event not checked |
| M3 | Inspect event details | Fresh setup; choose by title | BLOCKED |
| M4 | Submit registration | A5 published event with registration open | PASS: Gabutan HTTP registration; SQL confirmed pending |
| M5 | My Events registrations | Fresh setup | PARTIAL: HTTP page showed assembly; all registration states not checked |
| M6 | Attendance history | O5 and optionally O7 | BLOCKED |
| M7 | Certificate history and download | O9 | BLOCKED |
| M8 | View and download pass | Confirmed registration; M4 additionally needs officer approval | PARTIAL: HTTP pass page showed assembly; QR and PNG download not checked |
| M9 | Profile | Member sign-in | PARTIAL: HTTP page showed Gabutan; full fields not checked |
| Mobile | 390px, camera and keyboard checks | Appropriate browser and secure context | BLOCKED |

**Reset safeguard:** No supported automated reset is available. Fresh setups have a
read-only paired-marker inspection, but neither it nor the instance name, database
name or a path proves authorization to delete both database and private store.
Stop after a rehearsal; obtain separate explicit approval and independently verify the
dedicated instance, each database's physical files, and the exact private store before
any deletion. Never run the historical reset script against an assumed target.
