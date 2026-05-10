using System;
using System.IO;
using System.Reflection;
using System.Threading;
using AroAro.DataCore.LiteDb;
using Xunit;

namespace DataCore.Tests.LiteDb
{
    /// <summary>
    /// Tests for the private static DeleteFileWithRetry helper method (#83).
    /// Uses reflection since the method is private.
    /// </summary>
    public class DeleteFileWithRetryTests
    {
        private static readonly MethodInfo _deleteFileWithRetryMethod =
            typeof(LiteDbDataStore).GetMethod("DeleteFileWithRetry",
                BindingFlags.NonPublic | BindingFlags.Static);

        private static void InvokeDeleteFileWithRetry(string path, int maxWaitMs = 1000)
        {
            Assert.NotNull(_deleteFileWithRetryMethod);
            _deleteFileWithRetryMethod.Invoke(null, new object[] { path, maxWaitMs });
        }

        [Fact]
        public void DeleteFileWithRetry_FileExists_DeletesSuccessfully()
        {
            var tmpFile = Path.GetTempFileName();
            try
            {
                Assert.True(File.Exists(tmpFile), "Temp file should exist before deletion");

                InvokeDeleteFileWithRetry(tmpFile);

                Assert.False(File.Exists(tmpFile), "File should be deleted after DeleteFileWithRetry");
            }
            finally
            {
                if (File.Exists(tmpFile)) File.Delete(tmpFile);
            }
        }

        [Fact]
        public void DeleteFileWithRetry_FileDoesNotExist_NoOp()
        {
            var nonExistent = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}.tmp");
            Assert.False(File.Exists(nonExistent));

            // Should not throw
            var exception = Record.Exception(() => InvokeDeleteFileWithRetry(nonExistent));
            Assert.Null(exception);
        }

        [Fact]
        public void DeleteFileWithRetry_FileLocked_RetriesAndSucceeds()
        {
            var tmpFile = Path.GetTempFileName();
            try
            {
                // Lock the file from another thread and release after a short delay
                var locked = new ManualResetEventSlim(false);
                var release = new ManualResetEventSlim(false);

                var lockThread = new Thread(() =>
                {
                    using var fs = new FileStream(tmpFile, FileMode.Open, FileAccess.ReadWrite,
                        FileShare.None); // Exclusive lock
                    locked.Set();
                    release.Wait(TimeSpan.FromSeconds(5)); // Hold lock until released
                });
                lockThread.Start();
                locked.Wait(TimeSpan.FromSeconds(2));

                // Release the lock after 200ms
                var releaseThread = new Thread(() =>
                {
                    Thread.Sleep(200);
                    release.Set();
                });
                releaseThread.Start();

                // This should retry and eventually succeed
                InvokeDeleteFileWithRetry(tmpFile, maxWaitMs: 3000);

                Assert.False(File.Exists(tmpFile), "File should be deleted after lock is released");

                lockThread.Join(TimeSpan.FromSeconds(5));
                releaseThread.Join(TimeSpan.FromSeconds(5));
            }
            finally
            {
                if (File.Exists(tmpFile)) File.Delete(tmpFile);
            }
        }
    }
}
