#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Отказ ядра, адресованный пользователю: код для перевода на границе
    /// интерфейса плюс нейтральное английское сообщение для журнала.
    ///
    /// Схема повторяет <see cref="ValidationIssue"/>: ядро не знает языка
    /// окна, поэтому несёт код и аргументы, а перевод подставляет интерфейс.
    /// Прежде исключения персистентности и импорта были захардкожены
    /// по-русски и показывались как есть — пользователь с английским
    /// интерфейсом получал русский текст, а перевести его было нельзя
    /// без правки ядра.
    /// </summary>
    public class CoreException : Exception
    {
        /// <summary>
        /// Создаёт отказ с кодом для перевода.
        /// </summary>
        /// <param name="code">Код отказа: по нему интерфейс подбирает перевод
        /// (ключ словаря — «CoreError_» плюс код).</param>
        /// <param name="neutralMessage">Нейтральное английское сообщение —
        /// шаблон с местами подстановки для журнала и запасного вывода.</param>
        /// <param name="arguments">Аргументы шаблона; они же подставляются
        /// в перевод.</param>
        public CoreException(string code, string neutralMessage, params object[] arguments)
            : base(string.Format(CultureInfo.InvariantCulture, neutralMessage, arguments))
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Arguments = arguments;
        }

        /// <summary>Код отказа для словаря переводов.</summary>
        public string Code { get; }

        /// <summary>Аргументы для подстановки в переведённый шаблон.</summary>
        public IReadOnlyList<object> Arguments { get; }
    }

    /// <summary>Коды отказов ядра — единый перечень для ядра и словаря интерфейса.</summary>
    public static class CoreErrorCodes
    {
        /// <summary>Файл проекта повреждён или имеет неожиданную структуру.</summary>
        public const string ProjectFileCorrupt = "ProjectFileCorrupt";

        /// <summary>Версия формата файла проекта не поддерживается.</summary>
        public const string ProjectFileUnsupportedVersion = "ProjectFileUnsupportedVersion";

        /// <summary>Файл первого формата: читается только прежними сборками.</summary>
        public const string ProjectFileLegacyVersion = "ProjectFileLegacyVersion";

        /// <summary>В файле проекта неизвестная секция — файл новее программы.</summary>
        public const string ProjectFileUnknownSection = "ProjectFileUnknownSection";

        /// <summary>Тип операции из файла не поддерживается этой сборкой.</summary>
        public const string ProjectFileUnknownOperationType = "ProjectFileUnknownOperationType";

        /// <summary>Файл проекта превышает безопасный предел размера.</summary>
        public const string ProjectFileTooLarge = "ProjectFileTooLarge";

        /// <summary>Проект содержит больше операций, чем можно безопасно обработать.</summary>
        public const string ProjectFileTooComplex = "ProjectFileTooComplex";

        /// <summary>Файл не является чертежом DXF.</summary>
        public const string DxfNotADrawing = "DxfNotADrawing";

        /// <summary>В чертеже не заданы линейные единицы.</summary>
        public const string DxfUnitsNotSpecified = "DxfUnitsNotSpecified";

        /// <summary>Файл DXF превышает безопасный предел размера.</summary>
        public const string DxfFileTooLarge = "DxfFileTooLarge";

        /// <summary>Чертёж слишком сложен для поиска замкнутых контуров.</summary>
        public const string DxfTooComplex = "DxfTooComplex";

        /// <summary>Плоскость дуги или эллипса DXF не поддерживается импортом.</summary>
        public const string DxfUnsupportedCurvePlane = "DxfUnsupportedCurvePlane";

        /// <summary>Инструмент не помещается внутри эллипса.</summary>
        public const string EllipseToolDoesNotFit = "EllipseToolDoesNotFit";

        /// <summary>Окружность винтового подвода не помещается в кармане.</summary>
        public const string HelicalEntryDoesNotFit = "HelicalEntryDoesNotFit";
    }
}
