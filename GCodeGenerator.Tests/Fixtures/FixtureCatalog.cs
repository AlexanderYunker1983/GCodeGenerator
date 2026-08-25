using System.Collections.Generic;
using GCodeGenerator.Models;

namespace GCodeGenerator.Tests.Fixtures
{
    /// <summary>
    /// Каталог фикстур (пункт 0.3 плана):
    /// 9 видов сверления + 6 профилей + 4 кармана (по Default-настройкам)
    /// и перекрёстные варианты: многооперационная программа и варианты настроек
    /// (линейные номера, padded G, AllowArcs, шпиндель/СОЖ, WCS, G92-старт/финиш).
    /// </summary>
    public static class FixtureCatalog
    {
        public static IReadOnlyList<FixtureCase> All
        {
            get
            {
                var cases = new List<FixtureCase>();

                // Сверление: 9 видов.
                cases.Add(new FixtureCase("Drill.Points.Default", Ops(OperationFixtures.DrillPoints()), SettingsFixtures.Default()));
                cases.Add(new FixtureCase("Drill.Line.Default", Ops(OperationFixtures.DrillLine()), SettingsFixtures.Default()));
                cases.Add(new FixtureCase("Drill.Array.Default", Ops(OperationFixtures.DrillArray()), SettingsFixtures.Default()));
                cases.Add(new FixtureCase("Drill.Rect.Default", Ops(OperationFixtures.DrillRect()), SettingsFixtures.Default()));
                cases.Add(new FixtureCase("Drill.Circle.Default", Ops(OperationFixtures.DrillCircle()), SettingsFixtures.Default()));
                cases.Add(new FixtureCase("Drill.Arc.Default", Ops(OperationFixtures.DrillArc()), SettingsFixtures.Default()));
                cases.Add(new FixtureCase("Drill.Polygon.Default", Ops(OperationFixtures.DrillPolygon()), SettingsFixtures.Default()));
                cases.Add(new FixtureCase("Drill.Ellipse.Default", Ops(OperationFixtures.DrillEllipse()), SettingsFixtures.Default()));
                cases.Add(new FixtureCase("Drill.Package.Default", Ops(OperationFixtures.DrillPackage()), SettingsFixtures.Default()));

                // Профили: 6 видов.
                cases.Add(new FixtureCase("Profile.Rectangle.Default", Ops(OperationFixtures.ProfileRectangle()), SettingsFixtures.Default()));
                cases.Add(new FixtureCase("Profile.RoundedRectangle.Default", Ops(OperationFixtures.ProfileRoundedRectangle()), SettingsFixtures.Default()));
                cases.Add(new FixtureCase("Profile.Circle.Default", Ops(OperationFixtures.ProfileCircle()), SettingsFixtures.Default()));
                cases.Add(new FixtureCase("Profile.Ellipse.Default", Ops(OperationFixtures.ProfileEllipse()), SettingsFixtures.Default()));
                cases.Add(new FixtureCase("Profile.Polygon.Default", Ops(OperationFixtures.ProfilePolygon()), SettingsFixtures.Default()));
                cases.Add(new FixtureCase("Profile.Dxf.Default", Ops(OperationFixtures.ProfileDxf()), SettingsFixtures.Default()));

                // Карманы: 4 вида.
                cases.Add(new FixtureCase("Pocket.Rectangle.Default", Ops(OperationFixtures.PocketRectangle()), SettingsFixtures.Default()));
                cases.Add(new FixtureCase("Pocket.Circle.Default", Ops(OperationFixtures.PocketCircle()), SettingsFixtures.Default()));
                cases.Add(new FixtureCase("Pocket.Ellipse.Default", Ops(OperationFixtures.PocketEllipse()), SettingsFixtures.Default()));
                cases.Add(new FixtureCase("Pocket.Dxf.Default", Ops(OperationFixtures.PocketDxf()), SettingsFixtures.Default()));

                // Перекрёстные варианты.
                cases.Add(new FixtureCase("Multi.Operation.Default", MultiOperation(), SettingsFixtures.Default()));
                cases.Add(new FixtureCase("Multi.Operation.WcsG55", MultiOperation(), SettingsFixtures.WcsG55()));
                cases.Add(new FixtureCase("Multi.Operation.G92StartEnd", MultiOperation(), SettingsFixtures.G92StartEnd()));
                cases.Add(new FixtureCase("Drill.Points.NoLineNumbers", Ops(OperationFixtures.DrillPoints()), SettingsFixtures.NoLineNumbers()));
                cases.Add(new FixtureCase("Drill.Points.PaddedGCodes", Ops(OperationFixtures.DrillPoints()), SettingsFixtures.PaddedGCodes()));
                cases.Add(new FixtureCase("Drill.Points.SpindleCoolantOff", Ops(OperationFixtures.DrillPoints()), SettingsFixtures.SpindleCoolantOff()));
                cases.Add(new FixtureCase("Drill.Points.SpindleDelay", Ops(OperationFixtures.DrillPoints()), SettingsFixtures.SpindleDelay()));
                cases.Add(new FixtureCase("Profile.Circle.ArcsOff", Ops(OperationFixtures.ProfileCircle()), SettingsFixtures.ArcsOff()));
                cases.Add(new FixtureCase("Profile.Dxf.ArcsOff", Ops(OperationFixtures.ProfileDxf()), SettingsFixtures.ArcsOff()));
                cases.Add(new FixtureCase("Pocket.Circle.ArcsOff", Ops(OperationFixtures.PocketCircle()), SettingsFixtures.ArcsOff()));
                cases.Add(new FixtureCase("Pocket.Dxf.ArcsOff", Ops(OperationFixtures.PocketDxf()), SettingsFixtures.ArcsOff()));

                return cases;
            }
        }

        /// <summary>Сверление + профиль + карман в одной программе (порядок операций значим).</summary>
        private static List<OperationBase> MultiOperation()
        {
            return new List<OperationBase>
            {
                OperationFixtures.DrillLine(),
                OperationFixtures.ProfileCircle(),
                OperationFixtures.PocketRectangle()
            };
        }

        private static List<OperationBase> Ops(OperationBase operation)
        {
            return new List<OperationBase> { operation };
        }
    }
}
