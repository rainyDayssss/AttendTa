# Hotspot HTTPS for phone camera scanning

This is the Q2 runbook: how the demo host serves trusted HTTPS on the Windows hotspot so an
officer's phone can open the check-in scanner in Chrome. It is a procedure, not evidence. Record a
real host, certificate and phone run in `docs/hotspot-https-evidence.md`; rows without one stay
`BLOCKED`.

## Decision (Q2)

- **Mechanism:** a repo-generated local root CA issues a 397-day server certificate. Phones trust
  the public root certificate; the root private key stays on the host.
- **Serving:** Kestrel binds HTTPS directly on the hotspot adapter IP. There is no IIS, reverse
  proxy, cloud service or background job (AD-16).
- **Readiness:** HTTP stays on `127.0.0.1` so the `--demo-readiness` login check keeps its HTTP 200
  contract. LAN clients use HTTPS directly; the loopback endpoint is not redirected.
- **Address rule:** use the hotspot adapter IP in the URL (for example
  `https://192.168.137.1:5443`). HSTS does not apply to IP addresses and hotspot DNS is
  unreliable, so the server certificate SAN must contain that exact IP or DNS name.
- **Bind rule:** `Hotspot:LanAddress` must be the hotspot adapter IP. `0.0.0.0`, `::`, `127.0.0.1`
  and `::1` are refused so the app is not exposed on another network.

If the hotspot adapter address changes, reissue the server certificate for the new address and
update `Hotspot:LanAddress` and `Hotspot:PublicHostName`.

## Generate the certificates

The command reads the PFX password from `ATTENDTA_CERT_PASSWORD` and never prints it. Keep the
password in an approved secret store, not in shell history, command arguments, logs or evidence.

In a protected PowerShell session, with the built `ASI.Basecode.WebApp.dll`:

```powershell
$secure = Read-Host 'Hotspot certificate password' -AsSecureString
$ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try { $env:ATTENDTA_CERT_PASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr); $secure.Dispose() }

try {
    dotnet .\ASI.Basecode.WebApp.dll --new-hotspot-cert `
        --host 192.168.137.1 `
        --host attendta-demo.local `
        --out C:\AttendTa\hotspot-certificates
    if ($LASTEXITCODE -ne 0) { throw 'Hotspot certificate generation failed' }
}
finally { Remove-Item Env:ATTENDTA_CERT_PASSWORD -ErrorAction SilentlyContinue }
```

Use `--host` once per DNS name or IP address that must appear in the SAN. The first host becomes
the certificate subject. `--out` defaults to `hotspot-certificates` under the current directory.
The command refuses to overwrite an existing set so the root private key is not lost.

Four files are written:

| File | Purpose | Where it goes |
|---|---|---|
| `attendta-hotspot-root-ca.cer` | Public root CA certificate | Install on every phone |
| `attendta-hotspot-root-ca.pfx` | Root CA private key | Host only; never copy, commit or email |
| `attendta-hotspot-server.cer` | Public server certificate | Optional inspection only |
| `attendta-hotspot-server.pfx` | Password-protected server certificate and key | Host only; Kestrel loads it |

Never commit a PFX, a password or a private key. Add the output directory to `.gitignore` if it is
inside the repository.

## Configure the host

Set the section in the app process environment (double underscore maps to `:`):

```powershell
$env:Hotspot__Enabled = 'true'
$env:Hotspot__LanAddress = '192.168.137.1'
$env:Hotspot__HttpsPort = '5443'
$env:Hotspot__HttpPort = '5080'
$env:Hotspot__PublicHostName = '192.168.137.1'
$env:Hotspot__CertificatePath = 'C:\AttendTa\hotspot-certificates\attendta-hotspot-server.pfx'
$env:Hotspot__CertificatePassword = '<runtime secret>'
```

`PublicHostName` must be one of the SAN entries. `HttpsPort` and `HttpPort` must differ. The
certificate path must be absolute. Hotspot mode configures the Kestrel listeners explicitly, so
leave `ASPNETCORE_URLS` unset; it does not add a second listener in this mode.

Startup validates before listening and refuses with a message naming the key when:

- the PFX is missing (`Hotspot:CertificatePath`);
- the password is wrong (`Hotspot:CertificatePassword`);
- the certificate has no private key, is outside its validity window, lacks the server-auth EKU,
  or its SAN does not contain `Hotspot:PublicHostName`;
- `Hotspot:LanAddress` is a wildcard or loopback address;
- the ports are equal or out of range.

The password is never printed.

## Open the firewall

Allow only the hotspot subnet to reach the HTTPS port:

```powershell
New-NetFirewallRule -DisplayName 'AttendTa hotspot HTTPS' `
    -Direction Inbound -Protocol TCP -LocalPort 5443 `
    -RemoteAddress 192.168.137.0/24 -Action Allow
