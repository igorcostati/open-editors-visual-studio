using Microsoft.VisualStudio.Shell;
using OpenEditors.VisualStudio.Vsix.Services;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace OpenEditors.VisualStudio.Vsix.ToolWindows
{
    [Guid(PackageGuids.OpenEditorsToolWindowString)]
    public class OpenEditorsToolWindow : ToolWindowPane
    {
        private readonly OpenEditorsToolWindowControl _control;

        public OpenEditorsToolWindow() : base(null)
        {
            Caption = "Open Editors";
            _control = new OpenEditorsToolWindowControl();
            Content = _control;
        }

        public async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var packageInstance = package as OpenEditorsVisualStudioVsixPackage;
            var documentService = packageInstance?.DocumentService
                ?? await package.GetServiceAsync(typeof(OpenEditorsDocumentService)) as OpenEditorsDocumentService;

            if (documentService == null)
            {
                return;
            }

            await _control.InitializeAsync(documentService, package);

            await documentService.RefreshDocumentsAsync(package.DisposalToken);
        }
    }
}
