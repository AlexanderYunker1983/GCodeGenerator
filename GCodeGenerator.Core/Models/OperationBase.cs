using System;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Base class for all operations.
    ///
    /// Операция сама сообщает об изменении своих параметров: на этом держатся
    /// перерисовка предпросмотра и признак несохранённого проекта. Раньше
    /// уведомляли только имя и признак «включена», а геометрия была обычными
    /// свойствами, поэтому каждое место, меняющее операцию, обязано было
    /// вручную позвать «содержимое изменилось» — забытый вызов проявлялся
    /// не ошибкой, а неперерисованным предпросмотром.
    /// </summary>
    public abstract class OperationBase : ObservableObject
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
            set => SetProperty(ref _name, value);
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
            set => SetProperty(ref _isEnabled, value);
        }

        /// <summary>
        /// Short human readable description for list in UI.
        /// </summary>
        public abstract string GetDescription();

        /// <summary>
        /// Явное уведомление «изменилось всё сразу». Нужно там, где параметры
        /// операции заменяются скопом и слушателя интересует только сам факт:
        /// импорт чертежа заполняет операцию целиком.
        /// </summary>
        public void NotifyContentChanged() => OnPropertyChanged(string.Empty);
    }
}
