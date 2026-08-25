#nullable enable
using System;
using System.IO;
using System.Text;

namespace GCodeGenerator.Services
{
    public sealed class GCodeFileService : IGCodeFileService
    {
        // UTF-8 без BOM. Encoding.UTF8 ставит в начало файла преамбулу
        // EF BB BF — три байта перед первым кадром, которые часть стоек
        // не понимает: программа собрана из чистого ASCII, а сам файл
        // начинался не с него. Просто ASCII-кодировкой заменить нельзя:
        // имя операции пишет пользователь, продукт передаёт его как есть,
        // и не-ASCII имя превратилось бы в вопросительные знаки.
        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public void Save(string filePath, string gCode)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу G-code не задан.", nameof(filePath));

            File.WriteAllText(filePath, gCode ?? string.Empty, Utf8WithoutBom);
        }
    }
}
