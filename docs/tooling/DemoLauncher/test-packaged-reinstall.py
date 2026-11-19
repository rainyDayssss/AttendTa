"""Opt-in Windows/LocalDB end-to-end test. Requires pywinpty and a built Windows package.

Usage: python test-packaged-reinstall.py <verified-v1.1.0-extracted-directory> <new-standalone-exe>
Creates only a uniquely named LocalDB instance and a new temporary fixture directory.
On failure the fixture and instance are preserved for inspection.
"""
import json
import http.cookiejar
import os
from pathlib import Path
import queue
import re
import shutil
import socket
import subprocess
import sys
import tempfile
import threading
import time
import uuid
import urllib.parse
import urllib.request

from winpty import PtyProcess


class Terminal:
    def __init__(self, executable, env):
        self.process = PtyProcess.spawn([str(executable)], cwd=str(executable.parent), env=env,
                                        dimensions=(50, 200))
        self.output = ""
        self.transcript = ""
        self.chunks = queue.Queue()
        def reader():
            try:
                while True:
                    self.chunks.put(self.process.read(4096))
            except EOFError:
                self.chunks.put(None)
        threading.Thread(target=reader, daemon=True).start()

    def expect(self, text, timeout=60):
        deadline = time.monotonic() + timeout
        while text not in self.output:
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                raise RuntimeError(f"Timed out waiting for {text!r}: {self.output}")
            try:
                chunk = self.chunks.get(timeout=remaining)
            except queue.Empty:
                raise RuntimeError(f"Timed out waiting for {text!r}: {self.output}") from None
            if chunk is None:
                raise RuntimeError(f"Terminal exited before {text!r}: {self.transcript}")
            self.output += chunk
            self.transcript += chunk
        self.output = self.output.split(text, 1)[1]

    def send(self, line):
        self.process.write(line + "\r")

    def stop(self):
        if self.process.isalive():
            self.process.terminate(force=True)


