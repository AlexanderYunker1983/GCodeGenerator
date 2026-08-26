using System.Collections.Generic;
using GCodeGenerator.Models;

namespace GCodeGenerator.Tests.Fixtures
{
    /// <summary>
    /// Каталог фикстур (пункт 0.3 плана):
    /// 9 видов сверления + 6 профилей + 4 кармана (по Default-настройкам),
    /// перекрёстные варианты — многооперационная программа и варианты настроек
    /// (линейные номера, padded G, AllowArcs, шпиндель/СОЖ, WCS, G92-старт/финиш), —
    /// а также стратегии выборки карманов и черновая/чистовая обработка.
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
                cases.Add(new FixtureCase("Profile.Circle.AngledEntry", Ops(OperationFixtures.ProfileCircleAngledEntry()), SettingsFixtures.Default()));

                // Режимы траектории: у Default-фикстур режим OnLine, смещения
                // нет, и наружная с внутренней эквидистантами прежде не имели
                // эталонов вовсе — дефект эквидистант многоугольника и эллипса
                // жил в невидимой для эталонов зоне.
                cases.Add(new FixtureCase("Profile.Polygon.Outside", Ops(WithToolPathMode(OperationFixtures.ProfilePolygon(), ToolPathMode.Outside)), SettingsFixtures.Default()));
                cases.Add(new FixtureCase("Profile.Polygon.Inside", Ops(WithToolPathMode(OperationFixtures.ProfilePolygon(), ToolPathMode.Inside)), SettingsFixtures.Default()));
                cases.Add(new FixtureCase("Profile.Ellipse.Outside", Ops(WithToolPathMode(OperationFixtures.ProfileEllipse(), ToolPathMode.Outside)), SettingsFixtures.Default()));
                cases.Add(new FixtureCase("Profile.Ellipse.Inside", Ops(WithToolPathMode(OperationFixtures.ProfileEllipse(), ToolPathMode.Inside)), SettingsFixtures.Default()));
                cases.Add(new FixtureCase("Profile.Dxf.Outside", Ops(WithToolPathMode(OperationFixtures.ProfileDxf(), ToolPathMode.Outside)), SettingsFixtures.Default()));
                cases.Add(new FixtureCase("Profile.Dxf.Inside", Ops(WithToolPathMode(OperationFixtures.ProfileDxf(), ToolPathMode.Inside)), SettingsFixtures.Default()));

                // Карманы: 4 вида.
                cases.Add(new FixtureCase("Pocket.Rectangle.Default", Ops(OperationFixtures.PocketRectangle()), SettingsFixtures.Default()));
                cases.Add(new FixtureCase("Pocket.Circle.Default", Ops(OperationFixtures.PocketCircle()), SettingsFixtures.Default()));
                cases.Add(new FixtureCase("Pocket.Ellipse.Default", Ops(OperationFixtures.PocketEllipse()), SettingsFixtures.Default()));
                cases.Add(new FixtureCase("Pocket.Dxf.Default", Ops(OperationFixtures.PocketDxf()), SettingsFixtures.Default()));

                // Стратегии выборки: Default-фикстуры работают спиралью, и
                // точный G-код остальных четырёх стратегий эталоны прежде
                // не фиксировали — регрессия в порядке проходов или линковке
                // резов проходила бы мимо поведенческих инвариантов.
                foreach (var strategy in new[]
                {
                    PocketStrategy.Concentric,
                    PocketStrategy.Radial,
                    PocketStrategy.ZigZag,
                    PocketStrategy.Lines,
                })
                {
                    cases.Add(new FixtureCase($"Pocket.Circle.{strategy}", Ops(WithStrategy(OperationFixtures.PocketCircle(), strategy)), SettingsFixtures.Default()));
                    cases.Add(new FixtureCase($"Pocket.Rectangle.{strategy}", Ops(WithStrategy(OperationFixtures.PocketRectangle(), strategy)), SettingsFixtures.Default()));
                    cases.Add(new FixtureCase($"Pocket.Ellipse.{strategy}", Ops(WithStrategy(OperationFixtures.PocketEllipse(), strategy)), SettingsFixtures.Default()));
                    cases.Add(new FixtureCase($"Pocket.Dxf.{strategy}", Ops(WithStrategy(OperationFixtures.PocketDxf(), strategy)), SettingsFixtures.Default()));
                }

                // Черновая и чистовая обработка: припуск и его снятие тоже
                // не имели точных эталонов — только поведенческие проверки.
                cases.Add(new FixtureCase("Pocket.Circle.RoughFinishAll",
                    Ops(WithFinishing(OperationFixtures.PocketCircle(), roughing: true, PocketFinishingMode.All)), SettingsFixtures.Default()));
                cases.Add(new FixtureCase("Pocket.Circle.FinishWalls",
                    Ops(WithFinishing(OperationFixtures.PocketCircle(), roughing: false, PocketFinishingMode.Walls)), SettingsFixtures.Default()));
                cases.Add(new FixtureCase("Pocket.Circle.FinishBottom",
                    Ops(WithFinishing(OperationFixtures.PocketCircle(), roughing: false, PocketFinishingMode.Bottom)), SettingsFixtures.Default()));

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

        private static OperationBase WithStrategy(PocketOperationBase operation, PocketStrategy strategy)
        {
            operation.PocketStrategy = strategy;
            return operation;
        }

        private static OperationBase WithToolPathMode(ProfileOperationBase operation, ToolPathMode mode)
        {
            operation.ToolPathMode = mode;
            return operation;
        }

        private static OperationBase WithFinishing(
            PocketOperationBase operation, bool roughing, PocketFinishingMode mode)
        {
            operation.IsRoughingEnabled = roughing;
            operation.IsFinishingEnabled = true;
            operation.FinishingMode = mode;
            return operation;
        }
    }
}
