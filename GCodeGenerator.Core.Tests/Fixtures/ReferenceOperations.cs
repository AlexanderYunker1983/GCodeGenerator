using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Models;

namespace GCodeGenerator.Tests.Fixtures
{
    /// <summary>
    /// Состав эталонного проекта (пункт 0.7 плана): все 19 операций фикстур 0.3
    /// (9 сверл + 6 профилей + 4 кармана — все 11 типов) в порядке каталога.
    /// <c>DxfFilePath</c> DXF-операций нормализован к именам ассетов (без машинного пути),
    /// чтобы эталонный .ygc оставался переносимым.
    /// Единственный источник истины для эталонного набора (ReferenceProjectTests)
    /// и тестов легаси-ридера (ProjectFileServiceTests, пункт 1.2).
    /// </summary>
    public static class ReferenceOperations
    {
        public static List<OperationBase> Build()
        {
            var ops = new List<OperationBase>
            {
                // Сверление (9)
                OperationFixtures.DrillPoints(),
                OperationFixtures.DrillLine(),
                OperationFixtures.DrillArray(),
                OperationFixtures.DrillRect(),
                OperationFixtures.DrillCircle(),
                OperationFixtures.DrillArc(),
                OperationFixtures.DrillPolygon(),
                OperationFixtures.DrillEllipse(),
                OperationFixtures.DrillPackage(),
                // Профили (6)
                OperationFixtures.ProfileRectangle(),
                OperationFixtures.ProfileRoundedRectangle(),
                OperationFixtures.ProfileCircle(),
                OperationFixtures.ProfileEllipse(),
                OperationFixtures.ProfilePolygon(),
                OperationFixtures.ProfileDxf(),
                // Карманы (4)
                OperationFixtures.PocketRectangle(),
                OperationFixtures.PocketCircle(),
                OperationFixtures.PocketEllipse(),
                OperationFixtures.PocketDxf()
            };

            foreach (var op in ops.OfType<ProfileDxfOperation>())
                op.DxfFilePath = "profile_sample.dxf";
            foreach (var op in ops.OfType<PocketDxfOperation>())
                op.DxfFilePath = "pocket_sample.dxf";

            return ops;
        }
    }
}
