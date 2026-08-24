using System.Collections.Generic;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Координатор DXF-импорта: отделяет синтаксический разбор сущностей от
    /// геометрического восстановления замкнутых областей.
    /// </summary>
    public sealed class DxfImportService : IDxfImportService
    {
        public List<DxfPolyline> ReadProfilePolylines(string path)
            => DxfEntityReader.Read(path, includePolylineEntities: false);

        public List<DxfPolyline> ReadPocketClosedContours(string path)
        {
            var entities = DxfEntityReader.Read(path, includePolylineEntities: true);
            return new DxfClosedContourBuilder().Build(entities);
        }
    }
}
