#nullable enable
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
    ///
    /// Правило распространяется и на имя операции — единственное, что
    /// попадало сюда от пользователя: русское имя в программу не выводится
    /// (см. <see cref="Operation"/>). Раньше оно уходило в файл как есть,
    /// и обещание «только ASCII» на деле выполнялось лишь для собственных
    /// текстов продукта.
    /// </summary>
    public static class ProgramComments
    {
        /// <summary>
        /// Заголовок операции: имя и краткое описание.
        ///
        /// Имя задаёт пользователь, и при русском интерфейсе оно русское.
        /// В программу такое имя не попадает: комментарий уходит в файл,
        /// который читает стойка, и кириллица в нём либо отвергается кадром,
        /// либо показывается мусором. Остаётся описание — оно английское,
        /// собрано продуктом и называет тип операции и её размеры, то есть
        /// говорит о ней то же самое.
        ///
        /// Переводить имя буквами латиницы пробовали: «Карман под подшипник»
        /// давало «Karman pod podshipnik» — формально ASCII, а читать глазами
        /// в листинге невозможно. Английское описание понятнее собственного
        /// имени, записанного чужими буквами.
        /// </summary>
        /// <param name="name">Имя операции, заданное пользователем.</param>
        /// <param name="description">Краткое описание операции.</param>
        public static string Operation(string name, string description)
            => IsAscii(name) ? $"{name}: {description}" : description;

        /// <summary>Состоит ли текст только из символов, которые примет любая стойка.</summary>
        private static bool IsAscii(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            foreach (var character in text!)
            {
                if (character > 127)
                    return false;
            }

            return true;
        }

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
