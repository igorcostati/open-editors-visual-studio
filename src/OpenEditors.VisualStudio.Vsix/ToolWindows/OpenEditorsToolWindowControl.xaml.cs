using OpenEditors.VisualStudio.Vsix.Models;
using OpenEditors.VisualStudio.Vsix.Services;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OpenEditors.VisualStudio.Vsix.ToolWindows
{
    public partial class OpenEditorsToolWindowControl : UserControl
    {
        private OpenEditorsDocumentService _documentService;
        private AsyncPackage _package;
        private IVsUIShell _uiShell;

        public OpenEditorsToolWindowControl()
        {
            InitializeComponent();
        }

        public async Task InitializeAsync(OpenEditorsDocumentService documentService, AsyncPackage package)
        {
            _documentService = documentService;
            _package = package;
            DataContext = documentService;
            DocumentsList.ItemsSource = documentService?.Documents;
            _uiShell = await package.GetServiceAsync(typeof(SVsUIShell)) as IVsUIShell;
        }

        private void DocumentsList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_documentService == null)
            {
                return;
            }

            if (DocumentsList.SelectedItem is OpenDocumentItem item)
            {
                _ = _documentService.ActivateDocumentAsync(item.FullPath);
            }
        }

        private void CloseDocument_OnClick(object sender, RoutedEventArgs e)
        {
            var contextItem = (sender as FrameworkElement)?.DataContext as OpenDocumentItem
                ?? DocumentsList.SelectedItem as OpenDocumentItem;

            if (contextItem != null)
            {
                ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
                {
                    if (_documentService != null)
                    {
                        await _documentService.CloseDocumentAsync(contextItem.FullPath);
                    }
                });
            }
        }

        private void ListBoxItem_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item)
            {
                item.IsSelected = true;
            }
        }

        private void ListBoxItem_OnPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var selected = DocumentsList.SelectedItem as OpenDocumentItem;
            var package = _package as OpenEditorsVisualStudioVsixPackage;
            if (selected == null || package == null || _uiShell == null)
            {
                return;
            }

            package.ContextDocumentPath = selected.FullPath;

            var screenPoint = PointToScreen(e.GetPosition(this));
            var point = new POINTS
            {
                x = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, (int)screenPoint.X)),
                y = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, (int)screenPoint.Y)),
            };

            var commandSet = PackageGuids.OpenEditorsCommandSet;
            _uiShell.ShowContextMenu(0, ref commandSet, PackageIds.OpenEditorsContextMenu, new[] { point }, null);
            e.Handled = true;
        }

    }

}
