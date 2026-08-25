#nullable enable
namespace GCodeGenerator.Services
{
    /// <summary>Граница сохранения готового текстового G-code на диск.</summary>
    public interface IGCodeFileService
    {
        void Save(string filePath, string gCode);
    }
}
