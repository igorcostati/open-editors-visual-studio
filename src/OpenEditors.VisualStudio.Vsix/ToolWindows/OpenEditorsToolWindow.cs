using Microsoft.VisualStudio.Shell;
using OpenEditors.VisualStudio.Vsix.Services;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace OpenEditors.VisualStudio.Vsix.ToolWindows
{
    [Guid(PackageGuids.OpenEditorsToolWindowString)]
    public class OpenEditorsToolWindow : ToolWindowPane
    {
        private readonly OpenEditorsToolWindowControl _control;
        private bool _isInitialized;
        private readonly SemaphoreSlim _initializeGate = new SemaphoreSlim(1, 1);

        public OpenEditorsToolWindow() : base(null)
        {
            Caption = "Open Editors";
            _control = new OpenEditorsToolWindowControl();
            Content = _control;
        }

        public override void OnToolWindowCreated()
        {
            base.OnToolWindowCreated();

            var package = Package as AsyncPackage;
            if (package == null)
            {
                return;
            }

            package.JoinableTaskFactory.RunAsync(async delegate
            {
                await InitializeAsync(package);
            });
        }

        public async Task InitializeAsync(AsyncPackage package)
        {
            await _initializeGate.WaitAsync(package.DisposalToken);
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

                var packageInstance = package as OpenEditorsVisualStudioVsixPackage;
                var documentService = packageInstance?.DocumentService
                    ?? await package.GetServiceAsync(typeof(OpenEditorsDocumentService)) as OpenEditorsDocumentService;

                if (documentService == null)
                {
                    return;
                }

                if (!_isInitialized)
                {
                    await _control.InitializeAsync(documentService, package);
                    _isInitialized = true;
                }

                await documentService.RefreshDocumentsAsync(package.DisposalToken);
            }
            finally
            {
                _initializeGate.Release();
            }
        }
    }
}