def main():
    if os.name != "nt" or len(sys.argv) != 3:
        raise SystemExit(__doc__)
    package = Path(sys.argv[1]).resolve(strict=True)
    standalone = Path(sys.argv[2]).resolve(strict=True)
    for required in ("ASI.Basecode.WebApp.exe", "AttendTa-Demo.exe", "hostpolicy.dll",
                     "demo/attendta-mvp-schema.sql"):
        if not (package / required).is_file():
            raise SystemExit(f"Released package is incomplete: {required}")
    if not standalone.is_file():
        raise SystemExit("New standalone executable is missing.")
    temp_base = Path(os.environ.get("ATTENDTA_TEST_TEMP", tempfile.gettempdir())).resolve(strict=True)
    parent = Path(tempfile.mkdtemp(prefix="attendta-packaged-reinstall-", dir=temp_base)).resolve()
    # Only this copy is removed. The independently extracted release is read-only.
    old_copy = parent / "old-release-copy"
    shutil.copytree(package, old_copy, symlinks=False)
    app = old_copy / "ASI.Basecode.WebApp.exe"
    root, backup, journal = (parent / name for name in ("root", "backup", "journal"))
    credential = parent / "credentials.json"
    instance = "AttendTaDemo_" + uuid.uuid4().hex
    ids = ("admin", "officer", "gabutan", "taguiam", "talisic", "sinco",
           "pinca", "member06", "member07", "member08", "member09", "member10", "applicant")
    credentials = {key: f"TestOnlyPassword{i}x" for i, key in enumerate(ids)}
    credential.write_text(json.dumps(credentials), encoding="utf-8")
    env = os.environ.copy()
    temp = parent / "bundle-extractions"
    temp.mkdir()
    # Only the test's bundle uses this empty temp directory; other installations
    # and their legacy hints are never changed.
    env.update(TEMP=str(temp), TMP=str(temp))
    env.update(ATTENDTA_DEMO_SETUP_ROOT=str(root), ATTENDTA_DEMO_LOCALDB_INSTANCE=instance)
    created = False
    success = False
    terminals = []
    app_process = None
    try:
        subprocess.run(["sqllocaldb", "create", instance], check=True, capture_output=True)
        created = True
        subprocess.run(["sqllocaldb", "start", instance], check=True, capture_output=True)
        setup = subprocess.run([str(app), "--setup-demo", "--confirm-dedicated-localdb"],
                               input=json.dumps(credentials), text=True, env=env,
                               capture_output=True, timeout=180)
        if setup.returncode:
            raise RuntimeError(f"Fixture setup failed: {setup.stdout}\n{setup.stderr}")
        if not (root / ".attendta-demo-owner").is_file():
            raise RuntimeError("Released fixture did not create ownership marker.")
        # This is a test-owned copy, not the user's independently extracted release.
        shutil.rmtree(old_copy)

        hint = temp / "AttendTa" / "app"
        hint.mkdir(parents=True)
        marker = hint / "keep.txt"
        marker.write_text("test fixture", encoding="utf-8")
        legacy = Terminal(standalone, env)
        terminals.append(legacy)
        legacy.expect("Possible v1 installation found at:")
        legacy.expect("Startup is blocked.")
        if not marker.is_file() or backup.exists() or journal.exists():
            raise RuntimeError("Legacy hint refusal changed fixture resources.")
        legacy.send("")
        deadline = time.monotonic() + 15
        while legacy.process.isalive() and time.monotonic() < deadline:
            time.sleep(0.1)
        if legacy.process.isalive():
            raise RuntimeError("Legacy-hint launcher did not exit.")
        marker.unlink()
        hint.rmdir()
        hint.parent.rmdir()

        # Keep an unrelated standalone and its launcher alive. Neither is part of
        # the second standalone's ancestry, even though the image names match.
        blocker = Terminal(standalone, env)
        terminals.append(blocker)
        blocker.expect("Application files extracted to")
        blocker.expect("Choose 1, 2 or 3")

        def launch_to_selection():
            term = Terminal(standalone, env)
            terminals.append(term)
            term.expect("Application files extracted to")
            term.expect("Choose 1, 2 or 3")
            term.send("2")
            term.expect("Continue to identify")
            term.send("y")
            for prompt, value in (
                ("Existing private root", root),
                ("Exact dedicated LocalDB instance name", instance),
                ("Separate private credential JSON file path", credential),
                ("New backup directory", backup),
                ("New journal directory", journal),
            ):
                term.expect(prompt)
                term.send(str(value))
            return term

        refused = launch_to_selection()
        refused.expect("Reinstall declined or refused before removal")
        if backup.exists() or journal.exists() or not root.is_dir():
            raise RuntimeError("Unrelated process refusal changed fixture resources.")
        blocker.send("0")
        deadline = time.monotonic() + 15
        while blocker.process.isalive() and time.monotonic() < deadline:
            time.sleep(0.1)
        if blocker.process.isalive():
            raise RuntimeError("Blocker did not exit.")
        deadline = time.monotonic() + 15
        while refused.process.isalive() and time.monotonic() < deadline:
            time.sleep(0.1)
        if refused.process.isalive():
            raise RuntimeError("Refused launcher did not exit.")

        guided = launch_to_selection()
        for prompt, value in (
            ("Type OFFLINE", "OFFLINE"),
            ("Type the exact source instance name", instance),
            ("Type the exact source root shown above", root),
            ("Type OLD APP REMOVED", "OLD APP REMOVED"),
            ("Type DELETE DEMO DATA", "DELETE DEMO DATA"),
        ):
            guided.expect(prompt)
            guided.send(str(value))
        guided.expect("Clean seed installed and database counts, presenter credentials and 13 banners verified.", 300)
        guided.expect("Reinstall completed.", 30)
        deadline = time.monotonic() + 30
        while guided.process.isalive() and time.monotonic() < deadline:
            time.sleep(0.1)
        if guided.process.isalive() or guided.process.exitstatus != 0:
            raise RuntimeError("Standalone did not exit successfully.")
        if not (backup / "COMPLETE").is_file() or not (journal / "uninstall.jsonl").is_file():
            raise RuntimeError("Backup or journal missing.")
        if json.loads(credential.read_text(encoding="utf-8")) != credentials:
            raise RuntimeError("Credential fixture changed.")
        # Run the newly extracted app directly, with the fixed launcher endpoint.
        # First refuse if any existing process owns the port; do not stop it.
        with socket.socket() as probe:
            probe.bind(("127.0.0.1", 5000))
        extracted = re.search(r"Application files extracted to ([^\r\n]+)", guided.transcript)
        if not extracted:
            raise RuntimeError("New standalone did not report its extraction directory.")
        new_app = Path(extracted.group(1).strip()) / "ASI.Basecode.WebApp.exe"
        if not new_app.is_file():
            raise RuntimeError("Newly extracted application is missing.")
        app_env = env.copy()
        app_env.update(ASPNETCORE_ENVIRONMENT="Production", Hotspot__Enabled="false",
                       Common__SetupRoot=str(root),
                       ConnectionStrings__DefaultConnection=fr"Server=(localdb)\{instance};Database=AttendTa_Demo;Integrated Security=true;TrustServerCertificate=true")
        app_process = subprocess.Popen([str(new_app), "--demo-launcher"], cwd=str(new_app.parent),
                                       env=app_env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
        announcements = queue.Queue()
        def read_app_output():
            for line in app_process.stdout:
                announcements.put(line)
            announcements.put(None)
        threading.Thread(target=read_app_output, daemon=True).start()
        base = "http://127.0.0.1:5000"
        deadline = time.monotonic() + 60
        nonce = None
        while True:
            if app_process.poll() is not None:
                raise RuntimeError("New demo app exited before becoming ready.")
            try:
                line = announcements.get(timeout=max(0.01, deadline - time.monotonic()))
            except queue.Empty:
                raise RuntimeError("New demo app did not announce readiness.") from None
            if line is None:
                raise RuntimeError("New demo app exited before announcing readiness.")
            match = re.fullmatch(r"ATTENDTA_LAUNCH_READY http://127\.0\.0\.1:5000 ([A-F0-9]{64})\s*", line)
            if match:
                nonce = match.group(1)
                break
        with urllib.request.urlopen(base + "/_demo-launcher-ready", timeout=5) as response:
            if response.read().decode() != nonce or app_process.poll() is not None:
                raise RuntimeError("Readiness response did not match the test-owned application.")
        emails = ("alexmercado@gmail.com", "jamievillareal@gmail.com", "jerhongabutan@gmail.com",
                  "jaspertaguiam@gmail.com", "jhontalisic@gmail.com", "johnpaulsinco@gmail.com",
                  "sethpinca@gmail.com", "rileysarmiento@gmail.com", "drewalonzo@gmail.com",
                  "caseyvaldez@gmail.com", "julesnavarro@gmail.com", "noelsoriano@gmail.com")
        admin = None
        for key, email in zip(ids[:12], emails):
            opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
            html = opener.open(base + "/Account/Login", timeout=10).read().decode("utf-8")
            token = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html)
            if not token:
                raise RuntimeError("Login antiforgery token not found.")
            data = urllib.parse.urlencode({"Email": email, "Password": credentials[key],
                                            "__RequestVerificationToken": token.group(1)}).encode()
            response = opener.open(base + "/Account/Login", data=data, timeout=30)
            if "/Account/Login" in response.geturl():
                raise RuntimeError(f"Login failed for fixture account {key}.")
            if key == "admin":
                admin = opener
        for route, count in (("/Venue/Photo?id=", 5), ("/EventCover/Image?id=", 8)):
            for identifier in range(1, count + 1):
                response = admin.open(base + route + str(identifier), timeout=15)
                if response.status != 200 or not response.headers.get_content_type().startswith("image/"):
                    raise RuntimeError(f"Seeded image endpoint failed: {route}{identifier}")
        app_process.terminate()
        app_process.wait(timeout=30)
        app_process = None
        success = True
        print("PASS: released v1.1.0 provisioned; guided reinstall completed; 12 HTTP logins and 13 image endpoints passed; backup, journal and credentials preserved.")
    finally:
        if app_process is not None:
            app_process.terminate()
            app_process.wait(timeout=30)
        for term in terminals:
            term.stop()
        if success:
            if created:
                subprocess.run(["sqllocaldb", "stop", instance, "-k"], check=True, capture_output=True)
                subprocess.run(["sqllocaldb", "delete", instance], check=True, capture_output=True)
            shutil.rmtree(parent)
        else:
            print(f"Test failed; preserve fixture {parent} and instance {instance} for inspection.", file=sys.stderr)


if __name__ == "__main__":
    main()
