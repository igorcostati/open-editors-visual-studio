using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OpenEditors.VisualStudio.Tests
{
    internal static class VsixTestAssemblyLoader
    {
        private static Assembly _cachedAssembly;
        private static bool _resolverRegistered;

        public static Assembly Load()
        {
            EnsureAssemblyResolverRegistered();

            if (_cachedAssembly != null)
            {
                return _cachedAssembly;
            }

            var solutionRoot = FindSolutionRoot();
            var debugDll = Path.Combine(solutionRoot, "src", "OpenEditors.VisualStudio.Vsix", "bin", "Debug", "OpenEditors.VisualStudio.Vsix.dll");
            var releaseDll = Path.Combine(solutionRoot, "src", "OpenEditors.VisualStudio.Vsix", "bin", "Release", "OpenEditors.VisualStudio.Vsix.dll");

            var assemblyPath = File.Exists(debugDll)
                ? debugDll
                : releaseDll;

            if (!File.Exists(assemblyPath))
            {
                Assert.Inconclusive("OpenEditors.VisualStudio.Vsix.dll não foi encontrado. Compile a solução antes de executar os testes.");
            }

            _cachedAssembly = Assembly.LoadFrom(assemblyPath);
            return _cachedAssembly;
        }

        private static void EnsureAssemblyResolverRegistered()
        {
            if (_resolverRegistered)
            {
                return;
            }

            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            _resolverRegistered = true;
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var requestedAssemblyName = new AssemblyName(args.Name);
            var alreadyLoaded = AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, requestedAssemblyName.Name, StringComparison.OrdinalIgnoreCase));
            if (alreadyLoaded != null)
            {
                return alreadyLoaded;
            }

            var fileName = requestedAssemblyName.Name + ".dll";
            foreach (var directory in GetAssemblySearchDirectories())
            {
                var candidate = Path.Combine(directory, fileName);
                if (!File.Exists(candidate))
                {
                    continue;
                }

                try
                {
                    return Assembly.LoadFrom(candidate);
                }
                catch
                {
                }
            }

            return null;
        }

        private static IEnumerable<string> GetAssemblySearchDirectories()
        {
            var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrWhiteSpace(baseDirectory) && Directory.Exists(baseDirectory))
            {
                directories.Add(baseDirectory);
            }

            var devEnvDir = Environment.GetEnvironmentVariable("DevEnvDir");
            AddDirectoryCandidates(directories, devEnvDir);

            var vsInstallDir = Environment.GetEnvironmentVariable("VSINSTALLDIR");
            AddDirectoryCandidates(directories, vsInstallDir);

            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrWhiteSpace(programFilesX86))
            {
                var editions = new[] { "Community", "Professional", "Enterprise", "Preview" };
                var versions = new[] { "2026", "2022", "2019" };
                foreach (var version in versions)
                {
                    foreach (var edition in editions)
                    {
                        var ideDir = Path.Combine(programFilesX86, "Microsoft Visual Studio", version, edition, "Common7", "IDE");
                        AddDirectoryCandidates(directories, ideDir);
                    }
                }
            }

            return directories;
        }

        private static void AddDirectoryCandidates(ISet<string> directories, string root)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            if (Directory.Exists(root))
            {
                directories.Add(root);
            }

            var publicAssemblies = Path.Combine(root, "PublicAssemblies");
            if (Directory.Exists(publicAssemblies))
            {
                directories.Add(publicAssemblies);
            }

            var privateAssemblies = Path.Combine(root, "PrivateAssemblies");
            if (Directory.Exists(privateAssemblies))
            {
                directories.Add(privateAssemblies);
            }
        }

        private static string FindSolutionRoot()
        {
            var current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                var marker = Path.Combine(current.FullName, "src", "OpenEditors.VisualStudio.Vsix", "OpenEditors.VisualStudio.Vsix.csproj");
                if (File.Exists(marker))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            Assert.Inconclusive("Não foi possível localizar a raiz da solução para carregar a extensão em teste.");
            return null;
        }
    }
}
