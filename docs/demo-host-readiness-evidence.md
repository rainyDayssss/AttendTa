# Demo host readiness evidence — 2026-11-06

This checklist records host-specific evidence for the demo deployment described in
`docs/demo-host-setup.md`. It is not evidence by itself. A row may say `PASS` only when the
named operator ran the check on the recorded host and attached redacted output. Closed tracking
issues (including #74) are not proof. Rows without a real host run stay `BLOCKED` with the
missing input named.

Never paste connection strings, passwords, hashes, API keys, or unredacted logs into this file.

## Host and runtime

| Field | Value |
|---|---|
| Evidence date | 2026-11-06 |
| Operator | BLOCKED — no operator assigned |
| Host name | BLOCKED — no dedicated demo host selected |
| OS version | BLOCKED — host not provisioned |
| CPU / RAM | BLOCKED — host not provisioned |
| .NET 10 SDK/runtime version | BLOCKED — host not provisioned |
| SQL Server version | BLOCKED — host not provisioned |
| Application revision | BLOCKED — no deployed revision recorded on a real host (planned baseline: `04c6d9ec794478450da8b9b68a9edb1c297106a3`) |
| DNS name / address | BLOCKED — decision owner not named |
| TLS certificate approach | BLOCKED — decision owner not named |
| Windows service account | BLOCKED — no dedicated identity selected |
| SQL runtime login | BLOCKED — no dedicated login provisioned |
| `Common__SetupRoot` | BLOCKED — private root not chosen |
| `PLAYWRIGHT_BROWSERS_PATH` | BLOCKED — browser store not staged |
| Disconnection method | BLOCKED — offline run not scheduled |

## Readiness checks

| # | Check | Evidence required | Result | Missing input / notes |
|---|---|---|---|---|
| 1 | Database connect | `--demo-readiness` SQL check connects to `AttendTa_Demo` with the runtime connection string; `SELECT DB_NAME()` is `AttendTa_Demo` | BLOCKED | No `AttendTa_Demo` database and runtime connection string on a real host |
| 2 | Server denial | Effective server permissions contain only `CONNECT SQL` and `VIEW ANY DATABASE`; no direct or custom-role privilege | BLOCKED | No dedicated SQL login available to validate |
| 3 | Table/column audit denial | Table-level `UPDATE`/`DELETE` and every column-level `UPDATE` on `activity_logs` are absent | BLOCKED | No provisioned runtime login or isolated database |
| 4 | Rollback probe | `readiness.probe` insert runs in a transaction that is rolled back; `@@TRANCOUNT` is 0 and no probe row remains | BLOCKED | No host database to run the probe against |
| 5 | SetupRoot and logs | Six private areas exist; `logs/<yyyyMM>` is created and probed; no `.readiness-*` files remain; ACLs inspected separately | BLOCKED | No private `Common__SetupRoot` or service identity |
| 6 | Offline Chromium/PDF | Staged headless Chromium launches while disconnected and renders a non-empty PDF | BLOCKED | Browser not staged and host not disconnected |
| 7 | Localhost login | App running under the service identity returns HTTP 200 for `http://127.0.0.1:<port>/Account/Login` without following redirects | BLOCKED | App not running on a demo host |
| 8 | Argon2 benchmark | `--hash-password-env` elapsed milliseconds recorded with `m=19456 KiB, t=2, p=1`; disposable secret and hash discarded | BLOCKED | Built app not deployed; benchmark not run |
| 9 | Runtime and secrets | Service environment protected; runtime identity matches the check; setup secrets cleared after use | BLOCKED | No service identity or protected environment configured |
| 10 | Host/address/certificate decision | Named owner records the host, bind address, and certificate approach; phone scanning remains story 8.2 | BLOCKED | Decision owner not named; mechanism recorded in `docs/hotspot-https.md` (local root CA, direct Kestrel HTTPS on the hotspot adapter IP, HTTP loopback for readiness); real host address, certificate and phone evidence still missing |

The Q2 mechanism is recorded in `docs/hotspot-https.md` with its dated checklist in
`docs/hotspot-https-evidence.md`. The host address, certificate thumbprints and phone scan results
remain `BLOCKED` until a real host run exists; a recorded mechanism is not host evidence.

## Disconnected run

| Field | Value |
|---|---|
| Disconnection date/time | BLOCKED — run not scheduled |
| Host identity during run | BLOCKED — no service identity |
| Disconnection method | BLOCKED — not scheduled |
| Redacted PASS/FAIL output | BLOCKED — no run recorded |
| Exit code | BLOCKED — no run recorded |
| Connectivity restored | BLOCKED — no run recorded |

Do not mark the offline row `PASS` from an online run. An online readiness pass does not prove
offline operation; only a disconnected run with recorded, redacted output does.
