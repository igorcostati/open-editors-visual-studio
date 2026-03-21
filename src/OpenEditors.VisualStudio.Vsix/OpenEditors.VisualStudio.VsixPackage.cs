using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OpenEditors.VisualStudio.Vsix.Commands;
using OpenEditors.VisualStudio.Vsix.Services;

namespace OpenEditors.VisualStudio.Vsix
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("Open Editors", "Tracks and manages open editors", "1.0")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(ToolWindows.OpenEditorsToolWindow))]
    [ProvideService(typeof(OpenEditorsDocumentService))]
    [ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid(PackageGuidString)]
    public sealed class OpenEditorsVisualStudioVsixPackage : AsyncPackage
    {
         /// <summary>
         /// OpenEditors.VisualStudio.VsixPackage GUID string.
         /// </summary>
        public const string PackageGuidString = "eddeda39-af65-4734-ba70-6366d3d40275";

        internal OpenEditorsDocumentService DocumentService { get; private set; }
        internal string ContextDocumentPath { get; set; }

    #region Package Members

    /// <summary>
    /// Initialization of the package; this method is called right after the package is sited, so this is the place
    /// where you can put all the initialization code that rely on services provided by VisualStudio.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
    /// <param name="progress">A provider for progress updates.</param>
    /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        AddService(typeof(OpenEditorsDocumentService), CreateDocumentTrackingServiceAsync, promote: true);

        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        await OpenEditorsToolWindowCommand.InitializeAsync(this);
        await OpenEditorsContextMenuCommand.InitializeAsync(this);
    }

    private async Task<object> CreateDocumentTrackingServiceAsync(IAsyncServiceContainer container, CancellationToken cancellationToken, Type serviceType)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var gitStatusService = new GitStatusService();
        var documentErrorService = new DocumentErrorService(this);
        var fileIconService = new FileIconService(this);
        var themeService = new ThemeService();
        var service = new OpenEditorsDocumentService(this, gitStatusService, documentErrorService, fileIconService, themeService);
        await service.InitializeAsync(cancellationToken);
        DocumentService = service;
        return service;
    }

    #endregion
}
}
