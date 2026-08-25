#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GCodeGenerator.Diagnostics;
using GCodeGenerator.Models;
using GCodeGenerator.Persistence;

namespace GCodeGenerator.Infrastructure
{
    /// <summary>Что делать с необработанным исключением.</summary>
    public enum CrashResponse
    {
        /// <summary>
        /// Работу можно продолжить: сбой не затрагивает документ.
        /// </summary>
        Continue,

        /// <summary>
        /// Состояние программы неизвестно: сохранить аварийный снимок
        /// проекта и завершиться.
        /// </summary>
        Shutdown
    }

    /// <summary>
    /// Решение о судьбе программы после необработанного исключения и
    /// аварийное сохранение проекта.
    ///
    /// Прежде любое исключение в потоке интерфейса помечалось обработанным,
    /// и программа продолжала работу — «чтобы проект можно было сохранить».
    /// Замысел верный, исполнение опасное: после сбоя в глубине состояние
    /// документа неизвестно, а пользователь продолжает править и сохраняет
    /// поверх исходного файла то, что получилось. Сохранять надо, но не
    /// туда, куда сохраняет пользователь, и не ценой продолжения работы на
    /// повреждённой модели.
    ///
    /// Поэтому гасятся только те сбои, о которых заранее известно, что они
    /// не затрагивают документ: отменённая операция и отказ внешнего
    /// ресурса — буфера обмена, принтера, файла, до которого не достучаться.
    /// Всё остальное ведёт к снимку проекта рядом с журналом и
    /// контролируемому завершению.
    /// </summary>
    public sealed class CrashHandler
    {
        private readonly IProjectFileService _projectFileService;
        private readonly IAppLogger _logger;
        private readonly string _snapshotDirectory;

        public CrashHandler(IProjectFileService projectFileService, IAppLogger logger, string snapshotDirectory)
        {
            _projectFileService = projectFileService ?? throw new ArgumentNullException(nameof(projectFileService));
            _logger = logger ?? NullAppLogger.Instance;
            _snapshotDirectory = snapshotDirectory ?? throw new ArgumentNullException(nameof(snapshotDirectory));
        }

        /// <summary>
        /// Отказ внешнего ресурса и отмена операции документа не портят:
        /// файл не открылся, буфер обмена занят другой программой, задача
        /// снята — работать дальше можно.
        /// </summary>
        public static CrashResponse Classify(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is OperationCanceledException ||
                    current is IOException ||
                    current is UnauthorizedAccessException ||
                    current is System.Runtime.InteropServices.ExternalException)
                {
                    return CrashResponse.Continue;
                }

                // Составное исключение задачи продолжаемо, только если
                // продолжаемы все его причины.
                if (current is AggregateException aggregate)
                {
                    foreach (var inner in aggregate.InnerExceptions)
                    {
                        if (Classify(inner) == CrashResponse.Shutdown)
                            return CrashResponse.Shutdown;
                    }
                    return CrashResponse.Continue;
                }
            }

            return CrashResponse.Shutdown;
        }

        /// <summary>
        /// Сохраняет проект в отдельный файл рядом с журналом и возвращает
        /// путь к нему; <c>null</c> — сохранить не удалось или сохранять
        /// нечего. Само аварийное сохранение упасть не должно: оно и так
        /// выполняется после сбоя.
        /// </summary>
        /// <param name="operations">Операции документа на момент сбоя.</param>
        /// <param name="settings">Настройки генерации на момент сбоя.</param>
        /// <param name="timestamp">Метка времени в имени файла.</param>
        public string? TrySaveSnapshot(
            IReadOnlyList<OperationBase> operations, GCodeSettings settings, DateTime timestamp)
        {
            if (operations == null || operations.Count == 0)
                return null;

            try
            {
                Directory.CreateDirectory(_snapshotDirectory);
                var name = "crash-" + timestamp.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".ygc";
                var path = Path.Combine(_snapshotDirectory, name);
                _projectFileService.Save(path, operations, settings ?? new GCodeSettings());
                _logger.Info($"Аварийный снимок проекта: {path}");
                return path;
            }
            catch (Exception failure)
            {
                _logger.Error("Не удалось сохранить аварийный снимок проекта", failure);
                return null;
            }
        }
    }
}
