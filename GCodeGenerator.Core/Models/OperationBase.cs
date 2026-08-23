using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Base class for all operations.
    ///
    /// INotifyPropertyChanged is implemented here directly (status quo,
    /// plan item 3.9): the operation list UI binds to the model itself
    /// (MainView.xaml binds <c>IsEnabled</c>/<c>Name</c> on the operation)
    /// and MainViewModel listens to the operation's PropertyChanged to
    /// refresh the preview when the enabled flag is toggled. Extracting a
    /// separate IEnabledOperation interface would not remove this dependency
    /// — concrete operation instances are what the UI consumes — and would
    /// only split the contract. If a non-UI consumer ever needs operations
    /// without INPC, the interface can be extracted at that point.
    /// </summary>
    public abstract class OperationBase : INotifyPropertyChanged
    {
        private string _name;
        private bool _isEnabled = true;

        protected OperationBase(OperationType type, OperationCategory category, string name)
        {
            Type = type;
            Category = category;
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public OperationType Type { get; }

        /// <summary>
        /// Категория операции (Drill/Profile/Pocket), пункт 7.2 плана.
        /// Сериализуется в .ygc не будет ([JsonIgnore]): восстанавливается
        /// конструктором конкретного класса.
        /// </summary>
        [JsonIgnore]
        public OperationCategory Category { get; }

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


