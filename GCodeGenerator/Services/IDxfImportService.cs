using System.Collections.Generic;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Чистая граница импорта DXF. ViewModel отвечает только за выбор файла и
    /// обновление UI, а чтение формата и восстановление контуров выполняет сервис.
    /// </summary>
    public interface IDxfImportService
    {
        /// <summary>Читает геометрию профильной обработки.</summary>
        List<DxfPolyline> ReadProfilePolylines(string path);

        /// <summary>Читает и восстанавливает замкнутые контуры кармана.</summary>
        List<DxfPolyline> ReadPocketClosedContours(string path);
    }
}
