# Hotspot HTTPS — plain-English guide

This is the simple version of the real-world test for Story 8.2. The code is already built. These
steps are for the person who runs the demo on the actual laptop and phones.

For the full technical runbook, see `docs/hotspot-https.md`. For the evidence table, see
`docs/hotspot-https-evidence.md`.

## The big picture

Think of the app like a building with a front desk:

- The **laptop** is the building. It runs AttendTa.
- The **hotspot** is the temporary road to the building. The laptop gets an address on that road,
  usually something like `192.168.137.1`.
- The **phone** is the visitor. It needs two things:
  1. The **address** of the building — the hotspot IP.
  2. A **trusted ID check** so it believes the building is real — the certificate.

The app creates two certificates:

| Certificate | Simple meaning | Where it goes |
|---|---|---|
| **Root certificate** (`attendta-hotspot-root-ca.cer`) | A trusted stamp. Install it once on the phone. | Every officer phone |
| **Server certificate** (`attendta-hotspot-server.pfx`) | The building’s ID card. It contains the hotspot IP. | The laptop only |

If the phone has the root stamp and the server ID matches the address, Chrome shows **no warning**
and allows the camera.

Never copy the `.pfx` files to a phone. They contain private keys.

## Step 1 — Find the hotspot address and create the certificates

**What it means:** Find the laptop’s address on the hotspot, then create the certificates for that
exact address.

1. Turn on the Windows hotspot.
2. Open PowerShell and run:

   ```powershell
   ipconfig
   ```

3. Look for the hotspot adapter. It usually shows an IPv4 address like `192.168.137.1`. That is the
   address phones will use.
4. Run the certificate command with that address:

   ```powershell
   $env:ATTENDTA_CERT_PASSWORD = "choose-a-strong-password"
   dotnet .\ASI.Basecode.WebApp.dll --new-hotspot-cert `
       --host 192.168.137.1 `
       --out C:\AttendTa\hotspot-certificates
   ```

**Why:** The certificate must contain the exact address the phone will type. If the certificate says
`192.168.137.1` but the phone opens `192.168.137.2`, Chrome will warn.

The command writes four files:

| File | What it is | What to do with it |
|---|---|---|
| `attendta-hotspot-root-ca.cer` | Public root certificate | Copy to phones |
| `attendta-hotspot-root-ca.pfx` | Root private key | Keep on the laptop only |
| `attendta-hotspot-server.cer` | Public server certificate | Optional inspection |
| `attendta-hotspot-server.pfx` | Server certificate and key | The app uses it |

The command prints only thumbprints and names. It never prints the password.

## Step 2 — Install only the public root CA on each officer phone

**What it means:** Put the **root certificate** on the phone and mark it as trusted. Do **not** copy
the `.pfx` files.

**Android (Chrome):**

1. Copy `attendta-hotspot-root-ca.cer` to the phone.
2. Open Settings → Security → Encryption & credentials → Install a certificate → CA certificate.
3. Choose the `.cer` file and accept the warning.

**iPhone (Chrome and Safari):**

1. Send the `.cer` file to the phone and install the profile.
2. Go to Settings → General → VPN & Device Management → AttendTa Hotspot Root CA → Install.
3. Go to Settings → General → About → Certificate Trust Settings and turn on full trust.

**Why:** This is the trusted stamp. Without it, Chrome says the certificate is not trusted.

## Step 3 — Open the hotspot URL with the internet off

**What it means:** Turn off the laptop’s internet (Wi-Fi and mobile data), but keep the hotspot on.
On the phone, open:

```text
https://192.168.137.1:5443
```

Replace `192.168.137.1` with the address you found in Step 1.

**Why:** The demo must work without internet. If Chrome shows a warning, the root is not installed
or the address is not in the certificate.

## Step 4 — Test the camera scan and manual lookup

**What it means:** Do the actual door-scanner test.

1. Sign in as an **Officer**.
2. Open the **Check-in** page.
3. Allow camera access.
4. Scan a member’s QR pass.
5. Confirm the member’s **name** and **student number** appear.
6. Scan the same pass again — confirm it says already checked in, not a second record.
7. Switch to **Manual lookup**, search the same member, and confirm it finds them.
8. Deny camera access once and confirm manual lookup still works.

**Why:** This proves the camera works over HTTPS and the manual fallback still works.

## Step 5 — Repeat on a second phone and record the offline run

**What it means:** Use a phone that has never had the app or server certificate installed. Install
**only the root certificate** on it, then repeat the scan. Also do the scan with the laptop
disconnected from the internet.

**Why:** This proves the trust comes from the certificate itself, not from one phone’s special
setup. The offline run proves the demo does not secretly need the internet.

## Step 6 — Record the result

Open `docs/hotspot-https-evidence.md` and fill in the table with real results:

- Date
- Host name
- Hotspot IP
- Certificate thumbprints
- Phone model
- What happened for each check: `PASS` or `BLOCKED`
- If blocked, what is missing

**Do not write passwords, private keys, or PFX contents.** Only dates, addresses, thumbprints, and
results.

**Why:** The instructor and reviewer need proof that a real phone did the scan. A closed GitHub
issue is not proof. Until someone writes a real result, the rows stay `BLOCKED`.

## Printable checklist

- [ ] Hotspot turned on
- [ ] Hotspot IP found (`ipconfig`)
- [ ] `--new-hotspot-cert` run with that IP
- [ ] Four certificate files created
- [ ] Root CA installed on phone 1
- [ ] Phone 1 opens `https://<hotspot-ip>:5443` with no warning
- [ ] Phone 1 camera permission granted
- [ ] Phone 1 scans a valid pass and shows name + student number
- [ ] Phone 1 scans the same pass again and shows duplicate handling
- [ ] Phone 1 manual lookup finds the same member
- [ ] Phone 1 camera denied; manual lookup still works
- [ ] Root CA installed on phone 2 (only the root)
- [ ] Phone 2 scan works
- [ ] Laptop disconnected from the internet
- [ ] Offline scan still works
- [ ] Results written in `docs/hotspot-https-evidence.md`
- [ ] No password, private key, or PFX content written anywhere

## Why this is still `BLOCKED` in the repository

This development environment has no real hotspot, no officer phone, and no camera. The mechanism,
runbook, and checklist are built and tested, but nobody can truthfully claim “a phone scanned over
the hotspot” until the steps above are run on the real demo host. That is why Q2 stays open and
issue #75 is not closed by the code change alone.
