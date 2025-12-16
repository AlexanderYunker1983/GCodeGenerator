using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GCodeGenerator.Models
{
    public abstract class OperationBase : INotifyPropertyChanged
    {
        private string _name;
        private bool _isEnabled = true;

        protected OperationBase(OperationType type, string name)
        {
            Type = type;
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public OperationType Type { get; }

        /// <summary>
        /// User-friendly name of operation, shown in UI.
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                if (Equals(value, _name)) return;
                _name = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Indicates whether the operation is enabled and should be used
        /// in G-code generation and previews.
        /// Defaults to <c>true</c> so that legacy project files where this
        /// field is absent will treat operations as enabled.
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (value == _isEnabled) return;
                _isEnabled = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Short human readable description for list in UI.
        /// </summary>
        public abstract string GetDescription();

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}


