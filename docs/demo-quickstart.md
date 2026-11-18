# Historical demo quick start — do not use for provisioning

The former rehearsal procedure is retired for release #269. Its reset and scenario
seed can modify existing records, reused a member password hash, and its credential
handling put secrets in command arguments or SQL wrapper files. It also described
scenario states that do not match the fresh seed. Do not run that procedure or
re-run the historical seed to repair a demo.

For the experimental fresh-only Windows LocalDB procedure, use
[fresh local setup](demo-fresh-setup-269.md). For a packaged Windows executable,
follow `FIRST-TIME-SETUP.md` inside the extracted bundle; it includes private
credential input and first-time provisioning. Neither procedure authorizes a reset.
Release readiness and remaining verification are documented in
[release verification](release-269-verification.md).

## Operating guidance

- Check the [scenario matrix](demo-269-scenario-matrix.md) for current starting
  states and blocked beats rather than assuming historical assertions still apply.
- Camera check-in needs a supported browser, permission and a secure context
  (HTTPS or localhost). Use manual lookup if camera scanning is unavailable.
- If the connection string or private setup root is missing, check the launcher
  inputs or the application environment configuration for the exact provisioned
  instance and root. Do not substitute a different database.
- An empty or unexpected starting state warrants stopping and inspecting the
  configured target; do not rerun the old seed.
- The app has no automated demo reset. Obtain separate explicit approval and verify
  the exact disposable target before any cleanup or recreation.
- Do not email fictional demo addresses: they may belong to real people.

For non-demo local database development, see [database setup](database-setup.md).
For host prerequisites, see [demo host setup](demo-host-setup.md), subject to its
release #269 warnings.
