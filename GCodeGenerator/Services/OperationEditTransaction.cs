using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Даёт модальному редактору отдельную копию операции и переносит
    /// изменения обратно, сохраняя тождество операции в общем списке:
    /// подписки на неё и её место в порядке обработки не должны страдать
    /// от редактирования.
    ///
    /// Копию создаёт <see cref="OperationCloner"/> — тот же механизм, что
    /// применяется при черновой и чистовой обработке кармана, поэтому состав
    /// переносимых данных везде одинаков.
    /// </summary>
    internal static class OperationEditTransaction
    {
        public static OperationBase CreateWorkingCopy(OperationBase source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return OperationCloner.Clone(source);
        }

        public static void Commit(OperationBase workingCopy, OperationBase target)
        {
            if (workingCopy == null)
                throw new ArgumentNullException(nameof(workingCopy));
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (workingCopy.GetType() != target.GetType())
                throw new InvalidOperationException("Тип рабочей копии не совпадает с типом исходной операции.");

            // Повторное клонирование отделяет сохранённую модель от VM даже если
            // ссылка на диалог по ошибке будет удерживаться после его закрытия.
            var committedCopy = CreateWorkingCopy(workingCopy);
            var properties = target.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.CanRead
                    && property.SetMethod?.IsPublic == true
                    && property.GetIndexParameters().Length == 0
                    && property.GetCustomAttribute<JsonIgnoreAttribute>() == null)
                .ToList();

            var values = new Dictionary<PropertyInfo, object>();
            foreach (var property in properties)
                values[property] = property.GetValue(committedCopy);

            foreach (var pair in values)
                pair.Key.SetValue(target, pair.Value);

            target.NotifyContentChanged();
        }
    }
}
