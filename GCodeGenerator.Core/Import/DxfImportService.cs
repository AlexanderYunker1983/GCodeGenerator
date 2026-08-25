using System.Collections.Generic;
using GCodeGenerator.Models;

namespace GCodeGenerator.Import
{
    /// <summary>
    /// Координатор DXF-импорта: отделяет разбор файла от геометрического
    /// восстановления замкнутых областей.
    ///
    /// Обе операции читают чертёж одинаково — полный набор геометрии,
    /// включая полилинии. Прежде профильный импорт исключал полилинии,
    /// из-за чего контур, нарисованный полилинией, просто не появлялся
    /// в операции, хотя описание продукта обещал его поддержку.
    /// </summary>
    public sealed class DxfImportService : IDxfImportService
    {
        public List<Polyline2D> ReadProfilePolylines(string path)
            => DxfEntityReader.Read(path);

        public List<Polyline2D> ReadPocketClosedContours(string path)
        {
            var entities = DxfEntityReader.Read(path);
            return new DxfClosedContourBuilder().Build(entities);
        }
    }
}
