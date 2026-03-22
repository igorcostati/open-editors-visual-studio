using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OpenEditors.VisualStudio.Tests
{
    [TestClass]
    public class GitStatusServiceTests
    {
        private const string GitStatusServiceTypeName = "OpenEditors.VisualStudio.Vsix.Services.GitStatusService";

        [TestMethod]
        public void GetCachedStatus_ReturnsClean_ForNullOrUnknownPath()
        {
            var service = CreateGitStatusService(out var serviceType);

            var getCachedStatus = serviceType.GetMethod("GetCachedStatus", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(getCachedStatus);

            var nullStatus = getCachedStatus.Invoke(service, new object[] { null });
            var unknownStatus = getCachedStatus.Invoke(service, new object[] { @"C:\\unknown\\file.cs" });

            Assert.AreEqual("Clean", nullStatus?.ToString());
            Assert.AreEqual("Clean", unknownStatus?.ToString());
        }

        [TestMethod]
        public void GetCachedStatus_ReturnsClean_ForWhitespacePath()
        {
            var service = CreateGitStatusService(out var serviceType);

            var getCachedStatus = serviceType.GetMethod("GetCachedStatus", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(getCachedStatus);

            var whitespaceStatus = getCachedStatus.Invoke(service, new object[] { "   " });

            Assert.AreEqual("Clean", whitespaceStatus?.ToString());
        }

        [DataTestMethod]
        [DataRow("D", "Deleted")]
        [DataRow("A", "Added")]
        [DataRow("??", "Added")]
        [DataRow("M", "Modified")]
        [DataRow("R", "Modified")]
        [DataRow("C", "Modified")]
        [DataRow("U", "Modified")]
        [DataRow("", "Clean")]
        [DataRow(" ", "Clean")]
        [DataRow("X", "Clean")]
        public void ParseStatus_MapsGitCodesToExpectedStatus(string code, string expected)
        {
            var assembly = VsixTestAssemblyLoader.Load();
            var serviceType = assembly.GetType(GitStatusServiceTypeName, throwOnError: true);

            var parseStatus = serviceType.GetMethod("ParseStatus", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(parseStatus);

            var result = parseStatus.Invoke(null, new object[] { code });

            Assert.AreEqual(expected, result?.ToString());
        }

        [TestMethod]
        public void ParseStatus_WhenContainsDeletedAndAdded_PrioritizesDeleted()
        {
            var assembly = VsixTestAssemblyLoader.Load();
            var serviceType = assembly.GetType(GitStatusServiceTypeName, throwOnError: true);

            var parseStatus = serviceType.GetMethod("ParseStatus", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(parseStatus);

            var result = parseStatus.Invoke(null, new object[] { "AD" });

            Assert.AreEqual("Deleted", result?.ToString());
        }

        [TestMethod]
        public async Task RefreshAsync_WithEmptyInput_CompletesWithoutChangingStatus()
        {
            var service = CreateGitStatusService(out var serviceType);

            var refreshAsync = serviceType.GetMethod("RefreshAsync", BindingFlags.Instance | BindingFlags.Public);
            var getCachedStatus = serviceType.GetMethod("GetCachedStatus", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(refreshAsync);
            Assert.IsNotNull(getCachedStatus);

            var refreshTask = (Task)refreshAsync.Invoke(service, new object[]
            {
                new List<string>(),
                CancellationToken.None,
            });

            await refreshTask.ConfigureAwait(false);

            var status = getCachedStatus.Invoke(service, new object[] { @"C:\\unknown\\file.cs" });
            Assert.AreEqual("Clean", status?.ToString());
        }

        [TestMethod]
        public void FindRepositoryRoot_ReturnsClosestRepositoryDirectory()
        {
            var service = CreateGitStatusService(out var serviceType);

            var findRepositoryRoot = serviceType.GetMethod("FindRepositoryRoot", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(findRepositoryRoot);

            var tempRoot = Path.Combine(Path.GetTempPath(), "OpenEditors.Tests", Guid.NewGuid().ToString("N"));
            var repoRoot = Path.Combine(tempRoot, "repo");
            var nested = Path.Combine(repoRoot, "src", "folder");

            Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
            Directory.CreateDirectory(nested);
            var filePath = Path.Combine(nested, "file.cs");
            File.WriteAllText(filePath, "class C { }");

            try
            {
                var result = findRepositoryRoot.Invoke(service, new object[] { filePath });
                Assert.AreEqual(Path.GetFullPath(repoRoot), result as string, true);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
        }

        [TestMethod]
        public void FindRepositoryRoot_ReturnsRoot_WhenPathIsDirectoryInsideRepository()
        {
            var service = CreateGitStatusService(out var serviceType);

            var findRepositoryRoot = serviceType.GetMethod("FindRepositoryRoot", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(findRepositoryRoot);

            var tempRoot = Path.Combine(Path.GetTempPath(), "OpenEditors.Tests", Guid.NewGuid().ToString("N"));
            var repoRoot = Path.Combine(tempRoot, "repo");
            var nested = Path.Combine(repoRoot, "src", "folder");

            Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
            Directory.CreateDirectory(nested);

            try
            {
                var result = findRepositoryRoot.Invoke(service, new object[] { nested });
                Assert.AreEqual(Path.GetFullPath(repoRoot), result as string, true);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
        }

        [TestMethod]
        public void FindRepositoryRoot_ReturnsNull_WhenFileIsNotInsideRepository()
        {
            var service = CreateGitStatusService(out var serviceType);

            var findRepositoryRoot = serviceType.GetMethod("FindRepositoryRoot", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(findRepositoryRoot);

            var tempRoot = Path.Combine(Path.GetTempPath(), "OpenEditors.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            var filePath = Path.Combine(tempRoot, "file.cs");
            File.WriteAllText(filePath, "class C { }");

            try
            {
                var result = findRepositoryRoot.Invoke(service, new object[] { filePath });
                Assert.IsNull(result);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
        }

        private static object CreateGitStatusService(out Type serviceType)
        {
            var assembly = VsixTestAssemblyLoader.Load();
            serviceType = assembly.GetType(GitStatusServiceTypeName, throwOnError: true);
            return Activator.CreateInstance(serviceType);
        }
    }
}
