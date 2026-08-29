#nullable enable
using System;
using System.Threading.Tasks;

namespace GCodeGenerator.Services
{
    /// <summary>Отложенный атомарный снимок несохранённого документа.</summary>
    public interface IDocumentRecoveryService
    {
        /// <summary>Предсказуемый файл, который можно открыть как обычный проект.</summary>
        string RecoveryPath { get; }

        /// <summary>Резервная версия предыдущего успешного автоснимка.</summary>
        string BackupPath { get; }

        /// <summary>Остался ли снимок от предыдущего запуска.</summary>
        bool Exists { get; }

        /// <summary>Существует ли резервная версия автоснимка.</summary>
        bool BackupExists { get; }

        /// <summary>
        /// Убирает повреждённый основной снимок из стартового пути, сохраняя
        /// его рядом для диагностики. Возвращает новый путь или null.
        /// </summary>
        string? QuarantineCorruptSnapshot();

        /// <summary>
        /// Перезапускает debounce. Фабрика вызывается на UI-контексте,
        /// запись готового JSON выполняется в фоне.
        /// </summary>
        void Schedule(Func<string> snapshotFactory);

        /// <summary>Удаляет снимок после подтверждённого сохранения/отказа от изменений.</summary>
        void Clear();

        /// <summary>Ждёт текущую запись; нужна проверкам и контролируемому завершению.</summary>
        Task WaitForPendingSaveAsync();
    }
}
