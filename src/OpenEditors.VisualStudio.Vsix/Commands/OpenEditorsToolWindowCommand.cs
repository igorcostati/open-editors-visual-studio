using Microsoft.VisualStudio.Shell;
using OpenEditors.VisualStudio.Vsix.ToolWindows;
using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.Threading.Tasks;

namespace OpenEditors.VisualStudio.Vsix.Commands
{
    internal sealed class OpenEditorsToolWindowCommand
    {
        private readonly AsyncPackage _package;
        private readonly OleMenuCommand _menuItem;

        private OpenEditorsToolWindowCommand(AsyncPackage package, IMenuCommandService commandService)
        {
            _package = package;
            var menuCommandId = new CommandID(PackageGuids.OpenEditorsCommandSet, PackageIds.OpenEditorsToolWindowCommandId);
            _menuItem = new OleMenuCommand(Execute, menuCommandId);
            _menuItem.BeforeQueryStatus += MenuItem_OnBeforeQueryStatus;
            commandService.AddCommand(_menuItem);
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as IMenuCommandService;
            if (commandService == null)
            {
                return;
            }

            _ = new OpenEditorsToolWindowCommand(package, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            _package.JoinableTaskFactory.RunAsync(async delegate
            {
                ToolWindowPane window = await _package.ShowToolWindowAsync(typeof(OpenEditorsToolWindow), 0, true, _package.DisposalToken);
                if (window is OpenEditorsToolWindow toolWindow)
                {
                    toolWindow.Caption = GetLocalizedOpenEditorsText();
                    await toolWindow.InitializeAsync(_package);
                }
            });
        }

        private void MenuItem_OnBeforeQueryStatus(object sender, EventArgs e)
        {
            _menuItem.Text = GetLocalizedOpenEditorsText();
        }

        private static string GetLocalizedOpenEditorsText()
        {
            var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            switch (language)
            {
                case "pt": return "Editores Abertos";
                case "es": return "Editores Abiertos";
                case "fr": return "Éditeurs ouverts";
                case "de": return "Geöffnete Editoren";
                case "it": return "Editor aperti";
                case "ja": return "開いているエディター";
                case "ko": return "열린 편집기";
                case "zh": return "打开的编辑器";
                case "ru": return "Открытые редакторы";
                default: return "Open Editors";
            }
        }
    }
}
