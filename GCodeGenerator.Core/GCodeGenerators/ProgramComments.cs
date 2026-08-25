namespace GCodeGenerator.GCodeGenerators
{
    /// <summary>
    /// Тексты комментариев, попадающих в программу.
    ///
    /// Прежде они собирались строками прямо в генераторах и планировщике:
    /// «Pass 3, depth −1.500», «Contour too small for tool, stopping»,
    /// «Pocket too small after roughing allowance, skipping». Изменить
    /// формулировку или добавить к ней данные значило править логику, а
    /// увидеть весь набор было негде.
    ///
    /// Тексты остаются английскими намеренно, хотя перевод в ядре есть.
    /// Комментарий уходит не пользователю программы, а в файл, который
    /// читает стойка станка: многие стойки принимают только ASCII и на
    /// кириллице отказываются выполнять кадр или искажают его. Язык
    /// интерфейса на содержимое программы влиять не должен — иначе одна и
    /// та же операция давала бы разные файлы на разных машинах.
    /// </summary>
    public static class ProgramComments
    {
        /// <summary>Заголовок операции: имя и краткое описание.</summary>
        /// <param name="name">Имя операции, заданное пользователем.</param>
        /// <param name="description">Краткое описание операции.</param>
        public static string Operation(string name, string description)
            => $"{name}: {description}";

        /// <summary>Проход по глубине: номер и достигаемая высота.</summary>
        /// <param name="number">Номер прохода, начиная с единицы.</param>
        /// <param name="depth">Высота прохода в том же виде, в каком она попадёт в кадр.</param>
        public static string Pass(int number, string depth)
            => $"Pass {number}, depth {depth}";

        /// <summary>Контур стал уже инструмента — обработка прекращена.</summary>
        public const string ContourTooSmall = "Contour too small for tool, stopping";

        /// <summary>Карман исчез под припуском — обрабатывать нечего.</summary>
        public const string PocketTooSmallForAllowance = "Pocket too small after roughing allowance, skipping";
    }
}
