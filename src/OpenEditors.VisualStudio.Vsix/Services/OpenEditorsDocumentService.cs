using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using OpenEditors.VisualStudio.Vsix.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OpenEditors.VisualStudio.Vsix.Services
{
    /// <summary>
    /// Tracks open documents by subscribing to Visual Studio document/window events and
    /// running document table notifications. This is fully event-driven and does not poll.
    /// </summary>
    public sealed class OpenEditorsDocumentService : IVsRunningDocTableEvents3, IDisposable
    {
        private readonly AsyncPackage _package;
        private DTE2 _dte;
        private IVsRunningDocumentTable _runningDocumentTable;
        private DocumentEvents _documentEvents;
        private WindowEvents _windowEvents;
        private SolutionEvents _solutionEvents;
        private uint _rdtEventsCookie;
        private readonly IGitStatusService _gitStatusService;
        private readonly IDocumentErrorService _documentErrorService;
        private readonly IFileIconService _fileIconService;
        private readonly IThemeService _themeService;
        private CancellationTokenSource _statusRefreshCancellationTokenSource;

        public OpenEditorsDocumentService(
            AsyncPackage package,
            IGitStatusService gitStatusService,
            IDocumentErrorService documentErrorService,
            IFileIconService fileIconService,
            IThemeService themeService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            _gitStatusService = gitStatusService ?? throw new ArgumentNullException(nameof(gitStatusService));
            _documentErrorService = documentErrorService ?? throw new ArgumentNullException(nameof(documentErrorService));
            _fileIconService = fileIconService ?? throw new ArgumentNullException(nameof(fileIconService));
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            Documents = new ObservableCollection<OpenDocumentItem>();

            _themeService.ThemeChanged += OnThemeChanged;
        }

        public ObservableCollection<OpenDocumentItem> Documents { get; }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            await EnsureShellServicesAsync(cancellationToken);
            RefreshFromDte();
        }

        public async Task RefreshDocumentsAsync(CancellationToken cancellationToken)
        {
            await EnsureShellServicesAsync(cancellationToken);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            RefreshFromDte();
        }

        private async Task EnsureShellServicesAsync(CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            if (_dte == null)
            {
                _dte = await _package.GetServiceAsync(typeof(DTE)) as DTE2;
            }

            if (_runningDocumentTable == null)
            {
                _runningDocumentTable = await _package.GetServiceAsync(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;
            }

            if (_documentEvents == null)
            {
                _documentEvents = _dte?.Events?.DocumentEvents;
                if (_documentEvents != null)
                {
                    _documentEvents.DocumentOpened += OnDocumentOpened;
                    _documentEvents.DocumentClosing += OnDocumentClosing;
                    _documentEvents.DocumentSaved += OnDocumentSaved;
                }
            }

            if (_windowEvents == null)
            {
                _windowEvents = _dte?.Events?.WindowEvents;
                if (_windowEvents != null)
                {
                    _windowEvents.WindowActivated += OnWindowActivated;
                }
            }

            if (_solutionEvents == null)
            {
                _solutionEvents = _dte?.Events?.SolutionEvents;
                if (_solutionEvents != null)
                {
                    _solutionEvents.Opened += OnSolutionOpened;
                    _solutionEvents.AfterClosing += OnSolutionAfterClosing;
                }
            }

            if (_runningDocumentTable != null && _rdtEventsCookie == 0)
            {
                _runningDocumentTable.AdviseRunningDocTableEvents(this, out _rdtEventsCookie);
            }
        }

        public async Task ActivateDocumentAsync(string fullPath)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var document = FindOpenDocument(fullPath);
            document?.Activate();
        }

        public async Task SaveDocumentAsync(string fullPath)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var document = FindOpenDocument(fullPath);
            document?.Save();
        }

        public async Task CloseDocumentAsync(string fullPath)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var document = FindOpenDocument(fullPath);
            document?.Close(vsSaveChanges.vsSaveChangesPrompt);
        }

        public async Task DuplicateDocumentAsync(string fullPath)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var document = FindOpenDocument(fullPath);
            if (document == null)
            {
                return;
            }

            if (!document.Saved)
            {
                document.Save();
            }

            var sourcePath = document.FullName;
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                return;
            }

            var directory = Path.GetDirectoryName(sourcePath);
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(sourcePath);
            var extension = Path.GetExtension(sourcePath);

            var duplicatePath = Path.Combine(directory, $"{nameWithoutExtension} - Copy{extension}");
            var suffix = 2;
            while (File.Exists(duplicatePath))
            {
                duplicatePath = Path.Combine(directory, $"{nameWithoutExtension} - Copy ({suffix}){extension}");
                suffix++;
            }

            await Task.Run(() => File.Copy(sourcePath, duplicatePath));

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            AddDuplicateToProject(document, duplicatePath);
            _dte?.ItemOperations?.OpenFile(duplicatePath);
        }

        private void AddDuplicateToProject(Document sourceDocument, string duplicatePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var sourceProjectItem = sourceDocument?.ProjectItem;
            var projectItems = sourceProjectItem?.Collection;
            if (projectItems == null)
            {
                return;
            }

            try
            {
                projectItems.AddFromFile(duplicatePath);
            }
            catch
            {
            }
        }

        public async Task CloseAllDocumentsAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _dte?.ExecuteCommand("Window.CloseAllDocuments");
        }

        private void OnDocumentOpened(Document document)
        {
            RefreshFromDte();
        }

        private void OnDocumentClosing(Document document)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                await Task.Yield();
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                RefreshFromDte();
            });
        }

        private void OnDocumentSaved(Document document)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (document == null || string.IsNullOrEmpty(document.FullName))
            {
                return;
            }

            var item = FindItemByPath(document.FullName);
            if (item != null)
            {
                item.IsDirty = false;
            }

            QueueStatusRefresh();
        }

        private void OnWindowActivated(Window gotFocus, Window lostFocus)
        {
            RefreshActiveDocument();
        }

        private void OnSolutionOpened()
        {
            RefreshFromDte();
        }

        private void OnSolutionAfterClosing()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _statusRefreshCancellationTokenSource?.Cancel();
            Documents.Clear();
        }

        private void RefreshFromDte()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var liveDocuments = new Dictionary<string, (string fileName, bool isDirty)>(StringComparer.OrdinalIgnoreCase);

            if (_dte?.Documents != null)
            {
                foreach (Document document in _dte.Documents)
                {
                    if (document == null || string.IsNullOrEmpty(document.FullName))
                    {
                        continue;
                    }

                    var isDirty = false;
                    try
                    {
                        isDirty = !document.Saved;
                    }
                    catch
                    {
                    }

                    liveDocuments[document.FullName] = (Path.GetFileName(document.FullName), isDirty);
                }
            }

            for (var i = Documents.Count - 1; i >= 0; i--)
            {
                if (!liveDocuments.ContainsKey(Documents[i].FullPath))
                {
                    Documents.RemoveAt(i);
                }
            }

            foreach (var kvp in liveDocuments)
            {
                var item = FindItemByPath(kvp.Key);
                if (item == null)
                {
                    Documents.Add(new OpenDocumentItem
                    {
                        FullPath = kvp.Key,
                        FileName = kvp.Value.fileName,
                        IsDirty = kvp.Value.isDirty,
                        Icon = KnownMonikers.Document
                    });
                }
                else
                {
                    item.FileName = kvp.Value.fileName;
                    item.IsDirty = kvp.Value.isDirty;
                    if (item.Icon.Id == 0 && item.Icon.Guid == Guid.Empty)
                    {
                        item.Icon = KnownMonikers.Document;
                    }
                }
            }

            RefreshActiveDocument();
            QueueStatusRefresh();
        }

        private void RefreshActiveDocument()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var activePath = _dte?.ActiveDocument?.FullName;
            foreach (var item in Documents)
            {
                item.IsActive = !string.IsNullOrEmpty(activePath) &&
                    string.Equals(item.FullPath, activePath, StringComparison.OrdinalIgnoreCase);
            }
        }

        private OpenDocumentItem FindItemByPath(string fullPath)
        {
            return Documents.FirstOrDefault(d => string.Equals(d.FullPath, fullPath, StringComparison.OrdinalIgnoreCase));
        }

        private void QueueStatusRefresh()
        {
            _statusRefreshCancellationTokenSource?.Cancel();
            _statusRefreshCancellationTokenSource?.Dispose();
            _statusRefreshCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _statusRefreshCancellationTokenSource.Token;

            ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                try
                {
                    await Task.Delay(150, cancellationToken);
                    await RefreshStatusIndicatorsAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                }
            });
        }

        /// <summary>
        /// Error tracking approach: Error List is queried through IDocumentErrorService and mapped to each open document.
        /// Git detection approach: git porcelain is queried in IGitStatusService and cached, then applied to open documents.
        /// </summary>
        private async Task RefreshStatusIndicatorsAsync(CancellationToken cancellationToken)
        {
            List<string> openPaths;
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            openPaths = Documents.Select(d => d.FullPath).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();

            await _gitStatusService.RefreshAsync(openPaths, cancellationToken);
            var filesWithErrors = await _documentErrorService.GetFilesWithErrorsAsync(cancellationToken);
            var fileIcons = new Dictionary<string, ImageMoniker>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in openPaths)
            {
                fileIcons[path] = await _fileIconService.GetIconAsync(path, cancellationToken);
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            foreach (var document in Documents)
            {
                document.HasErrors = filesWithErrors.Contains(document.FullPath);
                document.GitStatus = _gitStatusService.GetCachedStatus(document.FullPath);
                if (fileIcons.TryGetValue(document.FullPath, out var moniker))
                {
                    document.Icon = moniker;
                }
            }
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            QueueStatusRefresh();
        }

        private Document FindOpenDocument(string fullPath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_dte?.Documents == null)
            {
                return null;
            }

            foreach (Document document in _dte.Documents)
            {
                if (document == null || string.IsNullOrEmpty(document.FullName))
                {
                    continue;
                }

                if (string.Equals(document.FullName, fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    return document;
                }
            }

            return null;
        }

        public int OnAfterAttributeChange(uint docCookie, uint grfAttribs)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterAttributeChangeEx(
            uint docCookie,
            uint grfAttribs,
            IVsHierarchy pHierOld,
            uint itemidOld,
            string pszMkDocumentOld,
            IVsHierarchy pHierNew,
            uint itemidNew,
            string pszMkDocumentNew)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if ((grfAttribs & (uint)__VSRDTATTRIB.RDTA_DocDataIsDirty) != 0)
                {
                    UpdateDirtyStateFromRdt(docCookie);
                }
            });

            return VSConstants.S_OK;
        }

        public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                RefreshFromDte();
            });

            return VSConstants.S_OK;
        }

        public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterSave(uint docCookie)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                UpdateDirtyStateFromRdt(docCookie);
            });

            return VSConstants.S_OK;
        }

        public int OnBeforeSave(uint docCookie)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                RefreshFromDte();
            });

            return VSConstants.S_OK;
        }

        public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        private void UpdateDirtyStateFromRdt(uint docCookie)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_runningDocumentTable == null)
            {
                return;
            }

            ErrorHandler.ThrowOnFailure(_runningDocumentTable.GetDocumentInfo(
                docCookie,
                out var flags,
                out _,
                out _,
                out var moniker,
                out _,
                out _,
                out _));

            if (string.IsNullOrEmpty(moniker))
            {
                return;
            }

            var item = FindItemByPath(moniker);
            if (item != null)
            {
                var document = FindOpenDocument(moniker);
                item.IsDirty = document != null && !document.Saved;
            }
        }

        public void Dispose()
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if (_documentEvents != null)
                {
                    _documentEvents.DocumentOpened -= OnDocumentOpened;
                    _documentEvents.DocumentClosing -= OnDocumentClosing;
                    _documentEvents.DocumentSaved -= OnDocumentSaved;
                }

                if (_windowEvents != null)
                {
                    _windowEvents.WindowActivated -= OnWindowActivated;
                }

                if (_solutionEvents != null)
                {
                    _solutionEvents.Opened -= OnSolutionOpened;
                    _solutionEvents.AfterClosing -= OnSolutionAfterClosing;
                }

                if (_runningDocumentTable != null && _rdtEventsCookie != 0)
                {
                    _runningDocumentTable.UnadviseRunningDocTableEvents(_rdtEventsCookie);
                    _rdtEventsCookie = 0;
                }

                _statusRefreshCancellationTokenSource?.Cancel();
                _statusRefreshCancellationTokenSource?.Dispose();
                _statusRefreshCancellationTokenSource = null;

                _themeService.ThemeChanged -= OnThemeChanged;
                _themeService.Dispose();
            });
        }
    }

    public interface IGitStatusService
    {
        Task RefreshAsync(IEnumerable<string> fullPaths, CancellationToken cancellationToken);

        GitDocumentStatus GetCachedStatus(string fullPath);
    }

    public interface IDocumentErrorService
    {
        Task<ISet<string>> GetFilesWithErrorsAsync(CancellationToken cancellationToken);
    }

    public interface IFileIconService
    {
        Task<ImageMoniker> GetIconAsync(string fullPath, CancellationToken cancellationToken);
    }

    public interface IThemeService : IDisposable
    {
        event EventHandler ThemeChanged;
    }

    /// <summary>
    /// Resolves file icons using Visual Studio shell image services and caches monikers by file extension.
    /// </summary>
    public sealed class FileIconService : IFileIconService
    {
        private readonly AsyncPackage _package;
        private readonly ConcurrentDictionary<string, ImageMoniker> _extensionIconCache = new ConcurrentDictionary<string, ImageMoniker>(StringComparer.OrdinalIgnoreCase);

        private IVsImageService2 _imageService;

        public FileIconService(AsyncPackage package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
        }

        public async Task<ImageMoniker> GetIconAsync(string fullPath, CancellationToken cancellationToken)
        {
            var extension = Path.GetExtension(fullPath) ?? string.Empty;
            if (_extensionIconCache.TryGetValue(extension, out var cached))
            {
                return cached;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            if (_imageService == null)
            {
                _imageService = await _package.GetServiceAsync(typeof(SVsImageService)) as IVsImageService2;
            }

            ImageMoniker moniker = KnownMonikers.Document;
            if (_imageService != null && !string.IsNullOrWhiteSpace(fullPath))
            {
                if (!TryGetMonikerFromImageService(fullPath, out moniker))
                {
                    moniker = KnownMonikers.Document;
                }
            }

            if (moniker.Guid == Guid.Empty && moniker.Id == 0)
            {
                moniker = KnownMonikers.Document;
            }

            _extensionIconCache[extension] = moniker;
            return moniker;
        }

        private bool TryGetMonikerFromImageService(string fullPath, out ImageMoniker moniker)
        {
            moniker = default(ImageMoniker);

            var serviceType = _imageService.GetType();
            var method = serviceType.GetMethod("GetImageMonikerForFile", BindingFlags.Public | BindingFlags.Instance);
            if (method == null)
            {
                return false;
            }

            var parameters = method.GetParameters();
            var args = new object[parameters.Length];
            if (parameters.Length == 1)
            {
                args[0] = fullPath;
                var result = method.Invoke(_imageService, args);
                if (result is ImageMoniker monikerResult)
                {
                    moniker = monikerResult;
                    return moniker.Guid != Guid.Empty || moniker.Id != 0;
                }

                return false;
            }

            if (parameters.Length == 2)
            {
                args[0] = fullPath;
                args[1] = null;
                var result = method.Invoke(_imageService, args);
                if (result is int hr && ErrorHandler.Succeeded(hr) && args[1] is ImageMoniker monikerOut)
                {
                    moniker = monikerOut;
                    return moniker.Guid != Guid.Empty || moniker.Id != 0;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Wraps Visual Studio theme notifications so UI can react to dynamic theme switches.
    /// </summary>
    public sealed class ThemeService : IThemeService
    {
        public event EventHandler ThemeChanged;

        public ThemeService()
        {
            VSColorTheme.ThemeChanged += OnThemeChanged;
        }

        private void OnThemeChanged(ThemeChangedEventArgs e)
        {
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            VSColorTheme.ThemeChanged -= OnThemeChanged;
        }
    }

    /// <summary>
    /// Reads Visual Studio Error List items and maps them to source file paths.
    /// This keeps error tracking logic inside a service and out of the UI layer.
    /// </summary>
    public sealed class DocumentErrorService : IDocumentErrorService
    {
        private readonly AsyncPackage _package;

        public DocumentErrorService(AsyncPackage package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
        }

        public async Task<ISet<string>> GetFilesWithErrorsAsync(CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dte = await _package.GetServiceAsync(typeof(DTE)) as DTE2;
            var errorItems = dte?.ToolWindows?.ErrorList?.ErrorItems;
            if (errorItems == null)
            {
                return result;
            }

            for (var i = 1; i <= errorItems.Count; i++)
            {
                ErrorItem item;
                try
                {
                    item = errorItems.Item(i);
                }
                catch
                {
                    continue;
                }

                if (item == null || item.ErrorLevel != vsBuildErrorLevel.vsBuildErrorLevelHigh)
                {
                    continue;
                }

                var filePath = item.FileName;
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    continue;
                }

                try
                {
                    result.Add(Path.GetFullPath(filePath));
                }
                catch
                {
                    result.Add(filePath);
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Uses git porcelain output and caches parsed status per path to avoid repeated expensive calls.
    /// </summary>
    public sealed class GitStatusService : IGitStatusService
    {
        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(2);

        private readonly ConcurrentDictionary<string, GitDocumentStatus> _statusCache = new ConcurrentDictionary<string, GitDocumentStatus>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, string> _repoRootCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _refreshSemaphore = new SemaphoreSlim(1, 1);

        private DateTime _lastRefreshUtc = DateTime.MinValue;

        public async Task RefreshAsync(IEnumerable<string> fullPaths, CancellationToken cancellationToken)
        {
            var paths = fullPaths?
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (paths == null || paths.Count == 0)
            {
                return;
            }

            if (DateTime.UtcNow - _lastRefreshUtc <= CacheDuration)
            {
                return;
            }

            await _refreshSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (DateTime.UtcNow - _lastRefreshUtc <= CacheDuration)
                {
                    return;
                }

                var groupedByRepo = paths
                    .Select(path => new { Path = path, Repo = FindRepositoryRoot(path) })
                    .Where(x => !string.IsNullOrEmpty(x.Repo))
                    .GroupBy(x => x.Repo, StringComparer.OrdinalIgnoreCase);

                foreach (var group in groupedByRepo)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await RefreshRepositoryStatusAsync(group.Key, cancellationToken).ConfigureAwait(false);
                }

                _lastRefreshUtc = DateTime.UtcNow;
            }
            finally
            {
                _refreshSemaphore.Release();
            }
        }

        public GitDocumentStatus GetCachedStatus(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                return GitDocumentStatus.Clean;
            }

            return _statusCache.TryGetValue(fullPath, out var status)
                ? status
                : GitDocumentStatus.Clean;
        }

        private async Task RefreshRepositoryStatusAsync(string repoRoot, CancellationToken cancellationToken)
        {
            var output = await RunGitStatusAsync(repoRoot, cancellationToken).ConfigureAwait(false);
            if (output == null)
            {
                return;
            }

            var repoPrefix = repoRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            foreach (var entry in output)
            {
                var fullPath = Path.GetFullPath(Path.Combine(repoRoot, entry.RelativePath));
                _statusCache[fullPath] = entry.Status;
            }

            var trackedOutputPaths = new HashSet<string>(
                output.Select(o => Path.GetFullPath(Path.Combine(repoRoot, o.RelativePath))),
                StringComparer.OrdinalIgnoreCase);

            var cachedKeys = _statusCache.Keys.Where(k => k.StartsWith(repoPrefix, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var key in cachedKeys)
            {
                if (!trackedOutputPaths.Contains(key))
                {
                    _statusCache[key] = GitDocumentStatus.Clean;
                }
            }
        }

        private async Task<IReadOnlyList<(string RelativePath, GitDocumentStatus Status)>> RunGitStatusAsync(string repoRoot, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"-C \"{repoRoot}\" status --porcelain --untracked-files=all",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using (var process = new System.Diagnostics.Process { StartInfo = processStartInfo })
                {
                    try
                    {
                        if (!process.Start())
                        {
                            return (IReadOnlyList<(string RelativePath, GitDocumentStatus Status)>)Array.Empty<(string, GitDocumentStatus)>();
                        }
                    }
                    catch
                    {
                        return (IReadOnlyList<(string RelativePath, GitDocumentStatus Status)>)Array.Empty<(string, GitDocumentStatus)>();
                    }

                    var lines = new List<(string RelativePath, GitDocumentStatus Status)>();
                    while (!process.StandardOutput.EndOfStream)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        string line = process.StandardOutput.ReadLine();
                        if (string.IsNullOrWhiteSpace(line) || line.Length < 4)
                        {
                            continue;
                        }

                        var statusCode = line.Substring(0, 2).Trim();
                        string relativePath = line.Substring(3).Trim();
                        if (relativePath.StartsWith("\"", StringComparison.Ordinal) && relativePath.EndsWith("\"", StringComparison.Ordinal))
                        {
                            relativePath = relativePath.Trim('"');
                        }

                        lines.Add((relativePath, ParseStatus(statusCode)));
                    }

                    process.WaitForExit();
                    return (IReadOnlyList<(string RelativePath, GitDocumentStatus Status)>)lines;
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        private static GitDocumentStatus ParseStatus(string statusCode)
        {
            if (string.IsNullOrWhiteSpace(statusCode))
            {
                return GitDocumentStatus.Clean;
            }

            if (statusCode.IndexOf('D') >= 0)
            {
                return GitDocumentStatus.Deleted;
            }

            if (statusCode.IndexOf('A') >= 0 || statusCode == "??")
            {
                return GitDocumentStatus.Added;
            }

            if (statusCode.IndexOf('M') >= 0 || statusCode.IndexOf('R') >= 0 || statusCode.IndexOf('C') >= 0 || statusCode.IndexOf('U') >= 0)
            {
                return GitDocumentStatus.Modified;
            }

            return GitDocumentStatus.Clean;
        }

        private string FindRepositoryRoot(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                return null;
            }

            var normalized = Path.GetFullPath(fullPath);
            if (_repoRootCache.TryGetValue(normalized, out var cachedRoot))
            {
                return cachedRoot;
            }

            var directory = File.Exists(normalized)
                ? Path.GetDirectoryName(normalized)
                : normalized;

            while (!string.IsNullOrEmpty(directory))
            {
                var gitDirectory = Path.Combine(directory, ".git");
                if (Directory.Exists(gitDirectory) || File.Exists(gitDirectory))
                {
                    _repoRootCache[normalized] = directory;
                    return directory;
                }

                directory = Path.GetDirectoryName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }

            _repoRootCache[normalized] = null;
            return null;
        }
    }
}
