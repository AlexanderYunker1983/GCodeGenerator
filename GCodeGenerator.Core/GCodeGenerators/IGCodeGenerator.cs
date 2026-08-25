using System;
using System.Collections.Generic;
using System.Threading;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    public interface IGCodeGenerator
    {
        /// <summary>
        /// Генерирует G-код-программу. Пункт 8.4 плана: <paramref name="progress"/>
        /// — необязательное сообщение о прогрессе (0–100, по операциям); метод остаётся
        /// чистым синхронным (асинхронность — на стороне UI, Task.Run).
        /// </summary>
        GCodeProgram Generate(
            IList<OperationBase> operations,
            GCodeSettings settings,
            IProgress<int> progress = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Строит только траекторию инструмента — то, что проделает станок,
        /// без единого G-слова. Нужна предпросмотру: он показывает движение
        /// инструмента, а не текст программы, и раньше вынужден был получать
        /// его обратным разбором уже готового G-code.
        /// </summary>
        /// <param name="operations">Операции документа.</param>
        /// <param name="settings">Настройки генерации.</param>
        /// <param name="progress">Сообщение о прогрессе (0–100, по операциям).</param>
        /// <param name="cancellation">
        /// Отмена: проверяется между операциями. Программа большого проекта
        /// строится заметное время, и её незачем достраивать, если документ
        /// уже изменился или пользователь закрывает окно.
        /// </param>
        Toolpath.ToolPath BuildToolPath(
            IList<OperationBase> operations,
            GCodeSettings settings,
            IProgress<int> progress = null,
            CancellationToken cancellation = default);
    }
}
