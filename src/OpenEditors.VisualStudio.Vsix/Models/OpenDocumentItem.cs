using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.Imaging.Interop;

namespace OpenEditors.VisualStudio.Vsix.Models
{
    public enum GitDocumentStatus
    {
        Clean = 0,
        Modified = 1,
        Added = 2,
        Deleted = 3
    }

    public class OpenDocumentItem : INotifyPropertyChanged
    {
        private bool _isActive;
        private bool _isDirty;
        private bool _hasErrors;
        private GitDocumentStatus _gitStatus;
        private ImageMoniker _icon;

        public string FileName { get; set; }

        public string FullPath { get; set; }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive == value)
                {
                    return;
                }

                _isActive = value;
                OnPropertyChanged();
            }
        }

        public bool HasErrors
        {
            get => _hasErrors;
            set
            {
                if (_hasErrors == value)
                {
                    return;
                }

                _hasErrors = value;
                OnPropertyChanged();
            }
        }

        public GitDocumentStatus GitStatus
        {
            get => _gitStatus;
            set
            {
                if (_gitStatus == value)
                {
                    return;
                }

                _gitStatus = value;
                OnPropertyChanged();
            }
        }

        public ImageMoniker Icon
        {
            get => _icon;
            set
            {
                if (_icon.Equals(value))
                {
                    return;
                }

                _icon = value;
                OnPropertyChanged();
            }
        }

        public bool IsDirty
        {
            get => _isDirty;
            set
            {
                if (_isDirty == value)
                {
                    return;
                }

                _isDirty = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
