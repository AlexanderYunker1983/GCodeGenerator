using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.GCodeGenerators.Interfaces;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using GCodeGenerator.Tests.Fixtures;
using GCodeGenerator.Trajectory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Дифференциальный тест сцены траектории (пункт 6.4 плана): для одной
    /// и той же программы <see cref="SceneBuilder"/> (структурный парсер
    /// блоков GCodeProgram) и <see cref="LegacyPreviewParser"/> (копия
    /// старого текстового парсера из PreviewViewModel) должны давать
    /// одинаковые сцены: тип перемещения, начала/концы, центр и радиус дуг,
    /// точки интерполяции.
    ///
    /// Допуск 1e-3 мм: текстовый парсер видит координаты, округлённые
    /// форматтером до Decimals (у операций по умолчанию 3 знака →
    /// ошибка округления ≤ 5e-4), структурный — полную точность блоков.
    ///
    /// Осознанные отличия (см. документацию LegacyPreviewParser): обе
    /// стороны содержат фикс G92 (позиция без сегмента) и фикс «утечки»
    /// координат из комментариев с N-префиксом, поэтому тест изолирует
    /// именно «текст против структуры».
    ///
    /// Фантомные сегменты на текстовой стороне: перемещение в полной
    /// точности ≤ 0.0001 (для SceneBuilder это не движение) после округления
    /// текста может выглядеть как движение до 0.0001 + 10^-Decimals
    /// (при 3 знаках — 0.001). Такие «округленные» сегменты текстового
    /// парсера пропускать при выравнивании допустимо: они не существуют
    /// в полной точности, и структурный парсер прав.
    /// </summary>
    [TestClass]
    public class SceneDifferentialTests
    {
        private static readonly SimpleGCodeGenerator Generator = new SimpleGCodeGenerator();
        private static readonly ProjectFileService Service = new ProjectFileService();
        private static CultureInfo _originalCulture;

        /// <summary>Допуск сравнения точек (округление текста до 3 знаков).</summary>
        private const double Tolerance = 1e-3;

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            _originalCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            CultureInfo.CurrentCulture = _originalCulture;
        }

        /// <summary>
        /// Все 31 фикстура: сцены из структуры и из текста совпадают.
        /// Покрывает дуги (Profile.Circle — G2/G3 с I/J), G92-фикстуру,
        /// padded G, линейные номера, M3/M4/M5/M8/M9, WCS.
        /// </summary>
        [TestMethod]
        public void AllFixtures_SceneBuilder_Equals_LegacyTextParser()
        {
            var failures = new List<string>();

            foreach (var fixture in FixtureCatalog.All)
            {
                var program = Generator.Generate(fixture.Operations, fixture.Settings);
                failures.AddRange(Compare(fixture.Name, program));
            }

            if (failures.Count > 0)
                Assert.Fail($"Дифференциальных несоответствий сцены: {failures.Count}\n\n{string.Join("\n\n", failures)}");
        }

        /// <summary>
        /// Эталонный проект (19 операций, полный пайплайн через .ygc):
        /// сцены из структуры и из текста совпадают.
        /// </summary>
        [TestMethod]
        public void ReferenceProject_SceneBuilder_Equals_LegacyTextParser()
        {
            var ygcPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reference", "reference_project.ygc");
            Assert.IsTrue(File.Exists(ygcPath), "Нет эталонного проекта reference_project.ygc");

            var operations = Service.Load(ygcPath).Operations;
            var program = Generator.Generate(operations, new GCodeSettings());

            var failures = Compare("reference_project", program).ToList();
            if (failures.Count > 0)
                Assert.Fail($"Дифференциальных несоответствий сцены: {failures.Count}\n\n{string.Join("\n\n", failures)}");
        }

        /// <summary>
        /// Программы фазы 5 (стратегии карманов, новая функциональность —
        /// golden для них нет): 5 стратегий × 4 типа кармана + варианты
        /// roughing/finishing. Сцены из структуры и из текста совпадают.
        /// </summary>
        [TestMethod]
        public void Phase5StrategyPrograms_SceneBuilder_Equals_LegacyTextParser()
        {
            var strategies = new[]
            {
                PocketStrategy.Concentric,
                PocketStrategy.Spiral,
                PocketStrategy.Radial,
                PocketStrategy.ZigZag,
                PocketStrategy.Lines,
            };

            var failures = new List<string>();

            // Карманы фазы 5 генерируются с Decimals = 6 (см. PocketProgram).
            const int Decimals = 6;

            foreach (var strategy in strategies)
            {
                failures.AddRange(Compare($"Pocket.Circle.{strategy}", PocketProgram(Circle(), strategy), Decimals));
                failures.AddRange(Compare($"Pocket.Rectangle.{strategy}", PocketProgram(Rectangle(), strategy), Decimals));
                failures.AddRange(Compare($"Pocket.Ellipse.{strategy}", PocketProgram(Ellipse(), strategy), Decimals));
                failures.AddRange(Compare($"Pocket.Dxf.{strategy}", PocketProgram(Dxf(), strategy), Decimals));
            }

            // Варианты roughing/finishing (фаза 5).
            failures.AddRange(Compare(
                "Pocket.Circle.Concentric.RoughFinish.Walls",
                PocketProgram(Circle(), PocketStrategy.Concentric,
                    roughing: true, finishing: true, mode: PocketFinishingMode.Walls), Decimals));
            failures.AddRange(Compare(
                "Pocket.Circle.ZigZag.FinishOnly",
                PocketProgram(Circle(), PocketStrategy.ZigZag, finishing: true), Decimals));

            if (failures.Count > 0)
                Assert.Fail($"Дифференциальных несоответствий сцены: {failures.Count}\n\n{string.Join("\n\n", failures)}");
        }

        // ------------------------------------------------------------------
        // Сравнение сцен
        // ------------------------------------------------------------------

        /// <summary>
        /// Выравнивает сцену SceneBuilder (структура) и сцену
        /// LegacyPreviewParser (текст той же программы) двумя указателями:
        /// совпавшие сегменты сдвигают оба указателя, фантомный
        /// «округленный» сегмент текстовой стороны — только текстовый.
        /// Возвращает список описаний расхождений (пусто — сцены совпадают).
        /// </summary>
        private static IEnumerable<string> Compare(string name, GCodeProgram program, int decimals = 3)
        {
            var scene = SceneBuilder.Build(program);
            var legacy = LegacyPreviewParser.Parse(program.Lines);

            int i = 0, j = 0;
            while (i < scene.Segments.Count || j < legacy.Count)
            {
                if (i < scene.Segments.Count && j < legacy.Count &&
                    SegmentsMatch(scene.Segments[i], legacy[j]))
                {
                    i++;
                    j++;
                    continue;
                }

                // Фантомный сегмент текстовой стороны (округление на границе
                // знака): перемещение в полной точности ≤ 0.0001, в тексте —
                // до 0.0001 + 10^-decimals. Допустимо, если сегмент завершается
                // в текущей позиции структурного парсера.
                if (j < legacy.Count &&
                    IsRoundingArtifact(legacy[j], decimals) &&
                    Close(legacy[j].End, CurrentPosition(scene, i)))
                {
                    j++;
                    continue;
                }

                yield return Divergence(name, i, j, scene, legacy);
                yield break;
            }
        }

        /// <summary>Полное совпадение сегментов (тип, точки, дуга) в допуске.</summary>
        private static bool SegmentsMatch(TrajectorySegment s, LegacyPreviewParser.LegacySegment l)
        {
            if (ToMoveType(l.MoveType) != s.MoveType)
                return false;
            if (!Close(s.Start, l.Start) || !Close(s.End, l.End))
                return false;

            if ((s.ArcCenter == null) != (l.ArcCenter == null))
                return false;
            if (s.ArcCenter != null && !Close(s.ArcCenter.Value, l.ArcCenter.Value))
                return false;
            if (Math.Abs(s.ArcRadius - l.ArcRadius) > Tolerance)
                return false;

            if ((s.InterpolatedPoints == null) != (l.InterpolatedPoints == null))
                return false;
            if (s.InterpolatedPoints != null)
            {
                if (s.InterpolatedPoints.Count != l.InterpolatedPoints.Count)
                    return false;
                for (int p = 0; p < s.InterpolatedPoints.Count; p++)
                    if (!Close(s.InterpolatedPoints[p], l.InterpolatedPoints[p]))
                        return false;
            }

            return true;
        }

        /// <summary>
        /// Сегмент, который мог появиться только из-за округления текста:
        /// максимальный сдвиг координат ≤ 0.0001 + 10^-decimals
        /// (порог «не движение» SceneBuilder + две ошибки округления).
        /// </summary>
        private static bool IsRoundingArtifact(LegacyPreviewParser.LegacySegment l, int decimals)
        {
            double max = Math.Abs(l.End.X - l.Start.X);
            max = Math.Max(max, Math.Abs(l.End.Y - l.Start.Y));
            max = Math.Max(max, Math.Abs(l.End.Z - l.Start.Z));
            return max <= 0.0001 + Math.Pow(10, -decimals);
        }

        /// <summary>Текущая позиция структурного парсера на указателе i.</summary>
        private static Vec3 CurrentPosition(TrajectoryScene scene, int i)
        {
            if (scene.Segments.Count == 0)
                return Vec3.Zero;
            return i < scene.Segments.Count
                ? scene.Segments[i].Start
                : scene.Segments[scene.Segments.Count - 1].End;
        }

        private static string Divergence(string name, int i, int j,
            TrajectoryScene scene, List<LegacyPreviewParser.LegacySegment> legacy)
        {
            var sb = new StringBuilder();
            sb.Append($"{name}: расхождение на сегменте SceneBuilder[{i}] / legacy[{j}] ");
            sb.Append($"(всего {scene.Segments.Count} vs {legacy.Count}).\n");
            if (i < scene.Segments.Count)
            {
                var s = scene.Segments[i];
                sb.Append($"  SceneBuilder: {s.MoveType} {s.Start} -> {s.End}");
                if (s.ArcCenter != null) sb.Append($" center={s.ArcCenter} r={s.ArcRadius}");
                sb.Append('\n');
            }
            if (j < legacy.Count)
            {
                var l = legacy[j];
                sb.Append($"  legacy: {l.MoveType} ({l.Start.X},{l.Start.Y},{l.Start.Z}) -> " +
                          $"({l.End.X},{l.End.Y},{l.End.Z})");
                if (l.ArcCenter != null)
                    sb.Append($" center=({l.ArcCenter.Value.X},{l.ArcCenter.Value.Y},{l.ArcCenter.Value.Z}) r={l.ArcRadius}");
            }
            return sb.ToString();
        }

        private static MoveType ToMoveType(LegacyPreviewParser.LegacyMoveType type)
        {
            switch (type)
            {
                case LegacyPreviewParser.LegacyMoveType.Rapid: return MoveType.Rapid;
                case LegacyPreviewParser.LegacyMoveType.Linear: return MoveType.Linear;
                case LegacyPreviewParser.LegacyMoveType.ArcCW: return MoveType.ArcCW;
                default: return MoveType.ArcCCW;
            }
        }

        private static bool Close(Vec3 a, (double X, double Y, double Z) b) =>
            Math.Abs(a.X - b.X) <= Tolerance &&
            Math.Abs(a.Y - b.Y) <= Tolerance &&
            Math.Abs(a.Z - b.Z) <= Tolerance;

        private static bool Close(Vec3 a, Vec3 b) =>
            Math.Abs(a.X - b.X) <= Tolerance &&
            Math.Abs(a.Y - b.Y) <= Tolerance &&
            Math.Abs(a.Z - b.Z) <= Tolerance;

        private static bool Close((double X, double Y, double Z) a, Vec3 b) =>
            Math.Abs(a.X - b.X) <= Tolerance &&
            Math.Abs(a.Y - b.Y) <= Tolerance &&
            Math.Abs(a.Z - b.Z) <= Tolerance;

        // ------------------------------------------------------------------
        // Фабрики карманов фазы 5 (параметры — как в PocketStrategyTests)
        // ------------------------------------------------------------------

        private static PocketCircleOperation Circle() => new PocketCircleOperation
        {
            CenterX = 0.0, CenterY = 0.0, Radius = 20.0,
        };

        private static PocketRectangleOperation Rectangle() => new PocketRectangleOperation
        {
            Width = 40.0, Height = 20.0,
            ReferencePointX = 0.0, ReferencePointY = 0.0,
            ReferencePointType = ReferencePointType.Center,
        };

        private static PocketEllipseOperation Ellipse() => new PocketEllipseOperation
        {
            CenterX = 0.0, CenterY = 0.0,
            RadiusX = 15.0, RadiusY = 8.0, RotationAngle = 0.0,
        };

        private static PocketDxfOperation Dxf()
        {
            var op = new PocketDxfOperation
            {
                DxfFilePath = DxfFixtureLoader.GetAssetPath("pocket_sample.dxf"),
            };
            op.ClosedContours = DxfFixtureLoader.LoadPocketClosedContours("pocket_sample.dxf");
            return op;
        }

        /// <summary>
        /// Генерирует программу кармана (UnifiedPocketGenerator →
        /// ProgramBuilder → GCodeFormatter) — тот же путь, что и в
        /// PocketStrategyTests.Run, но возвращает GCodeProgram целиком.
        /// </summary>
        private static GCodeProgram PocketProgram(OperationBase op, PocketStrategy strategy,
            bool roughing = false, bool finishing = false,
            PocketFinishingMode mode = PocketFinishingMode.All)
        {
            if (op is IPocketOperation pocket)
            {
                pocket.PocketStrategy = strategy;
                pocket.TotalDepth = 2.0;
                pocket.StepDepth = 2.0; // один слой
                pocket.ContourHeight = 0.0;
                pocket.SafeZHeight = 5.0;
                pocket.ToolDiameter = 10.0;
                pocket.StepPercentOfTool = 40.0;
                pocket.FeedXYRapid = 1000.0;
                pocket.FeedXYWork = 300.0;
                pocket.FeedZRapid = 500.0;
                pocket.FeedZWork = 200.0;
                pocket.Decimals = 6;
                pocket.WallTaperAngleDeg = 0.0;
                pocket.LineAngleDeg = 0.0;
                pocket.IsRoughingEnabled = roughing;
                pocket.IsFinishingEnabled = finishing;
                pocket.FinishAllowance = 2.0;
                pocket.FinishingMode = mode;
            }

            var program = new GCodeProgram();
            new UnifiedPocketGenerator().Generate(op, new ProgramBuilder(program),
                new GCodeSettings { Format = new GCodeFormatSettings { UseComments = true } });
            GCodeFormatter.Format(program, new GCodeSettings { Format = new GCodeFormatSettings { UseLineNumbers = false, UseComments = true } });
            return program;
        }
    }
}
