using System;
using System.Globalization;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Core.Localization;

namespace GCodeGenerator.Core.ViewModels.PropertiesViewModel;

public partial class PropertyItemViewModel : ObservableObject
{
    private readonly object _target;
    private readonly PropertyInfo _property;
    private readonly Action? _onValueChanged;

    public string DisplayName { get; }

    public Type PropertyType => _property.PropertyType;

    public bool IsBoolean =>
        PropertyType == typeof(bool) ||
        PropertyType == typeof(bool?);

    public bool IsEditable => _property.CanWrite;

    public PropertyItemViewModel(object target, PropertyInfo property, string displayName, Action? onValueChanged)
    {
        _target = target;
        _property = property;
        DisplayName = displayName;
        _onValueChanged = onValueChanged;
    }

    /// <summary>
    /// Строковое представление значения (для текстового редактора).
    /// </summary>
    public string Value
    {
        get
        {
            var value = _property.GetValue(_target);
            return ConvertToString(value);
        }
        set
        {
            if (!IsEditable)
                return;

            try
            {
                object? converted = ConvertFromString(value, PropertyType);
                _property.SetValue(_target, converted);
                OnPropertyChanged(nameof(Value));
                if (IsBoolean)
                    OnPropertyChanged(nameof(BoolValue));
                _onValueChanged?.Invoke();
            }
            catch
            {
                // Игнорируем ошибки конвертации, оставляя старое значение
            }
        }
    }

    /// <summary>
    /// Значение для булевых свойств (редактор CheckBox).
    /// </summary>
    public bool BoolValue
    {
        get
        {
            var value = _property.GetValue(_target);
            if (value is bool b) return b;
            return false;
        }
        set
        {
            if (!IsEditable)
                return;

            _property.SetValue(_target, value);
            OnPropertyChanged(nameof(BoolValue));
            OnPropertyChanged(nameof(Value));
            _onValueChanged?.Invoke();
        }
    }

    /// <summary>
    /// Обновляет отображаемое значение, вызывая уведомление об изменении свойства.
    /// </summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Value));
        if (IsBoolean)
            OnPropertyChanged(nameof(BoolValue));
    }

    private static string ConvertToString(object? value)
    {
        if (value is null)
            return string.Empty;

        // Форматируем числовые типы: целые числа без десятичных знаков, дробные - с 3 знаками после запятой
        return value switch
        {
            double d => d.ToString("F3", CultureInfo.CurrentCulture),
            float f => f.ToString("F3", CultureInfo.CurrentCulture),
            decimal dec => dec.ToString("F3", CultureInfo.CurrentCulture),
            int i => i.ToString(CultureInfo.CurrentCulture),
            long l => l.ToString(CultureInfo.CurrentCulture),
            short s => s.ToString(CultureInfo.CurrentCulture),
            byte b => b.ToString(CultureInfo.CurrentCulture),
            sbyte sb => sb.ToString(CultureInfo.CurrentCulture),
            ushort us => us.ToString(CultureInfo.CurrentCulture),
            uint ui => ui.ToString(CultureInfo.CurrentCulture),
            ulong ul => ul.ToString(CultureInfo.CurrentCulture),
            _ => Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty
        };
    }

    private static object? ConvertFromString(string text, Type targetType)
    {
        if (targetType == typeof(string))
            return text;

        if (string.IsNullOrWhiteSpace(text))
        {
            if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
                return Activator.CreateInstance(targetType);

            return null;
        }

        var nonNullableType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (nonNullableType.IsEnum)
        {
            return Enum.Parse(nonNullableType, text, ignoreCase: true);
        }

        return Convert.ChangeType(text, nonNullableType, CultureInfo.CurrentCulture);
    }
}


