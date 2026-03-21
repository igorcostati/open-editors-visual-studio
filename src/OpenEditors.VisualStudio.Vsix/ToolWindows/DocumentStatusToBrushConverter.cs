using Microsoft.VisualStudio.Shell;
using OpenEditors.VisualStudio.Vsix.Models;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace OpenEditors.VisualStudio.Vsix.ToolWindows
{
    public sealed class GitStatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var status = value is GitDocumentStatus gitStatus ? gitStatus : GitDocumentStatus.Clean;

            switch (status)
            {
                case GitDocumentStatus.Modified:
                    return ResolveVsBrush(VsBrushes.ToolWindowTextKey);
                case GitDocumentStatus.Added:
                    return ResolveVsBrush(VsBrushes.ToolWindowBackgroundKey);
                case GitDocumentStatus.Deleted:
                    return ResolveVsBrush(VsBrushes.ToolWindowBorderKey);
                default:
                    return Brushes.Transparent;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private static Brush ResolveVsBrush(object key)
        {
            return Application.Current.TryFindResource(key) as Brush ?? Brushes.Transparent;
        }
    }

    public sealed class DocumentStatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is OpenDocumentItem item))
            {
                return Brushes.Transparent;
            }

            if (item.HasErrors)
            {
                return Application.Current.TryFindResource(VsBrushes.ToolWindowBorderKey) as Brush
                    ?? Brushes.Transparent;
            }

            return new GitStatusToBrushConverter().Convert(item.GitStatus, targetType, parameter, culture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