```

Adjust the subnet to the hotspot adapter's actual network. Do not open the port to every network.

## Start and check the host

1. Start the app normally under the service identity with the `Hotspot__*` variables set. The app
   imports the PFX into that identity's user key store, so the identity needs a loaded user profile
   (a dedicated service account is fine). If the key store is unavailable, pre-import the PFX for
   that identity or use a machine key store before starting.
2. Confirm the loopback readiness endpoint still returns HTTP 200:
   `http://127.0.0.1:5080/Account/Login`.
3. On the host, open `https://<hotspot-ip>:5443/Account/Login`. Trust the root CA in the Windows
   Trusted Root store first, or expect a one-time warning.
4. Run `--demo-readiness` with `ATTENDTA_READINESS_LOGIN_URL` set to the loopback HTTP URL.

## Trust the root on a phone

Only `attendta-hotspot-root-ca.cer` goes to the phone. Transfer it by USB, local file share or
AirDrop; never send the root PFX.

**Android (Chrome):**

1. Copy the `.cer` to the phone and open it, or use
   `Settings > Security > Encryption & credentials > Install a certificate > CA certificate`.
2. Accept the "your data may be monitored" warning for the local CA.
3. Open `https://<hotspot-ip>:5443/Account/Login` in Chrome. There must be no certificate warning.

**iOS (Chrome and Safari):**

1. Transfer the `.cer` and install the profile
   (`Settings > General > VPN & Device Management > AttendTa Hotspot Root CA > Install`).
2. Enable full trust in `Settings > General > About > Certificate Trust Settings`.
3. Open the URL in Chrome. There must be no certificate warning.

iOS rejects server certificates whose lifetime exceeds 398 days, which is why the generated server
certificate is valid for 397 days. Do not extend it.

## Verify scanning on a phone

Record each result in `docs/hotspot-https-evidence.md` with the date, host and revision.

1. Sign in as an officer and open the check-in scanner.
2. Grant camera access and scan a valid pass. Confirm the member name and student number.
3. Scan the same pass again and confirm the duplicate handling.
4. Use manual lookup and confirm it returns the same member.
5. Deny camera access and confirm manual lookup still works.
6. Disconnect the host from the internet and repeat the scan. Trust and scanning must not depend
   on internet access.
7. Repeat on a second phone that has never had the app or server certificate installed, after
   installing only the public root CA, to show trust comes from the certificate chain and not from
   device setup.

File any scanner defect against stories 4.4/4.5. Do not change scanner code here.

## Rotation

- The root CA is valid for 10 years; the server certificate for 397 days.
- Reissue before the server certificate expires, or when the hotspot address changes.
- The command creates a new root CA as well, so install the new root on every phone. Keep the old
  output directory until every phone is updated, then delete it securely.
- Replace `Hotspot:CertificatePath` and `Hotspot:CertificatePassword`, restart the app and repeat
  the phone verification.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| `NET::ERR_CERT_AUTHORITY_INVALID` | Root CA not installed, or not trusted on iOS |
| `NET::ERR_CERT_COMMON_NAME_INVALID` | URL host is not in the certificate SAN |
| `ERR_CONNECTION_REFUSED` | Wrong `LanAddress`, app not running, or firewall rule missing |
| Startup refuses with `Hotspot:CertificatePath` | PFX path is missing or not absolute |
| Startup refuses with `Hotspot:CertificatePassword` | Wrong PFX password, or the service identity cannot open its key store |
| Startup refuses with `Hotspot:LanAddress` | Wildcard or loopback address supplied |
| Readiness login fails | App is not listening on the loopback HTTP port |
