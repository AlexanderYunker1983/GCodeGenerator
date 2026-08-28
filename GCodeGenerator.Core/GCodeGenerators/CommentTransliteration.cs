#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GCodeGenerator.GCodeGenerators
{
    /// <summary>
    /// Приведение текста комментария к латинице.
    ///
    /// Собственные тексты программы английские намеренно (см.
    /// <see cref="ProgramComments"/>), но имя операции задаёт пользователь, и
    /// при русском интерфейсе оно русское: «Сверление крепёжных отверстий».
    /// Файл программы читает стойка станка, а не человек — многие стойки
    /// принимают только ASCII и на кириллице либо отказываются выполнять кадр,
    /// либо показывают вместо комментария мусор. Хуже того, отказ приходит уже
    /// у станка: ни генерация, ни предпросмотр ничего необычного не замечают.
    ///
    /// Поэтому текст переводится в латиницу: смысл имени сохраняется, а кадр
    /// остаётся тем, что стойка заведомо прочитает. Символы, для которых
    /// замены нет, становятся вопросительным знаком — потерю лучше видеть,
    /// чем не заметить.
    ///
    /// Схема практическая, а не стандарт какой-либо страны: она пишет то, что
    /// оператор станка прочитает вслух и узнает. Мягкий и твёрдый знаки
    /// опускаются, «щ» становится «shch», а заглавные буквы, за которыми идут
    /// заглавные же, дают «SHAG», а не «ShAG».
    /// </summary>
    public static class CommentTransliteration
    {
        /// <summary>Замены для кириллицы; регистр восстанавливается отдельно.</summary>
        private static readonly Dictionary<char, string> Cyrillic = new Dictionary<char, string>
        {
            ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d",
            ['е'] = "e", ['ё'] = "e", ['ж'] = "zh", ['з'] = "z", ['и'] = "i",
            ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n",
            ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t",
            ['у'] = "u", ['ф'] = "f", ['х'] = "kh", ['ц'] = "ts", ['ч'] = "ch",
            ['ш'] = "sh", ['щ'] = "shch", ['ъ'] = "", ['ы'] = "y", ['ь'] = "",
            ['э'] = "e", ['ю'] = "yu", ['я'] = "ya",
            // Буквы, которых нет в русском алфавите, но которые приходят
            // с той же клавиатуры: украинские, белорусские, болгарские имена
            // операций иначе целиком превратились бы в вопросительные знаки.
            ['і'] = "i", ['ї'] = "yi", ['є'] = "ye", ['ґ'] = "g", ['ў'] = "u",
        };

        /// <summary>
        /// Знаки, которые станочник пишет в имени операции чаще, чем любую
        /// нерусскую букву: диаметр, градус, номер, размер «сорок на двадцать».
        /// Без них «Отверстие Ø8 под 45°» превратилось бы в строку с двумя
        /// вопросительными знаками ровно там, где стоит самое важное.
        /// Регистр здесь не участвует — это не буквы.
        /// </summary>
        private static readonly Dictionary<char, string> Symbols = new Dictionary<char, string>
        {
            ['Ø'] = "D", ['ø'] = "d", ['⌀'] = "D",
            ['°'] = "deg", ['№'] = "No", ['×'] = "x", ['±'] = "+/-", ['µ'] = "u", ['μ'] = "u",
            ['«'] = "\"", ['»'] = "\"", ['„'] = "\"", ['“'] = "\"", ['”'] = "\"",
            ['‘'] = "'", ['’'] = "'", ['—'] = "-", ['–'] = "-", ['−'] = "-", ['…'] = "...",
            [' '] = " ",
        };

        /// <summary>
        /// Латиница, равная исходному тексту по смыслу. Пустой текст остаётся
        /// пустым.
        /// </summary>
        /// <param name="text">Текст комментария.</param>
        public static string ToAscii(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var result = new StringBuilder(text!.Length);
            for (var index = 0; index < text.Length; index++)
            {
                var character = text[index];
                if (character < 128)
                {
                    result.Append(character);
                    continue;
                }

                if (Symbols.TryGetValue(character, out var symbol))
                {
                    result.Append(symbol);
                    continue;
                }

                var neighbourIsUpper = IsUpperAt(text, index - 1) || IsUpperAt(text, index + 1);
                var lower = char.ToLowerInvariant(character);
                if (Cyrillic.TryGetValue(lower, out var replacement))
                {
                    Append(result, replacement, character, neighbourIsUpper);
                    continue;
                }

                Append(result, WithoutDiacritics(character), character, neighbourIsUpper);
            }

            return result.ToString();
        }

        /// <summary>Заглавная ли буква на этом месте; за краями строки — нет.</summary>
        private static bool IsUpperAt(string text, int index)
            => index >= 0 && index < text.Length && char.IsUpper(text[index]);

        /// <summary>
        /// Дописывает замену в регистре исходной буквы. Многобуквенная замена
        /// заглавной буквы пишется целиком заглавными, если соседняя буква
        /// тоже заглавная: «Шаг» даёт «Shag», «ШАГ» — «SHAG». Смотреть только
        /// вперёд нельзя — у последней буквы слова следующий символ пробел,
        /// и «ЧЕРНОВАЯ» превращалась в «CHERNOVAYa».
        /// </summary>
        /// <param name="result">Собираемый текст.</param>
        /// <param name="replacement">Замена в нижнем регистре.</param>
        /// <param name="original">Исходный символ — из него берётся регистр.</param>
        /// <param name="neighbourIsUpper">Соседняя буква тоже заглавная.</param>
        private static void Append(
            StringBuilder result, string replacement, char original, bool neighbourIsUpper)
        {
            if (replacement.Length == 0)
                return;

            if (!char.IsUpper(original))
            {
                result.Append(replacement);
                return;
            }

            if (neighbourIsUpper)
            {
                result.Append(replacement.ToUpperInvariant());
                return;
            }

            result.Append(char.ToUpperInvariant(replacement[0]));
            if (replacement.Length > 1)
                result.Append(replacement, 1, replacement.Length - 1);
        }

        /// <summary>
        /// Буква без диакритики, если она у неё есть: «ü» становится «u»,
        /// «é» — «e». Так проходят немецкие, французские и польские имена,
        /// для которых отдельной таблицы нет. Всё остальное — иероглифы,
        /// знаки валют, стрелки — заменяется вопросительным знаком.
        /// </summary>
        /// <param name="character">Исходный символ.</param>
        private static string WithoutDiacritics(char character)
        {
            var decomposed = character.ToString().Normalize(NormalizationForm.FormD);
            var stripped = new StringBuilder(decomposed.Length);
            foreach (var part in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(part) == UnicodeCategory.NonSpacingMark)
                    continue;
                if (part >= 128)
                    return "?";

                stripped.Append(part);
            }

            return stripped.Length > 0 ? stripped.ToString().ToLowerInvariant() : "?";
        }
    }
}
