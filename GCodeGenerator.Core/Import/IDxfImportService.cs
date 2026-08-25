using System.Collections.Generic;
using GCodeGenerator.Models;

namespace GCodeGenerator.Import
{
    /// <summary>
    /// Чистая граница импорта DXF. ViewModel отвечает только за выбор файла и
    /// обновление UI, а чтение формата и восстановление контуров выполняет сервис.
    /// </summary>
    public interface IDxfImportService
    {
        /// <summary>Читает геометрию профильной обработки.</summary>
        List<Polyline2D> ReadProfilePolylines(string path);

        /// <summary>Читает и восстанавливает замкнутые контуры кармана.</summary>
        List<Polyline2D> ReadPocketClosedContours(string path);
    }
}
