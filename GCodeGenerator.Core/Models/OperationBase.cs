using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
    public abstract class OperationBase : ObservableObject, INotifyDataErrorInfo
    {
        private string _name;
        private bool _isEnabled = true;
        private IReadOnlyList<ValidationIssue> _issues;
        private bool _issuesAreStale = true;

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

        // --- Ошибки параметров ------------------------------------------------

        /// <summary>
        /// Проблемы параметров, найденные доменной проверкой. Окно показывает
        /// их прямо у полей: то же правило, по которому генерация отказывается
        /// строить программу, видно пользователю до нажатия кнопки.
        /// </summary>
        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        /// <inheritdoc />
        public bool HasErrors => Issues.Count > 0;

        /// <inheritdoc />
        public IEnumerable GetErrors(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return Issues.Select(ValidationMessages.Describe).ToList();

            return Issues
                .Where(issue => string.Equals(issue.ParameterName, propertyName, StringComparison.Ordinal))
                .Select(ValidationMessages.Describe)
                .ToList();
        }

        /// <summary>
        /// Список проблем. Считается лениво и запоминается: проверка идёт
        /// по всей операции, а спрашивают о ней по одному полю за раз.
        /// </summary>
        private IReadOnlyList<ValidationIssue> Issues
        {
            get
            {
                if (!_issuesAreStale)
                    return _issues;

                _issues = this is IValidatable validatable
                    ? validatable.Validate() ?? Array.Empty<ValidationIssue>()
                    : Array.Empty<ValidationIssue>();
                _issuesAreStale = false;
                return _issues;
            }
        }

        /// <summary>
        /// Любое изменение параметра делает прежний список проблем
        /// недействительным: правка одного поля способна исправить или
        /// вызвать ошибку в другом.
        /// </summary>
        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            // Об ошибках сообщает отдельное событие: обычное уведомление
            // означало бы ещё одно изменение операции, а от них пересобирается
            // предпросмотр — вдвое чаще, чем нужно.
            _issuesAreStale = true;
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(null));
        }
    }
}
