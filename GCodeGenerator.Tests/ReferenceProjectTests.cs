using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using GCodeGenerator.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Эталонный набор (пункт 0.7 плана): «реальный» многооперационный проект,
    /// сохранённый как настоящий файл .ygc (<c>Reference/reference_project.ygc</c>),
    /// и сгенерированный для него G-код (<c>Reference/reference_project.nc</c>).
    ///
    /// Снимает «ручной» эталон (smoke-чек-лист §8: «Сгенерировать G-код → сравнить
    /// с эталоном (diff)»): эталон теперь в репозитории, diff автоматизирован.
    ///
    /// Отличие от golden-тестов (0.4): тест идёт через полный реальный пайплайн —
    /// файл .ygc → ProjectFileService (реальная сериализация) → SimpleGCodeGenerator
    /// → сравнение с .nc. Golden-тесты используют in-memory фикстуры.
    ///
    /// Эталонный проект: все 19 операций фикстур 0.3 (9 сверл + 6 профилей + 4 кармана —
    /// все 15 типов операций) в порядке каталога. Настройки — значения по умолчанию
    /// GCodeSettings (линейные номера вкл., шпиндель/СОЖ вкл., дуги вкл.):
    /// самая «реалистичная» программа по умолчанию. DxfFilePath DXF-операций
    /// нормализован к именам ассетов без машинного пути (генератор использует
    /// контуры, а не путь; файл остаётся переносимым).
    ///
    /// Примечание: .ygc хранит AssemblyQualifiedName с версией сборки
    /// (в локальных/CI-сборках — 0.0.0.0). Уязвимость: файл, сохранённый сборкой
    /// с версией (YBUILD_PRODUCT_VERSION_DOTNET), не откроется в сборке 0.0.0.0
    /// (Type.GetType не разрешит тип) — устраняется в п. 1.2 (явный дискриминатор
    /// типов, короткие имена).
    ///
    /// Перегенерация эталонного набора: переменная окружения GCG_WRITE_REFERENCE=1
    /// + тест Write_Reference_Set (пишет в исходный каталог), затем пересобрать
    /// и закоммитить файлы. В CI тест — no-op.
    /// </summary>
    [TestClass]
    public class ReferenceProjectTests
    {
        private static readonly ProjectFileService Service = new ProjectFileService();
        private static readonly SimpleGCodeGenerator Generator = new SimpleGCodeGenerator();
        private static CultureInfo _originalCulture;

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

        /// <summary>Эталонные файлы в каталоге сборки тестов (копия из исходников).</summary>
        private static string ReferenceOutputDirectory =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reference");

        /// <summary>Эталонные файлы в исходном каталоге — единственный источник истины.</summary>
        private static string ReferenceSourceDirectory =>
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Reference"));

        /// <summary>
        /// Состав эталонного проекта: все 19 операций фикстур 0.3 в порядке каталога.
        /// DxfFilePath нормализован к именам ассетов (без машинного пути).
        /// </summary>
        private static List<OperationBase> BuildReferenceOperations()
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

        /// <summary>
        /// Эталонный .ygc открывается через ProjectFileService (реальный путь загрузки):
        /// 19 операций, ожидаемые типы и порядок.
        /// </summary>
        [TestMethod]
        public void Reference_Project_Loads_Through_ProjectFileService()
        {
            var ygcPath = Path.Combine(ReferenceOutputDirectory, "reference_project.ygc");
            Assert.IsTrue(File.Exists(ygcPath),
                "Нет эталонного проекта Reference/reference_project.ygc " +
                "(запустите Write_Reference_Set с GCG_WRITE_REFERENCE=1 и закоммитьте файлы)");

            var project = Service.Load(ygcPath);
            var ops = Service.ExtractOperations(project);

            Assert.AreEqual(19, ops.Count, "Число операций в эталонном проекте");

            var expectedTypes = new[]
            {
                typeof(DrillPointsOperation), typeof(DrillPointsOperation), typeof(DrillPointsOperation),
                typeof(DrillPointsOperation), typeof(DrillPointsOperation), typeof(DrillPointsOperation),
                typeof(DrillPointsOperation), typeof(DrillPointsOperation), typeof(DrillPointsOperation),
                typeof(ProfileRectangleOperation), typeof(ProfileRoundedRectangleOperation),
                typeof(ProfileCircleOperation), typeof(ProfileEllipseOperation),
                typeof(ProfilePolygonOperation), typeof(ProfileDxfOperation),
                typeof(PocketRectangleOperation), typeof(PocketCircleOperation),
                typeof(PocketEllipseOperation), typeof(PocketDxfOperation)
            };
            for (int i = 0; i < expectedTypes.Length; i++)
                Assert.AreEqual(expectedTypes[i], ops[i].GetType(), $"Операция [{i}]");
        }

        /// <summary>
        /// G-код, сгенерированный из эталонного .ygc (полный пайплайн),
        /// совпадает построчно с эталонным reference_project.nc.
        /// </summary>
        [TestMethod]
        public void Reference_Project_GCode_Matches_Reference_File()
        {
            var ops = Service.ExtractOperations(
                Service.Load(Path.Combine(ReferenceOutputDirectory, "reference_project.ygc")));
            var program = Generator.Generate(ops, SettingsFixtures.Default());

            var ncPath = Path.Combine(ReferenceOutputDirectory, "reference_project.nc");
            Assert.IsTrue(File.Exists(ncPath),
                "Нет эталонного G-кода Reference/reference_project.nc " +
                "(запустите Write_Reference_Set с GCG_WRITE_REFERENCE=1 и закоммитьте файлы)");

            var expected = File.ReadAllLines(ncPath).ToList();
            var actual = program.Lines.ToList();
            if (expected.SequenceEqual(actual))
                return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"reference_project: эталонное несоответствие " +
                          $"(ожидалось {expected.Count} строк, фактически {actual.Count}):");
            var shown = 0;
            for (int i = 0; i < Math.Max(expected.Count, actual.Count) && shown < 10; i++)
            {
                var e = i < expected.Count ? expected[i] : "<нет строки>";
                var a = i < actual.Count ? actual[i] : "<нет строки>";
                if (e != a)
                {
                    sb.AppendLine($"  строка {i + 1}: ожидается \"{e}\", фактически \"{a}\"");
                    shown++;
                }
            }
            Assert.Fail(sb.ToString());
        }

        /// <summary>
        /// Перегенерация эталонного набора в исходный каталог.
        /// Выполняется только при GCG_WRITE_REFERENCE=1 (в CI — no-op).
        /// </summary>
        [TestMethod]
        public void Write_Reference_Set()
        {
            if (Environment.GetEnvironmentVariable("GCG_WRITE_REFERENCE") != "1")
                return;

            Directory.CreateDirectory(ReferenceSourceDirectory);
            var ops = BuildReferenceOperations();
            Service.Save(Path.Combine(ReferenceSourceDirectory, "reference_project.ygc"), ops);

            var program = Generator.Generate(ops, SettingsFixtures.Default());
            File.WriteAllLines(Path.Combine(ReferenceSourceDirectory, "reference_project.nc"), program.Lines);
        }
    }
}
