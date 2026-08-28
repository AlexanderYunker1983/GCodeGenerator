#nullable enable
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Версия продукта в том виде, в каком её задают теги: X.Y.Z и
    /// необязательный суффикс — 1.2.3, 1.2.3-rc5, 1.2.3-alpha.
    ///
    /// Разбор нужен ровно для одного вопроса: та версия, что лежит на
    /// странице выпусков, новее установленной или нет. Сравнивать строки
    /// нельзя — «1.10.0» лексикографически меньше «1.9.0», а «1.2.3-rc5»
    /// длиннее «1.2.3» и при этом старее его.
    ///
    /// Порядок суффиксов тот же, что у build/Get-GitVersion.ps1, который
    /// выбирает тег при сборке: 1.2.3 &gt; 1.2.3-rc5 &gt; 1.2.3-beta3 &gt;
    /// 1.2.3-alpha2 &gt; 1.2.3-alpha. Расходиться этим двум местам нельзя:
    /// одно решает, какой версией назваться, второе — какая новее.
    /// </summary>
    public sealed class ProductVersion : IComparable<ProductVersion>
    {
        /// <summary>Формат тега: три числа и необязательный буквенный суффикс.</summary>
        private static readonly Regex Format = new Regex(
            @"^(\d+)\.(\d+)\.(\d+)(?:-([A-Za-z][A-Za-z0-9]*))?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>Суффикс на классы и номер: «rc5» — класс «rc», номер 5.</summary>
        private static readonly Regex SuffixFormat = new Regex(
            @"^([A-Za-z]+)(\d*)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>Выпуск без суффикса — старше любого предвыпуска той же версии.</summary>
        private const int ReleaseRank = 4;

        private ProductVersion(string text, int major, int minor, int patch, int classRank, int number)
        {
            Text = text;
            Major = major;
            Minor = minor;
            Patch = patch;
            ClassRank = classRank;
            Number = number;
        }

        /// <summary>Версия так, как она записана в теге.</summary>
        public string Text { get; }

        public int Major { get; }

        public int Minor { get; }

        public int Patch { get; }

        /// <summary>
        /// Старшинство суффикса: выпуск — 4, rc — 3, beta — 2, alpha — 1,
        /// незнакомый суффикс — 0. Незнакомый считается самым младшим:
        /// предложить обновиться на то, о чём ничего не известно, хуже,
        /// чем промолчать.
        /// </summary>
        public int ClassRank { get; }

        /// <summary>Номер внутри класса: rc10 новее rc5.</summary>
        public int Number { get; }

        /// <summary>
        /// Разбирает версию; <c>null</c> — строка не похожа на тег.
        ///
        /// Ведущая «v» отбрасывается: страница выпусков GitHub часто
        /// показывает тег именно так, хотя сам продукт такие теги не создаёт.
        /// </summary>
        /// <param name="text">Строка версии или тега.</param>
        public static ProductVersion? Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var trimmed = text!.Trim();
            if (trimmed.Length > 1 && (trimmed[0] == 'v' || trimmed[0] == 'V'))
                trimmed = trimmed.Substring(1);

            var match = Format.Match(trimmed);
            if (!match.Success)
                return null;

            var major = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var minor = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            var patch = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);

            var suffix = match.Groups[4].Value;
            if (suffix.Length == 0)
                return new ProductVersion(trimmed, major, minor, patch, ReleaseRank, 0);

            var parts = SuffixFormat.Match(suffix);
            var className = parts.Groups[1].Value.ToLowerInvariant();
            var number = parts.Groups[2].Value.Length > 0
                ? int.Parse(parts.Groups[2].Value, CultureInfo.InvariantCulture)
                : 0;

            var classRank = className switch
            {
                "alpha" => 1,
                "beta" => 2,
                "rc" => 3,
                _ => 0
            };

            return new ProductVersion(trimmed, major, minor, patch, classRank, number);
        }

        /// <inheritdoc />
        public int CompareTo(ProductVersion? other)
        {
            if (other == null)
                return 1;

            var byMajor = Major.CompareTo(other.Major);
            if (byMajor != 0) return byMajor;

            var byMinor = Minor.CompareTo(other.Minor);
            if (byMinor != 0) return byMinor;

            var byPatch = Patch.CompareTo(other.Patch);
            if (byPatch != 0) return byPatch;

            var byClass = ClassRank.CompareTo(other.ClassRank);
            if (byClass != 0) return byClass;

            return Number.CompareTo(other.Number);
        }

        /// <summary>Эта версия новее указанной.</summary>
        /// <param name="other">С чем сравнивать; null — сравнивать не с чем.</param>
        public bool IsNewerThan(ProductVersion? other) => other != null && CompareTo(other) > 0;

        /// <inheritdoc />
        public override string ToString() => Text;
    }
}
