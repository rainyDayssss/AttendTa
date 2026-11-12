# Hotspot HTTPS evidence — 2026-11-08

This checklist records host-specific evidence for the hotspot HTTPS runbook in
`docs/hotspot-https.md`. It is not evidence by itself. A row may say `PASS` only when the named
operator ran the check on the recorded host and attached redacted output. Rows without a real host
and phone run stay `BLOCKED` with the missing input named.

Never paste passwords, PFX contents, private keys, connection strings or unredacted logs into this
file. Record thumbprints, SANs, dates and revision only.

## Host and certificate

| Field | Value |
|---|---|
| Evidence date | 2026-11-08 |
| Operator | BLOCKED — no operator assigned |
| Host name | BLOCKED — no dedicated demo host selected |
| Hotspot adapter IP (`Hotspot:LanAddress`) | BLOCKED — no hotspot adapter recorded |
| Public host name (`Hotspot:PublicHostName`) | BLOCKED — no hotspot address recorded |
| HTTPS port | BLOCKED — host not configured |
| HTTP loopback port | BLOCKED — host not configured |
| Root CA thumbprint | BLOCKED — certificate not generated on a host |
| Server certificate thumbprint | BLOCKED — certificate not generated on a host |
| Server certificate SANs | BLOCKED — certificate not generated on a host |
| Server certificate validity | BLOCKED — certificate not generated on a host |
| Application revision | BLOCKED — no deployed revision recorded on a real host |
| Firewall rule scope | BLOCKED — host not configured |

## Verification

| # | Check | Evidence required | Result | Missing input / notes |
|---|---|---|---|---|
| 1 | Host HTTPS reachable | Host opens `https://<hotspot-ip>:5443/Account/Login` and receives the login page | BLOCKED | No real host, hotspot adapter address or running app |
| 2 | Certificate chain and SAN | Server certificate chains to the installed root and its SAN contains `Hotspot:PublicHostName`; thumbprints recorded above | BLOCKED | No generated certificate on a real host |
| 3 | Host browser trusted | Host browser shows no certificate warning after the root is trusted | BLOCKED | No host browser run recorded |
| 4 | Phone 1 root CA installed | Root CA installed and trusted on the first officer phone | BLOCKED | No phone available and no real host |
| 5 | Phone 1 Chrome no warning | Chrome on phone 1 opens the hotspot URL with no certificate warning | BLOCKED | No phone available and no real host |
| 6 | Phone 1 scan valid pass | Camera permission granted; a valid pass scans and shows the member name and student number | BLOCKED | No phone, camera run or valid pass on a real host |
| 7 | Phone 1 duplicate scan | Scanning the same pass again shows the duplicate handling | BLOCKED | No phone and no real host |
| 8 | Phone 1 manual lookup | Manual lookup returns the same member when the camera is not used | BLOCKED | No phone and no real host |
| 9 | Phone 1 camera denied | Camera permission denied; manual lookup still works | BLOCKED | No phone and no real host |
| 10 | Phone 2 fresh trust | A second phone that never had the certificate installed trusts the chain after installing only the root CA and scans a pass | BLOCKED | No second phone available |
| 11 | Offline scan | Scan and manual lookup work with the host disconnected from the internet; disconnection method recorded | BLOCKED | No host to disconnect and no phone run |
| 12 | Firewall scope | HTTPS port is reachable from the hotspot subnet and not from another network | BLOCKED | No host or firewall rule to test |

## Sign-off

| Field | Value |
|---|---|
| Reviewer | BLOCKED — no reviewer assigned |
| Decision | BLOCKED — Q2 stays open until a real host, certificate and phone run are recorded |
| Date | BLOCKED — no run recorded |

A closed tracking issue is not proof that this host passed. Only dated, host-specific evidence
with redacted output is.
