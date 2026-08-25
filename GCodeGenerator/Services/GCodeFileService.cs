#nullable enable
using System;
using System.IO;
using System.Text;

namespace GCodeGenerator.Services
{
    public sealed class GCodeFileService : IGCodeFileService
    {
        public void Save(string filePath, string gCode)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу G-code не задан.", nameof(filePath));

            File.WriteAllText(filePath, gCode ?? string.Empty, Encoding.UTF8);
        }
    }
}
