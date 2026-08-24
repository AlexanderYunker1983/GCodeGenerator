using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Creates an isolated operation graph for a modal editor and commits it
    /// back while preserving the identity of the operation stored in the main
    /// collection. Nested holes/DXF points are cloned through the same stable
    /// serializer used by project files.
    /// </summary>
    internal static class OperationEditTransaction
    {
        private static readonly ProjectFileService SnapshotSerializer = new ProjectFileService();

        public static OperationBase CreateWorkingCopy(OperationBase source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var snapshot = SnapshotSerializer.Serialize(new[] { source }, null);
            var operations = SnapshotSerializer.Deserialize(snapshot).Operations;
            if (operations == null || operations.Count != 1 || operations[0].GetType() != source.GetType())
                throw new InvalidOperationException($"Не удалось создать рабочую копию операции {source.GetType().Name}.");

            return operations[0];
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
