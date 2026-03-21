using Microsoft.VisualStudio.Shell;
using OpenEditors.VisualStudio.Vsix.Services;
using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;

namespace OpenEditors.VisualStudio.Vsix.Commands
{
    internal sealed class OpenEditorsContextMenuCommand
    {
        private readonly OpenEditorsVisualStudioVsixPackage _package;
        private readonly OleMenuCommand _saveCommand;
        private readonly OleMenuCommand _duplicateCommand;
        private readonly OleMenuCommand _closeCommand;
        private readonly OleMenuCommand _closeAllCommand;

        private OpenEditorsContextMenuCommand(OpenEditorsVisualStudioVsixPackage package, IMenuCommandService commandService)
        {
            _package = package;

            _saveCommand = CreateCommand(commandService, PackageIds.OpenEditorsContextSaveCommandId, SaveDocumentAsync);
            _duplicateCommand = CreateCommand(commandService, PackageIds.OpenEditorsContextDuplicateCommandId, DuplicateDocumentAsync);
            _closeCommand = CreateCommand(commandService, PackageIds.OpenEditorsContextCloseCommandId, CloseDocumentAsync);
            _closeAllCommand = CreateCommand(commandService, PackageIds.OpenEditorsContextCloseAllCommandId, CloseAllDocumentsAsync);

            _saveCommand.BeforeQueryStatus += OnSingleDocumentBeforeQueryStatus;
            _duplicateCommand.BeforeQueryStatus += OnSingleDocumentBeforeQueryStatus;
            _closeCommand.BeforeQueryStatus += OnSingleDocumentBeforeQueryStatus;
            _closeAllCommand.BeforeQueryStatus += OnCloseAllBeforeQueryStatus;
        }

        public static async Task InitializeAsync(OpenEditorsVisualStudioVsixPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as IMenuCommandService;
            if (commandService == null)
            {
                return;
            }

            _ = new OpenEditorsContextMenuCommand(package, commandService);
        }

        private OleMenuCommand CreateCommand(IMenuCommandService commandService, int commandId, EventHandler handler)
        {
            var menuCommandId = new CommandID(PackageGuids.OpenEditorsCommandSet, commandId);
            var command = new OleMenuCommand(handler, menuCommandId);
            commandService.AddCommand(command);
            return command;
        }

        private void OnSingleDocumentBeforeQueryStatus(object sender, EventArgs e)
        {
            var command = sender as OleMenuCommand;
            if (command == null)
            {
                return;
            }

            command.Visible = true;
            command.Enabled = _package.DocumentService != null && !string.IsNullOrWhiteSpace(_package.ContextDocumentPath);
        }

        private void OnCloseAllBeforeQueryStatus(object sender, EventArgs e)
        {
            var command = sender as OleMenuCommand;
            if (command == null)
            {
                return;
            }

            command.Visible = true;
            command.Enabled = _package.DocumentService != null;
        }

        private void SaveDocumentAsync(object sender, EventArgs e)
        {
            var fullPath = _package.ContextDocumentPath;
            _package.JoinableTaskFactory.RunAsync(async delegate
            {
                if (_package.DocumentService != null && !string.IsNullOrWhiteSpace(fullPath))
                {
                    await _package.DocumentService.SaveDocumentAsync(fullPath);
                }
            });
        }

        private void DuplicateDocumentAsync(object sender, EventArgs e)
        {
            var fullPath = _package.ContextDocumentPath;
            _package.JoinableTaskFactory.RunAsync(async delegate
            {
                if (_package.DocumentService != null && !string.IsNullOrWhiteSpace(fullPath))
                {
                    await _package.DocumentService.DuplicateDocumentAsync(fullPath);
                }
            });
        }

        private void CloseDocumentAsync(object sender, EventArgs e)
        {
            var fullPath = _package.ContextDocumentPath;
            _package.JoinableTaskFactory.RunAsync(async delegate
            {
                if (_package.DocumentService != null && !string.IsNullOrWhiteSpace(fullPath))
                {
                    await _package.DocumentService.CloseDocumentAsync(fullPath);
                }
            });
        }

        private void CloseAllDocumentsAsync(object sender, EventArgs e)
        {
            _package.JoinableTaskFactory.RunAsync(async delegate
            {
                if (_package.DocumentService != null)
                {
                    await _package.DocumentService.CloseAllDocumentsAsync();
                }
            });
        }
    }
}
