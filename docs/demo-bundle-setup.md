# First-time Windows demo setup

This experimental package is not release-verified. Setup requires Windows, SQL Server
LocalDB and a dedicated private LocalDB instance. The standalone executable extracts
application files; it does not create a database or collect passwords. The operator
must establish isolation and restrictive filesystem permissions; the software does not
verify ACLs. Do not use production data or send email to demo addresses.

## 1. Extract the application

In PowerShell, run the standalone executable with `--extract-only`. For example, from
the directory containing it:

```powershell
.\AttendTa-Demo-Standalone.exe --extract-only
```

Record the application directory printed by the program; it is a new temporary
directory. Do not delete it while the app is running. If using the Windows ZIP instead,
extract the entire ZIP to a directory accessible to the operator and runtime identity.
In the commands below, set `$appDir` to that extracted directory.

## 2. Prepare an isolated target and private credentials

Obtain host approval. Independently verify the LocalDB instance is owned by the
operator, unshared, dedicated to this demo, and contains no user databases. Create a
new instance if needed using `SqlLocalDB`; an instance name alone does not establish
isolation. The accepted name is `AttendTaDemo`, or `AttendTaDemo_` followed by exactly
32 hexadecimal characters.

Choose a **new, nonexistent** setup-root child under an existing private parent outside
the extracted application, source checkout, deployment tree and web root. Restrict
access to the operator and runtime identity. Setup refuses an existing root or an
instance containing any user database.

Receive or create a JSON object in a separate private file outside the application and
checkout, with access restricted to the authorized operator. It must contain exactly
these case-sensitive property names, each mapped to a different password string:

`admin`, `officer`, `gabutan`, `taguiam`, `talisic`, `sinco`, `pinca`, `member06`,
`member07`, `member08`, `member09`, `member10`, and `applicant`.

Each password must be 8–128 characters, contain an ASCII letter and a digit, and have
no control characters. Use a proper JSON editor or serializer to escape special
characters. Never put passwords or hashes in command arguments, logs, shared files,
the bundle, or source control. These keys identify credentials, not login addresses.
Store and deliver credentials separately using your approved secure process.

## 3. Provision once

In a protected PowerShell session, substitute paths and the exact instance name; do
not place credentials in these commands. The same instance name must be used for
provisioning and for the launcher later.

```powershell
$appDir = '<extracted application directory>'
$credentialFile = '<private JSON file path>'
$env:ATTENDTA_DEMO_LOCALDB_INSTANCE = '<dedicated instance name>'
$env:ATTENDTA_DEMO_SETUP_ROOT = '<new private setup-root child>'
Get-Content -Raw -LiteralPath $credentialFile | & (Join-Path $appDir 'ASI.Basecode.WebApp.exe') --setup-demo --confirm-dedicated-localdb
```

The confirmation asserts that you verified the dedicated instance; it is not proof
of isolation. A success message does not establish that sign-in, images or scenarios
work. On failure, **stop**: setup can leave a partial database or root, and refuses
retry while either exists. Obtain separate approval and verify the exact target before
any cleanup. No destructive reset is supplied.

## 4. Launch and check

Run `AttendTa-Demo.exe` in the extracted directory. Enter the same setup root and the
same dedicated instance name when prompted. The app binds IPv4 loopback port 5000 and
opens `http://127.0.0.1:5000/Account/Login` (also reachable at
`http://localhost:5000` where localhost resolves to IPv4). If the port is occupied,
startup fails; stop or reconfigure the other service only with its owner's approval.
Keep the launcher window open; press Enter there to stop the app. The launcher does not
provision accounts or accept passwords.

Unlike the v1 release, the current standalone bundle does not support `--uninstall`.
It never automatically deletes extracted files, databases, credentials, or the private
setup root. Arrange separately approved cleanup after verifying ownership and stopping
the app; preserve any data that must be retained.

The launcher checks for the documented v1 location `%TEMP%\AttendTa\app` and asks
whether to continue separately if that path exists. The path alone does not establish
that it belongs to AttendTa or which version it contains. This release does not
overwrite or upgrade the old installation. Before considering a manual uninstall,
verify the exact legacy executable and target, back up any needed database, uploads,
keys and credentials, and inspect the old release's uninstall instructions and effects.
Do not run a legacy `--uninstall` until its exact target and consequences are approved.
Extract-only mode does not run this launcher check; run the launcher after provisioning.

The launcher also checks TCP listeners on port 5000 before startup. A listener might
belong to an unrelated service; identify its owner (for example, with
`Get-NetTCPConnection -State Listen -LocalPort 5000` and `Get-Process -Id <OwningProcess>`)
and obtain approval before stopping or reconfiguring it. The check is advisory and
another process can take the port afterward; startup can still fail. No existing
installation or conflicting process is removed or stopped by this check.
Authenticate using the separately supplied credentials and these login addresses:

| Credential key | Login email |
|---|---|
| `admin` | `alexmercado@gmail.com` |
| `officer` | `jamievillareal@gmail.com` |
| `gabutan` | `jerhongabutan@gmail.com` |
| `taguiam` | `jaspertaguiam@gmail.com` |
| `talisic` | `jhontalisic@gmail.com` |
| `sinco` | `johnpaulsinco@gmail.com` |
| `pinca` | `sethpinca@gmail.com` |
| `member06` | `rileysarmiento@gmail.com` |
| `member07` | `drewalonzo@gmail.com` |
| `member08` | `caseyvaldez@gmail.com` |
| `member09` | `julesnavarro@gmail.com` |
| `member10` | `noelsoriano@gmail.com` |

`applicant` belongs to the seeded pending request `applicant@gmail.com` and can sign
in only after approval. Validate logins, images and the presenter scenarios before use.
All identities are fictional; their addresses may belong to real people. Never email
them. Setup does not fabricate issued certificate PDFs; issuance requires the app's
certificate configuration and Chromium.
