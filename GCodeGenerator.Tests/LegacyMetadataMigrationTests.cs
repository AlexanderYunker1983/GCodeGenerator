using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using GCodeGenerator.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Тесты миграции легаси-Metadata в типизированные свойства (пункт 3.2 плана):
    /// старый .ygc открывается, значения попадают в типизированные свойства,
    /// Metadata очищается, новые файлы сохраняются без Metadata.
    /// </summary>
    [TestClass]
    public class LegacyMetadataMigrationTests
    {
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

        private static ProjectFileService Service { get; } = new ProjectFileService();

        /// <summary>Эталонные файлы в каталоге сборки тестов (копия из исходников).</summary>
        private static string ReferenceOutputDirectory =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reference");

        // ------------------------------------------------------------------
        // Легаси-файл v1: сверление
        // ------------------------------------------------------------------

        /// <summary>
        /// Легаси-файл v1: операции сверления после загрузки имеют корректный
        /// DrillMode, типизированные свойства совпадают с эталонными (фикстуры),
        /// Metadata пуст.
        /// </summary>
        [TestMethod]
        public void LegacyV1_DrillOps_MigratedToTypedProperties()
        {
            var path = Path.Combine(ReferenceOutputDirectory, "legacy_project_v1.ygc");
            var loaded = Service.Load(path);
            Assert.IsNotNull(loaded, "Легаси-файл должен открыться");
            Assert.AreEqual(19, loaded.Count);

            var expectedModes = new[]
            {
                DrillMode.Points, DrillMode.Line, DrillMode.Array, DrillMode.Rect,
                DrillMode.Circle, DrillMode.Arc, DrillMode.Polygon, DrillMode.Ellipse,
                DrillMode.Package
            };

            for (int i = 0; i < 9; i++)
            {
                var op = (DrillPointsOperation)loaded[i];
                Assert.AreEqual(expectedModes[i], op.DrillMode, $"операция[{i}]: DrillMode");
                Assert.IsTrue(op.Metadata == null || op.Metadata.Count == 0,
                    $"операция[{i}]: Metadata должна быть пуста после миграции");
            }

            // Значения совпадают с эталонными in-memory операциями (фикстуры).
            var expected = ReferenceOperations.Build();
            for (int i = 0; i < 9; i++)
            {
                var a = (DrillPointsOperation)expected[i];
                var b = (DrillPointsOperation)loaded[i];
                Assert.AreEqual(a.DrillMode, b.DrillMode, $"[{i}]: DrillMode");
                Assert.AreEqual(a.StartX, b.StartX, 1e-9, $"[{i}]: StartX");
                Assert.AreEqual(a.StartY, b.StartY, 1e-9, $"[{i}]: StartY");
                Assert.AreEqual(a.Distance, b.Distance, 1e-9, $"[{i}]: Distance");
                Assert.AreEqual(a.HoleCount, b.HoleCount, $"[{i}]: HoleCount");
                Assert.AreEqual(a.CenterX, b.CenterX, 1e-9, $"[{i}]: CenterX");
                Assert.AreEqual(a.Radius, b.Radius, 1e-9, $"[{i}]: Radius");
                Assert.AreEqual(a.TotalDepth, b.TotalDepth, 1e-9, $"[{i}]: TotalDepth");
                Assert.AreEqual(a.StepDepth, b.StepDepth, 1e-9, $"[{i}]: StepDepth");
                Assert.AreEqual(a.FeedZRapid, b.FeedZRapid, 1e-9, $"[{i}]: FeedZRapid");
                Assert.AreEqual(a.FeedZWork, b.FeedZWork, 1e-9, $"[{i}]: FeedZWork");
                Assert.AreEqual(a.RetractHeight, b.RetractHeight, 1e-9, $"[{i}]: RetractHeight");
                Assert.AreEqual(a.Holes.Count, b.Holes.Count, $"[{i}]: Holes.Count");
            }

            // Конкретные значения ключевых операций (защита от «всё по умолчанию»).
            var line = (DrillPointsOperation)loaded[1];
            Assert.AreEqual(10.0, line.StartX, 1e-9, "Line: StartX");
            Assert.AreEqual(5.0, line.Distance, 1e-9, "Line: Distance");
            Assert.AreEqual(5, line.HoleCount, "Line: HoleCount");

            var array = (DrillPointsOperation)loaded[2];
            Assert.AreEqual(4, array.HoleCount, "Array: HoleCount");
            Assert.AreEqual(3, array.RowCount, "Array: RowCount");
            Assert.AreEqual(5.0, array.RowPitch, 1e-9, "Array: RowPitch");

            var rect = (DrillPointsOperation)loaded[3];
            Assert.AreEqual(10, rect.Holes.Count, "Rect: 10 отверстий по контуру");

            var arc = (DrillPointsOperation)loaded[5];
            Assert.AreEqual(180.0, arc.EndAngleDeg, 1e-9, "Arc: EndAngleDeg");

            var polygon = (DrillPointsOperation)loaded[6];
            Assert.AreEqual(4, polygon.NumberOfSides, "Polygon: NumberOfSides");
            Assert.AreEqual(3, polygon.HolesPerSide, "Polygon: HolesPerSide");

            var ellipse = (DrillPointsOperation)loaded[7];
            Assert.AreEqual(25.0, ellipse.RadiusX, 1e-9, "Ellipse: RadiusX");
            Assert.AreEqual(15.0, ellipse.RadiusY, 1e-9, "Ellipse: RadiusY");

            var package = (DrillPointsOperation)loaded[8];
            Assert.AreEqual("SOIC-8", package.PackageName, "Package: PackageName");
        }

        /// <summary>
        /// DoD фазы 3: старый .ygc → открыт → сохранён → Metadata пуст,
        /// значения сохранены (повторная загрузка даёт те же операции).
        /// </summary>
        [TestMethod]
        public void LegacyV1_OpenSave_MetadataEmpty_ValuesPreserved()
        {
            var legacyPath = Path.Combine(ReferenceOutputDirectory, "legacy_project_v1.ygc");
            var loaded = Service.Load(legacyPath);
            Assert.IsNotNull(loaded);

            var v2Path = Path.Combine(Path.GetTempPath(), "gcg_meta_migrate_" + Guid.NewGuid().ToString("N") + ".ygc");
            try
            {
                Service.Save(v2Path, loaded);

                // В сохранённом JSON Metadata всех операций — пустой объект.
                var json = File.ReadAllText(v2Path, Encoding.UTF8);
                using (var doc = System.Text.Json.JsonDocument.Parse(json))
                {
                    var ops = doc.RootElement.GetProperty("operations");
                    Assert.AreEqual(19, ops.GetArrayLength());
                    foreach (var entry in ops.EnumerateArray())
                    {
                        var data = entry.GetProperty("data");
                        if (!data.TryGetProperty("Metadata", out var metadata))
                            continue; // у ProfileDxfOperation/Metadata может не быть
                        Assert.AreEqual(System.Text.Json.JsonValueKind.Object, metadata.ValueKind,
                            "Metadata — объект");
                        Assert.AreEqual(0, metadata.EnumerateObject().Count(),
                            $"type={entry.GetProperty("type").GetString()}: Metadata должна быть пуста");
                    }
                }

                // Значения переживают цикл open → save → open.
                var reloaded = Service.Load(v2Path);
                Assert.AreEqual(19, reloaded.Count);
                var expected = ReferenceOperations.Build();
                for (int i = 0; i < 19; i++)
                {
                    Assert.AreEqual(expected[i].GetType(), reloaded[i].GetType(), $"[{i}]: тип");
                    var a = expected[i] as DrillPointsOperation;
                    var b = reloaded[i] as DrillPointsOperation;
                    if (a != null)
                    {
                        Assert.AreEqual(a.DrillMode, b.DrillMode, $"[{i}]: DrillMode");
                        Assert.AreEqual(a.Holes.Count, b.Holes.Count, $"[{i}]: Holes.Count");
                    }
                }
            }
            finally
            {
                if (File.Exists(v2Path))
                    File.Delete(v2Path);
            }
        }

        // ------------------------------------------------------------------
        // Heuristic Array/Rect
        // ------------------------------------------------------------------

        private static DrillPointsOperation LegacyDrillOp(int holeCount, params KeyValuePair<string, object>[] meta)
        {
            var op = new DrillPointsOperation();
            for (int i = 0; i < holeCount; i++)
                op.Holes.Add(new DrillHole { X = i, Y = 0, Z = 0, TotalDepth = 2, StepDepth = 1, FeedZRapid = 500, FeedZWork = 200, RetractHeight = 0.3 });
            foreach (var kv in meta)
                op.Metadata[kv.Key] = kv.Value;
            return op;
        }

        /// <summary>
        /// Array (все точки сетки 4×3 = 12 отверстий) распознаётся как Array.
        /// </summary>
        [TestMethod]
        public void MigrateDrill_FullGrid_IsArray()
        {
            var op = LegacyDrillOp(12,
                new KeyValuePair<string, object>("StartX", 10),
                new KeyValuePair<string, object>("StartY", 10),
                new KeyValuePair<string, object>("StartZ", 0),
                new KeyValuePair<string, object>("Distance", 5),
                new KeyValuePair<string, object>("HoleCount", 4),
                new KeyValuePair<string, object>("AngleDeg", 0),
                new KeyValuePair<string, object>("RowPitch", 5),
                new KeyValuePair<string, object>("RowCount", 3));

            LegacyMetadataMigrator.Migrate(op);

            Assert.AreEqual(DrillMode.Array, op.DrillMode);
            Assert.AreEqual(0, op.Metadata.Count, "Metadata очищена");
        }

        /// <summary>
        /// Rect (контур 4×3 = 10 отверстий) распознаётся как Rect.
        /// </summary>
        [TestMethod]
        public void MigrateDrill_BorderOnly_IsRect()
        {
            var op = LegacyDrillOp(10,
                new KeyValuePair<string, object>("StartX", 10),
                new KeyValuePair<string, object>("StartY", 10),
                new KeyValuePair<string, object>("StartZ", 0),
                new KeyValuePair<string, object>("Distance", 5),
                new KeyValuePair<string, object>("HoleCount", 4),
                new KeyValuePair<string, object>("AngleDeg", 0),
                new KeyValuePair<string, object>("RowPitch", 5),
                new KeyValuePair<string, object>("RowCount", 3));

            LegacyMetadataMigrator.Migrate(op);

            Assert.AreEqual(DrillMode.Rect, op.DrillMode);
            Assert.AreEqual(0, op.Metadata.Count, "Metadata очищена");
        }

        /// <summary>
        /// Неоднозначные случаи (RowCount==2 или HoleCount==2: обе формулы дают
        /// одинаковое число отверстий) — выбирается Array; G-код идентичен.
        /// </summary>
        [TestMethod]
        public void MigrateDrill_AmbiguousGrid_DefaultsToArray()
        {
            // 2 ряда × 3 отверстия = 6: и Array (2*3), и Rect (2*2+2*3-4) = 6.
            var op2x3 = LegacyDrillOp(6,
                new KeyValuePair<string, object>("StartX", 0),
                new KeyValuePair<string, object>("StartY", 0),
                new KeyValuePair<string, object>("StartZ", 0),
                new KeyValuePair<string, object>("Distance", 5),
                new KeyValuePair<string, object>("HoleCount", 3),
                new KeyValuePair<string, object>("AngleDeg", 0),
                new KeyValuePair<string, object>("RowPitch", 5),
                new KeyValuePair<string, object>("RowCount", 2));
            LegacyMetadataMigrator.Migrate(op2x3);
            Assert.AreEqual(DrillMode.Array, op2x3.DrillMode, "2×3 → Array");

            // 3 ряда × 2 отверстия = 6: и Array (3*2), и Rect (2*3+2*2-4) = 6.
            var op3x2 = LegacyDrillOp(6,
                new KeyValuePair<string, object>("StartX", 0),
                new KeyValuePair<string, object>("StartY", 0),
                new KeyValuePair<string, object>("StartZ", 0),
                new KeyValuePair<string, object>("Distance", 5),
                new KeyValuePair<string, object>("HoleCount", 2),
                new KeyValuePair<string, object>("AngleDeg", 0),
                new KeyValuePair<string, object>("RowPitch", 5),
                new KeyValuePair<string, object>("RowCount", 3));
            LegacyMetadataMigrator.Migrate(op3x2);
            Assert.AreEqual(DrillMode.Array, op3x2.DrillMode, "3×2 → Array");
        }

        /// <summary>
        /// Metadata без распознанных ключей паттерна (вручную созданный файл) —
        /// DrillMode остаётся Points, Metadata не удаляется (данные не теряются).
        /// </summary>
        [TestMethod]
        public void MigrateDrill_UnknownKeys_KeptAsPoints()
        {
            var op = LegacyDrillOp(3,
                new KeyValuePair<string, object>("CustomKey", 42));

            LegacyMetadataMigrator.Migrate(op);

            Assert.AreEqual(DrillMode.Points, op.DrillMode);
            Assert.AreEqual(1, op.Metadata.Count, "нераспознанные ключи сохраняются");
            Assert.IsTrue(op.Metadata.ContainsKey("CustomKey"));
        }

        /// <summary>
        /// Миграция идемпотентна: повторный вызов не изменяет операцию.
        /// </summary>
        [TestMethod]
        public void MigrateDrill_Idempotent()
        {
            var op = LegacyDrillOp(5,
                new KeyValuePair<string, object>("StartX", 10),
                new KeyValuePair<string, object>("StartY", 0),
                new KeyValuePair<string, object>("StartZ", 0),
                new KeyValuePair<string, object>("Distance", 5),
                new KeyValuePair<string, object>("HoleCount", 5),
                new KeyValuePair<string, object>("AngleDeg", 0));

            LegacyMetadataMigrator.Migrate(op);
            var modeAfterFirst = op.DrillMode;
            var startXAfterFirst = op.StartX;

            LegacyMetadataMigrator.Migrate(op);

            Assert.AreEqual(modeAfterFirst, op.DrillMode);
            Assert.AreEqual(startXAfterFirst, op.StartX, 1e-9);
            Assert.AreEqual(0, op.Metadata.Count);
        }

        // ------------------------------------------------------------------
        // Профили (пункт 3.6: Metadata — [Obsolete] + [JsonIgnore])
        // ------------------------------------------------------------------

        /// <summary>
        /// Пункт 3.6: поле Metadata в JSON старого файла профиля игнорируется
        /// при загрузке ([JsonIgnore]), типизированные свойства из JSON
        /// сохраняются, Metadata не заполняется и не пишется при сохранении.
        /// </summary>
        [TestMethod]
        public void Profile_MetadataInJson_IgnoredOnLoad_AndNotSaved()
        {
            const string json = "{\"version\":2,\"operations\":[{\"type\":\"ProfileCircle\",\"data\":{"
                + "\"CenterX\":30,\"CenterY\":40,\"Radius\":50,\"TotalDepth\":6,"
                + "\"Metadata\":{\"Radius\":999,\"ToolPathMode\":1}}}]}";

            var loaded = Service.Deserialize(json);
            Assert.AreEqual(1, loaded.Count);
            var op = (ProfileCircleOperation)loaded[0];

            // Типизированные свойства из JSON сохранены.
            Assert.AreEqual(30.0, op.CenterX, 1e-9);
            Assert.AreEqual(40.0, op.CenterY, 1e-9);
            Assert.AreEqual(50.0, op.Radius, 1e-9, "Radius из типизированного свойства, а не из Metadata");
            Assert.AreEqual(6.0, op.TotalDepth, 1e-9);

            // Metadata не десериализуется ([JsonIgnore], пункт 3.6).
#pragma warning disable CS0618 // намеренная проверка устаревшего свойства
            Assert.IsNull(op.Metadata, "Metadata не должна заполняться из JSON");
#pragma warning restore CS0618

            // При сохранении Metadata не пишется.
            var saved = Service.Serialize(loaded);
            Assert.IsFalse(saved.Contains("\"Metadata\""), "в сохранённом JSON нет поля Metadata");

            // Повторная загрузка — те же значения.
            var reloaded = (ProfileCircleOperation)Service.Deserialize(saved)[0];
            Assert.AreEqual(50.0, reloaded.Radius, 1e-9);
        }

        // ------------------------------------------------------------------
        // Карманы (пункт 7.2c: Metadata — [Obsolete], миграция при загрузке)
        // ------------------------------------------------------------------

        /// <summary>
        /// Круглый карман: Metadata с ключом-триггером "Radius" — значения
        /// копируются в типизированные свойства (Metadata побеждает, как в
        /// старом диалоге), мигрированные ключи удаляются.
        /// </summary>
        [TestMethod]
        public void MigratePocketCircle_TriggerKey_MigratedToTypedProperties()
        {
            var op = new PocketCircleOperation
            {
                CenterX = 1, CenterY = 2, Radius = 3, TotalDepth = 4,
                StepDepth = 0.5, ToolDiameter = 5, ContourHeight = 0,
                FeedXYRapid = 1, FeedXYWork = 1, FeedZRapid = 1, FeedZWork = 1,
                SafeZHeight = 0, RetractHeight = 0, StepPercentOfTool = 6,
                Decimals = 0, LineAngleDeg = 0, WallTaperAngleDeg = 0,
                Direction = MillingDirection.Clockwise,
                PocketStrategy = PocketStrategy.Spiral,
                IsRoughingEnabled = false, IsFinishingEnabled = false,
                FinishAllowance = 0, FinishingMode = PocketFinishingMode.All,
            };
            // Metadata с другими значениями: старые диалоги писали в оба места,
            // при расхождении старая логика чтения брала Metadata.
#pragma warning disable CS0618 // намеренная проверка устаревшего свойства
            op.Metadata["Direction"] = MillingDirection.CounterClockwise;
            op.Metadata["PocketStrategy"] = PocketStrategy.Concentric;
            op.Metadata["CenterX"] = 20;
            op.Metadata["CenterY"] = 30;
            op.Metadata["Radius"] = 10;
            op.Metadata["TotalDepth"] = 8;
            op.Metadata["StepDepth"] = 2;
            op.Metadata["ToolDiameter"] = 4;
            op.Metadata["ContourHeight"] = 0.5;
            op.Metadata["FeedXYRapid"] = 1000;
            op.Metadata["FeedXYWork"] = 300;
            op.Metadata["FeedZRapid"] = 500;
            op.Metadata["FeedZWork"] = 200;
            op.Metadata["SafeZHeight"] = 1;
            op.Metadata["RetractHeight"] = 0.3;
            op.Metadata["StepPercentOfTool"] = 40;
            op.Metadata["Decimals"] = 3;
            op.Metadata["LineAngleDeg"] = 15;
            op.Metadata["WallTaperAngleDeg"] = 5;
            op.Metadata["IsRoughingEnabled"] = true;
            op.Metadata["IsFinishingEnabled"] = true;
            op.Metadata["FinishAllowance"] = 0.2;
            op.Metadata["FinishingMode"] = PocketFinishingMode.Walls;
#pragma warning restore CS0618

            LegacyMetadataMigrator.Migrate(op);

            Assert.AreEqual(20.0, op.CenterX, 1e-9);
            Assert.AreEqual(30.0, op.CenterY, 1e-9);
            Assert.AreEqual(10.0, op.Radius, 1e-9, "Radius из Metadata (ключ-триггер)");
            Assert.AreEqual(8.0, op.TotalDepth, 1e-9);
            Assert.AreEqual(2.0, op.StepDepth, 1e-9);
            Assert.AreEqual(4.0, op.ToolDiameter, 1e-9);
            Assert.AreEqual(0.5, op.ContourHeight, 1e-9);
            Assert.AreEqual(1000.0, op.FeedXYRapid, 1e-9);
            Assert.AreEqual(300.0, op.FeedXYWork, 1e-9);
            Assert.AreEqual(500.0, op.FeedZRapid, 1e-9);
            Assert.AreEqual(200.0, op.FeedZWork, 1e-9);
            Assert.AreEqual(1.0, op.SafeZHeight, 1e-9);
            Assert.AreEqual(0.3, op.RetractHeight, 1e-9);
            Assert.AreEqual(40.0, op.StepPercentOfTool, 1e-9);
            Assert.AreEqual(3, op.Decimals);
            Assert.AreEqual(15.0, op.LineAngleDeg, 1e-9);
            Assert.AreEqual(5.0, op.WallTaperAngleDeg, 1e-9);
            Assert.AreEqual(MillingDirection.CounterClockwise, op.Direction);
            Assert.AreEqual(PocketStrategy.Concentric, op.PocketStrategy);
            Assert.IsTrue(op.IsRoughingEnabled);
            Assert.IsTrue(op.IsFinishingEnabled);
            Assert.AreEqual(0.2, op.FinishAllowance, 1e-9);
            Assert.AreEqual(PocketFinishingMode.Walls, op.FinishingMode);
#pragma warning disable CS0618 // намеренная проверка устаревшего свойства
            Assert.AreEqual(0, op.Metadata.Count, "Metadata очищена");
#pragma warning restore CS0618
        }

        /// <summary>
        /// Круглый карман: Metadata без ключа-триггера (только нераспознанные
        /// ключи) — типизированные свойства не изменяются, Metadata сохраняется
        /// (данные не теряются).
        /// </summary>
        [TestMethod]
        public void MigratePocket_NoTriggerKey_TypedUntouched_MetadataKept()
        {
            var op = new PocketCircleOperation
            {
                CenterX = 11, CenterY = 12, Radius = 13,
                ToolDiameter = 14, StepPercentOfTool = 15, Decimals = 2,
            };
#pragma warning disable CS0618 // намеренная проверка устаревшего свойства
            op.Metadata["CustomKey"] = 42;
#pragma warning restore CS0618

            LegacyMetadataMigrator.Migrate(op);

            Assert.AreEqual(11.0, op.CenterX, 1e-9, "CenterX не изменён");
            Assert.AreEqual(12.0, op.CenterY, 1e-9);
            Assert.AreEqual(13.0, op.Radius, 1e-9);
            Assert.AreEqual(14.0, op.ToolDiameter, 1e-9);
            Assert.AreEqual(15.0, op.StepPercentOfTool, 1e-9);
            Assert.AreEqual(2, op.Decimals);
#pragma warning disable CS0618 // намеренная проверка устаревшего свойства
            Assert.AreEqual(1, op.Metadata.Count, "нераспознанные ключи сохраняются");
            Assert.IsTrue(op.Metadata.ContainsKey("CustomKey"));
#pragma warning restore CS0618
        }

        /// <summary>
        /// Круглый карман: Metadata с триггером, но без части ключей —
        /// присутствующие мигрируются, отсутствующие оставляют текущие
        /// типизированные значения (старый диалог бросал бы
        /// KeyNotFoundException; мигратор — допустим, строго лучше).
        /// </summary>
        [TestMethod]
        public void MigratePocket_PartialMetadata_MissingKeysKeepTypedValues()
        {
            var op = new PocketCircleOperation
            {
                Radius = 13, TotalDepth = 17, Decimals = 2,
            };
#pragma warning disable CS0618 // намеренная проверка устаревшего свойства
            op.Metadata["Radius"] = 10;
            op.Metadata["ToolDiameter"] = 4;
#pragma warning restore CS0618

            LegacyMetadataMigrator.Migrate(op);

            Assert.AreEqual(10.0, op.Radius, 1e-9, "Radius из Metadata");
            Assert.AreEqual(4.0, op.ToolDiameter, 1e-9, "ToolDiameter из Metadata");
            Assert.AreEqual(17.0, op.TotalDepth, 1e-9, "TotalDepth без ключа — не изменён");
            Assert.AreEqual(2, op.Decimals, "Decimals без ключа — не изменён");
#pragma warning disable CS0618 // намеренная проверка устаревшего свойства
            Assert.AreEqual(0, op.Metadata.Count, "присутствующие ключи удалены");
#pragma warning restore CS0618
        }

        /// <summary>
        /// Ключи-триггеры прямоугольного ("Width") и эллиптического
        /// ("RadiusX") карманов работают.
        /// </summary>
        [TestMethod]
        public void MigratePocket_Rectangle_Ellipse_TriggersWork()
        {
            var rect = new PocketRectangleOperation { Width = 1, Height = 2, RotationAngle = 0 };
#pragma warning disable CS0618 // намеренная проверка устаревшего свойства
            rect.Metadata["Width"] = 40;
            rect.Metadata["Height"] = 20;
            rect.Metadata["RotationAngle"] = 30;
            rect.Metadata["ReferencePointType"] = ReferencePointType.TopLeft;
#pragma warning restore CS0618
            LegacyMetadataMigrator.Migrate(rect);
            Assert.AreEqual(40.0, rect.Width, 1e-9);
            Assert.AreEqual(20.0, rect.Height, 1e-9);
            Assert.AreEqual(30.0, rect.RotationAngle, 1e-9);
            Assert.AreEqual(ReferencePointType.TopLeft, rect.ReferencePointType);
#pragma warning disable CS0618 // намеренная проверка устаревшего свойства
            Assert.AreEqual(0, rect.Metadata.Count);
#pragma warning restore CS0618

            var ellipse = new PocketEllipseOperation { RadiusX = 1, RadiusY = 2, RotationAngle = 0 };
#pragma warning disable CS0618 // намеренная проверка устаревшего свойства
            ellipse.Metadata["RadiusX"] = 15;
            ellipse.Metadata["RadiusY"] = 8;
            ellipse.Metadata["RotationAngle"] = 45;
#pragma warning restore CS0618
            LegacyMetadataMigrator.Migrate(ellipse);
            Assert.AreEqual(15.0, ellipse.RadiusX, 1e-9);
            Assert.AreEqual(8.0, ellipse.RadiusY, 1e-9);
            Assert.AreEqual(45.0, ellipse.RotationAngle, 1e-9);
#pragma warning disable CS0618 // намеренная проверка устаревшего свойства
            Assert.AreEqual(0, ellipse.Metadata.Count);
#pragma warning restore CS0618
        }

        /// <summary>
        /// Сквозной: JSON кармана с расхождением типизированных свойств и
        /// Metadata (триггер присутствует) — при загрузке Metadata побеждает
        /// (повторяет старое чтение диалога); при сохранении Metadata пуст,
        /// повторная загрузка даёт те же значения.
        /// </summary>
        [TestMethod]
        public void Pocket_MetadataInJson_WinsOnLoad_AndNotSaved()
        {
            const string json = "{\"version\":2,\"operations\":[{\"type\":\"PocketCircle\",\"data\":{"
                + "\"CenterX\":20,\"CenterY\":30,\"Radius\":50,\"TotalDepth\":8,\"StepDepth\":2,"
                + "\"ToolDiameter\":4,\"ContourHeight\":0.5,\"FeedXYRapid\":1000,\"FeedXYWork\":300,"
                + "\"FeedZRapid\":500,\"FeedZWork\":200,\"SafeZHeight\":1,\"RetractHeight\":0.3,"
                + "\"StepPercentOfTool\":40,\"Decimals\":3,\"LineAngleDeg\":15,\"WallTaperAngleDeg\":5,"
                + "\"Direction\":1,\"PocketStrategy\":0,\"IsRoughingEnabled\":true,\"IsFinishingEnabled\":true,"
                + "\"FinishAllowance\":0.2,\"FinishingMode\":0,"
                + "\"Metadata\":{\"Radius\":999,\"ToolDiameter\":6,\"Decimals\":1}}}]}";

            var loaded = Service.Deserialize(json);
            Assert.AreEqual(1, loaded.Count);
            var op = (PocketCircleOperation)loaded[0];

            // Metadata побеждает (как в старом диалоге), остальное — из JSON.
            Assert.AreEqual(999.0, op.Radius, 1e-9, "Radius из Metadata");
            Assert.AreEqual(6.0, op.ToolDiameter, 1e-9, "ToolDiameter из Metadata");
            Assert.AreEqual(1, op.Decimals, "Decimals из Metadata");
            Assert.AreEqual(20.0, op.CenterX, 1e-9, "CenterX из типизированного свойства");
            Assert.AreEqual(8.0, op.TotalDepth, 1e-9);
            Assert.AreEqual(MillingDirection.CounterClockwise, op.Direction);
            Assert.AreEqual(PocketStrategy.Concentric, op.PocketStrategy);

            // При сохранении Metadata — пустой объект (ключи мигрированы).
            var saved = Service.Serialize(loaded);
            Assert.IsTrue(saved.Contains("\"Metadata\":{}"), "Metadata в сохранённом JSON — пустой объект");

            // Повторная загрузка — те же значения.
            var reloaded = (PocketCircleOperation)Service.Deserialize(saved)[0];
            Assert.AreEqual(999.0, reloaded.Radius, 1e-9);
            Assert.AreEqual(6.0, reloaded.ToolDiameter, 1e-9);
            Assert.AreEqual(1, reloaded.Decimals);
        }

        /// <summary>
        /// DXF-карман: Metadata не мигрируется (диалог никогда не читал
        /// Metadata) — значения не копируются, ключи не удаляются.
        /// </summary>
        [TestMethod]
        public void PocketDxf_MetadataNotMigrated()
        {
            var op = new PocketDxfOperation
            {
                TotalDepth = 2, ToolDiameter = 3,
            };
#pragma warning disable CS0618 // намеренная проверка устаревшего свойства
            op.Metadata["Radius"] = 999;
            op.Metadata["TotalDepth"] = 8;
#pragma warning restore CS0618

            LegacyMetadataMigrator.Migrate(op);

            Assert.AreEqual(2.0, op.TotalDepth, 1e-9, "TotalDepth не изменён");
#pragma warning disable CS0618 // намеренная проверка устаревшего свойства
            Assert.AreEqual(2, op.Metadata.Count, "Metadata не тронута");
            Assert.IsTrue(op.Metadata.ContainsKey("Radius"));
            Assert.IsTrue(op.Metadata.ContainsKey("TotalDepth"));
#pragma warning restore CS0618
        }

        /// <summary>
        /// Легаси-файл v1: карманы открываются с типизированными свойствами
        /// и пустым Metadata (в закреплённой фикстуре Metadata карманов пустой —
        /// миграция здесь нет-оп, защита от регрессий формата).
        /// </summary>
        [TestMethod]
        public void LegacyV1_PocketOps_TypedProperties_EmptyMetadata()
        {
            var path = Path.Combine(ReferenceOutputDirectory, "legacy_project_v1.ygc");
            var loaded = Service.Load(path);
            Assert.IsNotNull(loaded, "Легаси-файл должен открыться");

            var rect = (PocketRectangleOperation)loaded[15];
            var circle = (PocketCircleOperation)loaded[16];
            var ellipse = (PocketEllipseOperation)loaded[17];
            var dxf = (PocketDxfOperation)loaded[18];

            Assert.AreEqual(40.0, rect.Width, 1e-9);
            Assert.AreEqual(20.0, rect.Height, 1e-9);
            Assert.AreEqual(PocketStrategy.Spiral, rect.PocketStrategy);
            Assert.AreEqual(20.0, circle.CenterX, 1e-9);
            Assert.AreEqual(10.0, circle.Radius, 1e-9);
            Assert.AreEqual(15.0, ellipse.RadiusX, 1e-9);
            Assert.AreEqual(8.0, ellipse.RadiusY, 1e-9);
            Assert.AreEqual(2, dxf.ClosedContours.Count);

#pragma warning disable CS0618 // намеренная проверка устаревшего свойства
            Assert.AreEqual(0, rect.Metadata.Count, "rect: Metadata пуста");
            Assert.AreEqual(0, circle.Metadata.Count, "circle: Metadata пуста");
            Assert.AreEqual(0, ellipse.Metadata.Count, "ellipse: Metadata пуста");
            Assert.AreEqual(0, dxf.Metadata.Count, "dxf: Metadata пуста");
#pragma warning restore CS0618
        }
    }
}
