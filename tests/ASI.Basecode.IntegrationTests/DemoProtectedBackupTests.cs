using System.Data;
using System.Diagnostics;
using System.Text.Json;
using ASI.Basecode.WebApp.Demo;
using Microsoft.Data.SqlClient;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class DemoProtectedBackupTests
{
    public static bool Enabled => OperatingSystem.IsWindows() &&
        Environment.GetEnvironmentVariable("ATTENDTA_TEST_DEMO_BACKUP") == "1";

    [Fact(SkipUnless = nameof(Enabled), Skip = "Requires an explicitly enabled isolated LocalDB test.")]
    public async Task BacksUpOnlyNewDedicatedFixtureAndLeavesSourceIntact()
    {
        var instance = "AttendTaDemo_" + Guid.NewGuid().ToString("N");
        var restoreInstance = "AttendTaDemo_" + Guid.NewGuid().ToString("N");
        var recoveryInstance = "AttendTaDemo_" + Guid.NewGuid().ToString("N");
        var partialRecoveryInstance = "AttendTaDemo_" + Guid.NewGuid().ToString("N");
        var parent = Path.Combine(Path.GetTempPath(), "attendta-backup-test-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(parent, "source");
        var backup = Path.Combine(parent, "backup");
        var credentialFile = Path.Combine(parent, "credentials.json");
        var created = false;
        var restoreCreated = false;
        var recoveryCreated = false;
        var partialRecoveryCreated = false;
        var ct = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(parent);
        try
        {
            Assert.Equal(0, await LocalDb("create", instance));
            created = true;
            Assert.Equal(0, await LocalDb("start", instance));
            foreach (var area in new[] { "database", "keys", "uploads/events", "uploads/venues", "certificates", "logs", "temporary" })
                Directory.CreateDirectory(Path.Combine(root, area));
            await File.WriteAllTextAsync(Path.Combine(root, "uploads/events", "test.png"), "fixture", ct);
            await File.WriteAllTextAsync(Path.Combine(root, "keys", "test.xml"), "key fixture", ct);
            await File.WriteAllTextAsync(Path.Combine(root, "certificates", "test.pdf"), "certificate fixture", ct);
            var credentials = DemoCredentials.CredentialIds.Select((id, index) => (id, value: $"FixturePassword{index}"))
                .ToDictionary(item => item.id, item => item.value);
            await File.WriteAllTextAsync(credentialFile, JsonSerializer.Serialize(credentials), ct);
            await using (var master = new SqlConnection(DemoSetup.MasterConnection(instance)))
            {
                await master.OpenAsync(ct);
                await using var create = new SqlCommand("""
                    DECLARE @sql nvarchar(max) = N'CREATE DATABASE [AttendTa_Demo] ON PRIMARY '
                      + N'(NAME=N''AttendTa_Demo'', FILENAME=N''' + REPLACE(@data, '''', '''''') + N''') '
                      + N'LOG ON (NAME=N''AttendTa_Demo_log'', FILENAME=N''' + REPLACE(@log, '''', '''''') + N''')';
                    EXEC(@sql);
                    """, master);
                create.Parameters.Add("@data", SqlDbType.NVarChar, 260).Value = Path.Combine(root, "database", "AttendTa_Demo.mdf");
                create.Parameters.Add("@log", SqlDbType.NVarChar, 260).Value = Path.Combine(root, "database", "AttendTa_Demo_log.ldf");
                await create.ExecuteNonQueryAsync(ct);
            }
            var db = new SqlConnectionStringBuilder(DemoSetup.MasterConnection(instance)) { InitialCatalog = DemoTarget.Database };
            await using (var connection = new SqlConnection(db.ConnectionString))
            {
                await connection.OpenAsync(ct);
                await using var fixture = new SqlCommand("""
                    CREATE TABLE dbo.BackupRestoreFixture (Value nvarchar(100) NOT NULL);
                    INSERT dbo.BackupRestoreFixture VALUES (N'preserved test data');
                    CREATE TABLE dbo.venues (image_path nvarchar(500));
                    CREATE TABLE dbo.events (image_path nvarchar(500));
                    CREATE TABLE dbo.certificates (file_path nvarchar(500));
                    INSERT dbo.events VALUES (N'uploads/events/test.png');
                    INSERT dbo.certificates VALUES (N'certificates/test.pdf');
                    """, connection);
                await fixture.ExecuteNonQueryAsync(ct);
                await DemoOwnership.MarkNewAsync(connection, root, TestContext.Current.CancellationToken);
            }
            await DemoProtectedBackup.RunAsync(root, AppContext.BaseDirectory, instance, credentialFile, backup,
                TestContext.Current.CancellationToken);
            Assert.True(File.Exists(Path.Combine(backup, "COMPLETE")));
            Assert.False(File.Exists(Path.Combine(backup, "INCOMPLETE")));
            Assert.Equal("fixture", await File.ReadAllTextAsync(Path.Combine(backup, "store/uploads/events/test.png"), ct));
            Assert.True(File.Exists(Path.Combine(root, "uploads/events/test.png")));
            Assert.True(File.Exists(credentialFile));
            var manifest = DemoBackupManifest.Parse(await File.ReadAllBytesAsync(Path.Combine(backup, "manifest.json"), ct));
            Assert.Equal(manifest.Count, (await DemoBackupVerification.VerifyFilesAsync(backup, ct)).Count);
            Assert.Contains(manifest, entry => entry.Path == "database.bak");
            Assert.Contains(manifest, entry => entry.Path == "store/uploads/events/test.png");
            Assert.DoesNotContain(manifest, entry => entry.Path.Contains("credentials", StringComparison.OrdinalIgnoreCase));
            foreach (var entry in manifest)
            {
                using var source = DemoBackupSourceFile.Open(Path.Combine(backup, entry.Path.Replace('/', Path.DirectorySeparatorChar)));
                Assert.Equal(entry, await DemoBackupManifest.MeasureAsync(entry.Path, source, ct));
            }
            var verification = await DemoRestoreVerification.RunAsync(backup, ct);
            Assert.False(Directory.Exists(verification.TemporaryRoot));
            Assert.DoesNotContain(verification.Instance, await DemoRestoreVerification.InstanceNamesAsync(ct));
            var collisionRoot = Path.Combine(parent, "should-not-exist");
            await Assert.ThrowsAnyAsync<Exception>(() => DemoBackupRecovery.RunAsync(
                backup, collisionRoot, AppContext.BaseDirectory, instance, ct));
            Assert.False(Directory.Exists(collisionRoot));
            await Assert.ThrowsAsync<InvalidOperationException>(() => DemoBackupRecovery.RunAsync(
                backup, root, AppContext.BaseDirectory, instance, ct));
            Assert.Equal(0, await LocalDb("create", recoveryInstance));
            recoveryCreated = true;
            Assert.Equal(0, await LocalDb("start", recoveryInstance));
            var recoveryRoot = Path.Combine(parent, "recovery");
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DemoBackupRecovery.RunAsync(
                    backup, recoveryRoot, AppContext.BaseDirectory, recoveryInstance, cancelled.Token));
            }
            Assert.False(Directory.Exists(recoveryRoot));
            await DemoBackupRecovery.RunAsync(backup, recoveryRoot, AppContext.BaseDirectory, recoveryInstance, ct);
            Assert.False(File.Exists(Path.Combine(recoveryRoot, ".attendta-recovery-incomplete")));
            Assert.Equal("fixture", await File.ReadAllTextAsync(Path.Combine(recoveryRoot, "uploads/events/test.png"), ct));
            Assert.Equal("key fixture", await File.ReadAllTextAsync(Path.Combine(recoveryRoot, "keys/test.xml"), ct));
            Assert.Equal("certificate fixture", await File.ReadAllTextAsync(Path.Combine(recoveryRoot, "certificates/test.pdf"), ct));
            Assert.True(File.Exists(Path.Combine(backup, "database.bak")));
            await DemoOwnership.InspectAsync(recoveryRoot, AppContext.BaseDirectory, recoveryInstance, ct);
            var recoveredDb = new SqlConnectionStringBuilder(DemoSetup.MasterConnection(recoveryInstance))
                { InitialCatalog = DemoTarget.Database };
            await using (var recovered = new SqlConnection(recoveredDb.ConnectionString))
            {
                await recovered.OpenAsync(ct);
                await using var check = new SqlCommand("SELECT Value FROM dbo.BackupRestoreFixture", recovered);
                Assert.Equal("preserved test data", await check.ExecuteScalarAsync(ct));
            }
            var uninstallPreview = await DemoRemovalPreview.RunAsync(recoveryRoot, AppContext.BaseDirectory,
                recoveryInstance, credentialFile, ct);
            var uninstallBackup = Path.Combine(parent, "uninstall-backup");
            var uninstallJournal = Path.Combine(parent, "uninstall-journal");
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DemoGuardedUninstall.RunAsync(false,
                recoveryRoot, AppContext.BaseDirectory, recoveryInstance, credentialFile, uninstallBackup,
                uninstallJournal, uninstallPreview, ct));
            Assert.False(Directory.Exists(uninstallBackup));
            Assert.False(Directory.Exists(uninstallJournal));
            await Assert.ThrowsAsync<InvalidDataException>(() => DemoGuardedUninstall.RunAsync(true, recoveryRoot,
                AppContext.BaseDirectory, recoveryInstance, credentialFile, Path.Combine(parent, "mismatch-backup"),
                Path.Combine(parent, "mismatch-journal"), uninstallPreview.Skip(1).ToArray(), ct));
            Assert.True(Directory.Exists(recoveryRoot));
            await using (var activeRecovery = new SqlConnection(recoveredDb.ConnectionString))
            {
                await activeRecovery.OpenAsync(ct);
                var activeBackup = Path.Combine(parent, "uninstall-active-backup");
                await Assert.ThrowsAsync<InvalidOperationException>(() => DemoGuardedUninstall.RunAsync(true, recoveryRoot,
                    AppContext.BaseDirectory, recoveryInstance, credentialFile, activeBackup,
                    Path.Combine(parent, "uninstall-active-journal"), uninstallPreview, ct));
                Assert.False(Directory.Exists(activeBackup));
            }
            using (var locked = new FileStream(Path.Combine(recoveryRoot, "uploads/events/test.png"),
                FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                await Assert.ThrowsAnyAsync<IOException>(() => DemoGuardedUninstall.RunAsync(true, recoveryRoot,
                    AppContext.BaseDirectory, recoveryInstance, credentialFile, Path.Combine(parent, "uninstall-locked-backup"),
                    Path.Combine(parent, "uninstall-locked-journal"), uninstallPreview, ct));
            }
            Assert.True(Directory.Exists(recoveryRoot));
            var streamSource = Path.Combine(recoveryRoot, "uploads/events/test.png");
            await File.WriteAllTextAsync(streamSource + ":fixture", "source stream", ct);
            await Assert.ThrowsAsync<InvalidDataException>(() => DemoGuardedUninstall.RunAsync(true, recoveryRoot,
                AppContext.BaseDirectory, recoveryInstance, credentialFile, Path.Combine(parent, "source-stream-backup"),
                Path.Combine(parent, "source-stream-journal"), uninstallPreview, ct));
            Assert.Equal("source stream", await File.ReadAllTextAsync(streamSource + ":fixture", ct));
            Assert.Equal("fixture", await File.ReadAllTextAsync(streamSource, ct));
            File.Delete(streamSource + ":fixture");
            foreach (var directory in new[] { recoveryRoot, Path.Combine(recoveryRoot, "uploads", "events") })
            {
                foreach (var injectAfterBackup in new[] { false, true })
                {
                    var directoryStream = directory + ":fixture";
                    if (!injectAfterBackup) await File.WriteAllTextAsync(directoryStream, "directory stream", ct);
                    var id = Guid.NewGuid().ToString("N");
                    var error = await Assert.ThrowsAsync<InvalidDataException>(() => DemoGuardedUninstall.RunAsync(true,
                        recoveryRoot, AppContext.BaseDirectory, recoveryInstance, credentialFile,
                        Path.Combine(parent, "directory-stream-backup-" + id),
                        Path.Combine(parent, "directory-stream-journal-" + id), uninstallPreview, ct,
                        beforeRemoval: injectAfterBackup
                            ? () => File.WriteAllTextAsync(directoryStream, "directory stream", ct)
                            : null));
                    Assert.Contains("alternate data stream", error.Message);
                    Assert.Equal("directory stream", await File.ReadAllTextAsync(directoryStream, ct));
                    Assert.Equal("fixture", await File.ReadAllTextAsync(streamSource, ct));
                    await using (var intact = new SqlConnection(recoveredDb.ConnectionString))
                    {
                        await intact.OpenAsync(ct);
                        await using var check = new SqlCommand("SELECT Value FROM dbo.BackupRestoreFixture", intact);
                        Assert.Equal("preserved test data", await check.ExecuteScalarAsync(ct));
                    }
                    File.Delete(directoryStream);
                }
            }
            var streamBackup = Path.Combine(parent, "stream-backup");
            await Assert.ThrowsAsync<InvalidDataException>(() => DemoGuardedUninstall.RunAsync(true, recoveryRoot,
                AppContext.BaseDirectory, recoveryInstance, credentialFile, streamBackup,
                Path.Combine(parent, "stream-backup-journal"), uninstallPreview, ct,
                beforeRemoval: () => File.WriteAllTextAsync(Path.Combine(streamBackup, "database.bak") + ":fixture", "backup stream", ct)));
            Assert.True(File.Exists(streamSource));
            Assert.Equal("backup stream", await File.ReadAllTextAsync(Path.Combine(streamBackup, "database.bak") + ":fixture", ct));
            var tamperedUninstallBackup = Path.Combine(parent, "uninstall-tampered-backup");
            await Assert.ThrowsAsync<InvalidDataException>(() => DemoGuardedUninstall.RunAsync(true, recoveryRoot,
                AppContext.BaseDirectory, recoveryInstance, credentialFile, tamperedUninstallBackup,
                Path.Combine(parent, "uninstall-tampered-journal"), uninstallPreview, ct,
                beforeRemoval: () => File.WriteAllTextAsync(
                    Path.Combine(tamperedUninstallBackup, "database.bak"), "tampered", ct)));
            Assert.True(Directory.Exists(recoveryRoot));
            var unverifiedLog = Path.Combine(recoveryRoot, "logs", "unknown.txt");
            await File.WriteAllTextAsync(unverifiedLog, "preserve", ct);
            var logPreview = await DemoRemovalPreview.RunAsync(recoveryRoot, AppContext.BaseDirectory,
                recoveryInstance, credentialFile, ct);
            await Assert.ThrowsAsync<InvalidDataException>(() => DemoGuardedUninstall.RunAsync(true, recoveryRoot,
                AppContext.BaseDirectory, recoveryInstance, credentialFile, Path.Combine(parent, "uninstall-unknown-backup"),
                Path.Combine(parent, "uninstall-unknown-journal"), logPreview, ct));
            Assert.Equal("preserve", await File.ReadAllTextAsync(unverifiedLog, ct));
            File.Delete(unverifiedLog);
            await DemoGuardedUninstall.RunAsync(true, recoveryRoot, AppContext.BaseDirectory, recoveryInstance,
                credentialFile, uninstallBackup, uninstallJournal, uninstallPreview, ct);
            Assert.False(Directory.Exists(recoveryRoot));
            Assert.True(File.Exists(credentialFile));
            Assert.True(File.Exists(Path.Combine(uninstallBackup, "COMPLETE")));
            var uninstallLog = await File.ReadAllTextAsync(Path.Combine(uninstallJournal, "uninstall.jsonl"), ct);
            Assert.Contains("DatabaseDropped", uninstallLog);
            Assert.Contains("Completed", uninstallLog);
            await using (var removedMaster = new SqlConnection(DemoSetup.MasterConnection(recoveryInstance)))
            {
                await removedMaster.OpenAsync(ct);
                await using var check = new SqlCommand("SELECT DB_ID(N'AttendTa_Demo')", removedMaster);
                Assert.Equal(DBNull.Value, await check.ExecuteScalarAsync(ct));
            }
            var preview = await DemoUninstallPreflight.RunAsync(root, AppContext.BaseDirectory, instance,
                credentialFile, backup, ct);
            Assert.Contains(Path.Combine(root, "uploads", "events", "test.png"), preview);
            var lockedBackup = Path.Combine(parent, "continuous-lock-backup");
            var observedCompetingLockRefusal = false;
            await DemoLockedBackupVerification.RunAsync(root, AppContext.BaseDirectory, instance, credentialFile,
                lockedBackup, ct, async _ =>
                {
                    await using var competitor = new SqlConnection(DemoSetup.MasterConnection(instance));
                    await competitor.OpenAsync(ct);
                    await using var attempt = new SqlCommand("""
                        DECLARE @result int;
                        EXEC @result=sys.sp_getapplock @Resource=N'AttendTaDemoFreshSetup',
                            @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=0;
                        SELECT @result;
                        """, competitor);
                    observedCompetingLockRefusal = Convert.ToInt32(await attempt.ExecuteScalarAsync(ct)) < 0;
                });
            Assert.True(observedCompetingLockRefusal);
            Assert.True(File.Exists(Path.Combine(lockedBackup, "COMPLETE")));
            var changedBackup = Path.Combine(parent, "changed-source-backup");
            var sourceImage = Path.Combine(root, "uploads", "events", "test.png");
            try
            {
                await Assert.ThrowsAsync<InvalidDataException>(() => DemoLockedBackupVerification.RunAsync(
                    root, AppContext.BaseDirectory, instance, credentialFile, changedBackup, ct,
                    async _ => await File.WriteAllTextAsync(sourceImage, "changed during verification", ct)));
                Assert.True(File.Exists(sourceImage));
            }
            finally { await File.WriteAllTextAsync(sourceImage, "fixture", CancellationToken.None); }
            await using (var active = new SqlConnection(db.ConnectionString))
            {
                await active.OpenAsync(ct);
                await Assert.ThrowsAsync<InvalidOperationException>(() => DemoUninstallPreflight.RunAsync(
                    root, AppContext.BaseDirectory, instance, credentialFile, backup, ct));
                var refusedBackup = Path.Combine(parent, "active-session-backup");
                await Assert.ThrowsAsync<InvalidOperationException>(() => DemoLockedBackupVerification.RunAsync(
                    root, AppContext.BaseDirectory, instance, credentialFile, refusedBackup, ct));
                Assert.False(Directory.Exists(refusedBackup));
            }
            Assert.True(File.Exists(Path.Combine(root, "uploads/events/test.png")));

            // Restore into a second instance created only for this test. Neither the
            // original demo database nor any pre-existing LocalDB is a restore target.
            Assert.Equal(0, await LocalDb("create", restoreInstance));
            restoreCreated = true;
            Assert.Equal(0, await LocalDb("start", restoreInstance));
            var restoreDirectory = Path.Combine(parent, "restore");
            Directory.CreateDirectory(restoreDirectory);
            await using (var restoreMaster = new SqlConnection(DemoSetup.MasterConnection(restoreInstance)))
            {
                await restoreMaster.OpenAsync(ct);
                await using var restore = new SqlCommand("""
                    RESTORE DATABASE [AttendTa_RestoreTest] FROM DISK=@backup
                    WITH CHECKSUM, MOVE N'AttendTa_Demo' TO @data,
                    MOVE N'AttendTa_Demo_log' TO @log;
                    """, restoreMaster) { CommandTimeout = 120 };
                restore.Parameters.Add("@backup", SqlDbType.NVarChar, 4000).Value = Path.Combine(backup, "database.bak");
                restore.Parameters.Add("@data", SqlDbType.NVarChar, 4000).Value = Path.Combine(restoreDirectory, "restored.mdf");
                restore.Parameters.Add("@log", SqlDbType.NVarChar, 4000).Value = Path.Combine(restoreDirectory, "restored.ldf");
                await restore.ExecuteNonQueryAsync(ct);
            }
            var restoredConnection = new SqlConnectionStringBuilder(DemoSetup.MasterConnection(restoreInstance))
                { InitialCatalog = "AttendTa_RestoreTest" };
            await using (var restored = new SqlConnection(restoredConnection.ConnectionString))
            {
                await restored.OpenAsync(ct);
                await using var check = new SqlCommand("SELECT Value FROM dbo.BackupRestoreFixture", restored);
                Assert.Equal("preserved test data", await check.ExecuteScalarAsync(ct));
                await using var marker = new SqlCommand("""
                    SELECT CONVERT(nvarchar(32), value) FROM sys.extended_properties
                    WHERE class=0 AND name=N'AttendTaDemoOwnershipV1';
                    """, restored);
                Assert.Equal(await File.ReadAllTextAsync(Path.Combine(backup, "store", DemoOwnership.MarkerFile), ct),
                    await marker.ExecuteScalarAsync(ct));
            }
            await Assert.ThrowsAsync<InvalidOperationException>(() => DemoProtectedBackup.RunAsync(
                root, AppContext.BaseDirectory, instance, credentialFile, backup, TestContext.Current.CancellationToken));
            var failedBackup = Path.Combine(parent, "locked-backup");
            using (var locked = new FileStream(Path.Combine(root, "uploads/events/test.png"),
                FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                await Assert.ThrowsAnyAsync<Exception>(() => DemoProtectedBackup.RunAsync(
                    root, AppContext.BaseDirectory, instance, credentialFile, failedBackup, ct));
            }
            Assert.False(Directory.Exists(failedBackup));
            Assert.False(File.Exists(Path.Combine(failedBackup, "COMPLETE")));
            Assert.Equal("fixture", await File.ReadAllTextAsync(Path.Combine(root, "uploads/events/test.png"), ct));
            var extra = Path.Combine(backup, "unexpected.txt");
            await File.WriteAllTextAsync(extra, "fixture", ct);
            await Assert.ThrowsAsync<InvalidDataException>(() => DemoBackupVerification.VerifyFilesAsync(backup, ct));
            File.Delete(extra);
            var copiedImage = Path.Combine(backup, "store/uploads/events/test.png");
            await File.WriteAllTextAsync(copiedImage, "tampered", ct);
            await Assert.ThrowsAsync<InvalidDataException>(() => DemoBackupVerification.VerifyFilesAsync(backup, ct));
            await Assert.ThrowsAsync<InvalidDataException>(() => DemoRestoreVerification.RunAsync(backup, ct));
            await Assert.ThrowsAsync<InvalidDataException>(() => DemoUninstallPreflight.RunAsync(
                root, AppContext.BaseDirectory, instance, credentialFile, backup, ct));
            var rejectedRecovery = Path.Combine(parent, "tampered-recovery");
            await Assert.ThrowsAsync<InvalidDataException>(() => DemoBackupRecovery.RunAsync(
                backup, rejectedRecovery, AppContext.BaseDirectory, recoveryInstance, ct));
            Assert.False(Directory.Exists(rejectedRecovery));
            var partialPreview = await DemoRemovalPreview.RunAsync(root, AppContext.BaseDirectory, instance, credentialFile, ct);
            var partialBackup = Path.Combine(parent, "partial-uninstall-backup");
            var partialJournal = Path.Combine(parent, "partial-uninstall-journal");
            var afterDropSource = Path.Combine(root, "uploads/events/test.png");
            var afterDropError = await Assert.ThrowsAsync<InvalidDataException>(() => DemoGuardedUninstall.RunAsync(true, root,
                AppContext.BaseDirectory, instance, credentialFile, partialBackup, partialJournal,
                partialPreview, ct, () => File.WriteAllTextAsync(afterDropSource + ":fixture", "after drop", ct)));
            Assert.Contains("alternate data stream", afterDropError.Message);
            Assert.True(Directory.Exists(root));
            Assert.Equal("fixture", await File.ReadAllTextAsync(afterDropSource, ct));
            Assert.Equal("after drop", await File.ReadAllTextAsync(afterDropSource + ":fixture", ct));
            var partialLog = await File.ReadAllTextAsync(Path.Combine(partialJournal, "uninstall.jsonl"), ct);
            Assert.Contains("DatabaseDropStarting", partialLog);
            Assert.DoesNotContain("Completed", partialLog);
            Assert.True(File.Exists(Path.Combine(partialBackup, "COMPLETE")));
            Assert.Equal(0, await LocalDb("create", partialRecoveryInstance));
            partialRecoveryCreated = true;
            Assert.Equal(0, await LocalDb("start", partialRecoveryInstance));
            var partialRecoveryRoot = Path.Combine(parent, "partial-recovery");
            await DemoBackupRecovery.RunAsync(partialBackup, partialRecoveryRoot, AppContext.BaseDirectory, partialRecoveryInstance, ct);
            Assert.Equal("fixture", await File.ReadAllTextAsync(Path.Combine(partialRecoveryRoot, "uploads/events/test.png"), ct));
            await DemoOwnership.InspectAsync(partialRecoveryRoot, AppContext.BaseDirectory, partialRecoveryInstance, ct);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            if (partialRecoveryCreated)
            {
                Assert.Equal(0, await LocalDb("stop", partialRecoveryInstance, "-k"));
                Assert.Equal(0, await LocalDb("delete", partialRecoveryInstance));
            }
            if (recoveryCreated)
            {
                Assert.Equal(0, await LocalDb("stop", recoveryInstance, "-k"));
                Assert.Equal(0, await LocalDb("delete", recoveryInstance));
            }
            if (restoreCreated)
            {
                Assert.Equal(0, await LocalDb("stop", restoreInstance, "-k"));
                Assert.Equal(0, await LocalDb("delete", restoreInstance));
            }
            if (created)
            {
                Assert.Equal(0, await LocalDb("stop", instance, "-k"));
                Assert.Equal(0, await LocalDb("delete", instance));
            }
            if (Directory.Exists(parent)) Directory.Delete(parent, true);
        }
    }

    private static async Task<int> LocalDb(params string[] args)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo("sqllocaldb")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true } };
        foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await Task.WhenAll(stdout, stderr);
        return process.ExitCode;
    }
}
