using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using GCodeGenerator.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Тесты ProjectFileService (пункты 0.6 и 1.2 плана): round-trip проекта .ygc.
    ///
    /// Формат v2 (System.Text.Json), в который всегда сохраняется:
    /// {"version":2,"operations":[{"type":"&lt;короткое имя&gt;","data":{...}}]}.
    /// Легаси-формат v1 (JavaScriptSerializer) — только чтение:
    /// {"Operations":[{"Type":"&lt;AssemblyQualifiedName&gt;","Data":"&lt;JSON операции&gt;"}]}.
    ///
    /// Нюанс сравнения: значения Metadata (Dictionary&lt;string, object&gt;) после round-trip
    /// приходят как Int32/Decimal/string (повторяет JavaScriptSerializer), а не как исходный
    /// тип — числа сравниваются как double.
    /// </summary>
    [TestClass]
    public class ProjectFileServiceTests
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

        /// <summary>
        /// Все 19 операций фикстур 0.3 (9 сверл, 6 профилей, 4 кармана) —
        /// покрывают все 11 конкретных типов операций.
        /// </summary>
        private static List<OperationBase> BuildAllOperations()
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

            // Кастомные Name/IsEnabled — проверяем, что пользовательские значения
            // переживают round-trip (а не только значения по умолчанию из конструкторов).
            ops[0].Name = "Сверление: точки";
            ops[0].IsEnabled = false;
            ops[11].Name = "Профиль: окружность";
            ops[18].Name = "Карман: DXF";

            return ops;
        }

        // ------------------------------------------------------------------
        // Round-trip (v2)
        // ------------------------------------------------------------------

        /// <summary>
        /// Round-trip через файл: сохранить все 19 операций → открыть →
        /// количество, порядок, типы и ВСЕ поля совпадают.
        /// </summary>
        [TestMethod]
        public void RoundTrip_AllOperationTypes_File_PreservesAllFields()
        {
            var original = BuildAllOperations();
            var filePath = Path.Combine(Path.GetTempPath(), "gcg_roundtrip_" + Guid.NewGuid().ToString("N") + ".ygc");
            try
            {
                Service.Save(filePath, original, null);
                var loaded = Service.Load(filePath).Operations;

                Assert.AreEqual(original.Count, loaded.Count, "Число операций");

                for (int i = 0; i < original.Count; i++)
                {
                    var a = original[i];
                    var b = loaded[i];
                    Assert.AreEqual(a.GetType(), b.GetType(), $"Операция [{i}]: тип");
                    CompareOperation($"операция[{i}] ({a.GetType().Name})", a, b);
                }
            }
            finally
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        /// <summary>
        /// Round-trip in-memory (Serialize/Deserialize) даёт тот же результат,
        /// что и через файл (Save/Load) — файл лишь переносит JSON.
        /// </summary>
        [TestMethod]
        public void RoundTrip_InMemory_MatchesFileRoundTrip()
        {
            var original = BuildAllOperations();

            var json = Service.Serialize(original, null);
            var inMemory = Service.Deserialize(json).Operations;

            var filePath = Path.Combine(Path.GetTempPath(), "gcg_roundtrip_" + Guid.NewGuid().ToString("N") + ".ygc");
            try
            {
                Service.Save(filePath, original, null);
                var fromFile = Service.Load(filePath).Operations;

                Assert.AreEqual(inMemory.Count, fromFile.Count, "Число операций");
                for (int i = 0; i < inMemory.Count; i++)
                    CompareOperation($"операция[{i}]", inMemory[i], fromFile[i]);
            }
            finally
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        // ------------------------------------------------------------------
        // Формат v2
        // ------------------------------------------------------------------

        /// <summary>
        /// Формат v2 зафиксирован: конверт {"version":2,"operations":[{type,data}...]},
        /// короткий дискриминатор типа, данные операции — вложенный JSON-объект.
        /// </summary>
        [TestMethod]
        public void FileFormat_V2Structure()
        {
            var json = Service.Serialize(BuildAllOperations(), null);

            Assert.IsTrue(json.StartsWith("{\"version\":2,\"operations\":[", StringComparison.Ordinal),
                "Файл должен начинаться с {\"version\":2,\"operations\":[");
            Assert.IsTrue(json.Contains("\"type\":\"DrillPoints\""),
                "type — короткое имя операции (не AssemblyQualifiedName)");
            Assert.IsTrue(json.Contains("\"data\":{"),
                "data — вложенный JSON-объект операции");
            Assert.IsFalse(json.Contains("AssemblyQualifiedName") && json.Contains(", GCodeGenerator, Version="),
                "Не должно содержать AssemblyQualifiedName с версией сборки");
            Assert.AreEqual(19, json.Split(new[] { "\"type\":\"" }, StringSplitOptions.None).Length - 1,
                "По одной записи на операцию");
        }

        // ------------------------------------------------------------------
        // Некорректные записи и ошибки
        // ------------------------------------------------------------------

        /// <summary>
        /// Некорректная или неизвестная операция отклоняет весь файл: частичная
        /// загрузка с последующим сохранением потеряла бы непрочитанные данные.
        /// </summary>
        [TestMethod]
        public void Deserialize_InvalidOrUnknownOperation_ThrowsWithoutPartialLoad()
        {
            var invalidEntries = new[]
            {
                "42",
                "{\"type\":\"\",\"data\":{}}",
                "{\"type\":\"UnknownType\",\"data\":{}}",
                "{\"type\":\"ProfileCircle\"}",
            };

            foreach (var invalidEntry in invalidEntries)
            {
                var json = "{\"version\":2,\"operations\":["
                    + "{\"type\":\"ProfileCircle\",\"data\":{}},"
                    + invalidEntry
                    + "]}";

                Exception exception = null;
                try
                {
                    Service.Deserialize(json);
                }
                catch (Exception ex)
                {
                    exception = ex;
                }

                Assert.IsNotNull(exception, invalidEntry);
            }
        }

        [TestMethod]
        public void Deserialize_UnsupportedVersion_Throws()
        {
            var newer = "{\"version\":3,\"operations\":[{\"type\":\"ProfileCircle\",\"data\":{}}]}";
            var olderTagged = "{\"version\":1,\"operations\":[]}";

            Assert.ThrowsException<NotSupportedException>(() => Service.Deserialize(newer));
            Assert.ThrowsException<NotSupportedException>(() => Service.Deserialize(olderTagged));
        }

        [TestMethod]
        public void Deserialize_UnknownRootSection_Throws()
        {
            const string json = "{\"version\":2,\"operations\":[],\"futureData\":{\"keep\":true}}";

            Assert.ThrowsException<NotSupportedException>(() => Service.Deserialize(json));
        }

        [TestMethod]
        public void Save_OverExistingProject_UsesAtomicReplacementWithoutTemporaryFiles()
        {
            var directory = Path.Combine(Path.GetTempPath(), "gcg_atomic_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "project.ygc");
            try
            {
                File.WriteAllText(path, "old content");

                Service.Save(path, new List<OperationBase> { OperationFixtures.ProfileCircle() }, null);

                Assert.IsTrue(File.ReadAllText(path).Contains("\"type\":\"ProfileCircle\""));
                CollectionAssert.AreEqual(
                    new[] { "project.ygc" },
                    Directory.GetFiles(directory).Select(Path.GetFileName).ToArray(),
                    "Временный файл не должен оставаться после успешной замены");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// Валидный тип + не-объектный JSON данных (42) — БРОСАЕТ исключение
        /// (не возвращает null) — зафиксировано как поведение прежнего
        /// LoadOperationsFromProject: в UI это ошибка «Ошибка при загрузке проекта».
        /// </summary>
        [TestMethod]
        public void Deserialize_ValidTypeWithNonObjectData_Throws()
        {
            var json = "{\"version\":2,\"operations\":[{\"type\":\"ProfileCircle\",\"data\":42}]}";
            try
            {
                Service.Deserialize(json);
                Assert.Fail("Ожидалось исключение при не-объектном JSON данных операции");
            }
            catch (Exception)
            {
                // Ожидаемо: обработчик ошибки — в MainViewModel.OpenProject
            }
        }

        /// <summary>
        /// Нет секции операций (пустой объект, null, чужой файл) → null
        /// (в UI — «Невозможно прочитать файл проекта»).
        /// </summary>
        [TestMethod]
        public void Deserialize_NoOperationsSection_ReturnsNull()
        {
            Assert.IsNull(Service.Deserialize("{}").Operations, "пустой объект");
            Assert.IsNull(Service.Deserialize("{\"version\":2}").Operations, "v2 без operations");
            Assert.IsNull(Service.Deserialize("{\"version\":2,\"operations\":null}").Operations, "v2 operations=null");
            Assert.IsNull(Service.Deserialize("{\"Operations\":null}").Operations, "легаси Operations=null");
            Assert.IsNull(Service.Deserialize("{\"Foo\":\"bar\"}").Operations, "чужой файл без секции операций");
        }

        /// <summary>
        /// Пустой массив операций → пустой список (не null): проект очищается без ошибки.
        /// </summary>
        [TestMethod]
        public void Deserialize_EmptyOperations_ReturnsEmptyList()
        {
            var v2 = Service.Deserialize("{\"version\":2,\"operations\":[]}").Operations;
            Assert.IsNotNull(v2, "v2 пустой массив");
            Assert.AreEqual(0, v2.Count);

            var legacy = Service.Deserialize("{\"Operations\":[]}").Operations;
            Assert.IsNotNull(legacy, "легаси пустой массив");
            Assert.AreEqual(0, legacy.Count);
        }

        /// <summary>
        /// Некорректный JSON — исключение (VM ловит его и показывает ошибку, как раньше).
        /// </summary>
        [TestMethod]
        public void Deserialize_InvalidJson_Throws()
        {
            try
            {
                Service.Deserialize("{ это не JSON");
                Assert.Fail("Ожидалось исключение при некорректном JSON");
            }
            catch (Exception)
            {
                // Ожидаемо: обработчик ошибки — в MainViewModel.OpenProject
            }
        }

        // ------------------------------------------------------------------
        // Легаси (v1, JavaScriptSerializer)
        // ------------------------------------------------------------------

        /// <summary>
        /// Эталонный легаси-файл v1 (Reference/legacy_project_v1.ygc) открывается:
        /// 19 операций, ожидаемые типы и порядок.
        /// </summary>
        [TestMethod]
        public void Legacy_LoadsV1ReferenceFile()
        {
            var path = Path.Combine(ReferenceOutputDirectory, "legacy_project_v1.ygc");
            Assert.IsTrue(File.Exists(path), "Нет эталонного легаси-файла Reference/legacy_project_v1.ygc");

            var loaded = Service.Load(path).Operations;
            Assert.IsNotNull(loaded, "Легаси-файл должен содержать секцию операций");
            Assert.AreEqual(19, loaded.Count, "Число операций в легаси-файле");

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
                Assert.AreEqual(expectedTypes[i], loaded[i].GetType(), $"Операция [{i}]");
        }

        /// <summary>
        /// Операции из легаси-файла v1 по полям совпадают с эталонными in-memory операциями
        /// (Fixtures/ReferenceOperations) — легаси-ридер восстанавливает все значения.
        /// </summary>
        [TestMethod]
        public void Legacy_FieldsMatchInMemoryReference()
        {
            var path = Path.Combine(ReferenceOutputDirectory, "legacy_project_v1.ygc");
            var loaded = Service.Load(path).Operations;
            var expected = ReferenceOperations.Build();

            Assert.AreEqual(expected.Count, loaded.Count, "Число операций");
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].GetType(), loaded[i].GetType(), $"Операция [{i}]: тип");
                CompareOperation($"операция[{i}] ({expected[i].GetType().Name})", expected[i], loaded[i]);
            }
        }

        /// <summary>
        /// Файл v1, сохранённый сборкой с версией (Version=9.9.9.9 в AssemblyQualifiedName),
        /// всё равно открывается: версия сборки игнорируется, тип разрешается по имени класса.
        /// Устраняет уязвимость версий, зафиксированную в п. 0.7.
        /// </summary>
        [TestMethod]
        public void Legacy_VersionedBuildAqn_StillLoads()
        {
            var json = "{\"Operations\":[{" +
                "\"Type\":\"GCodeGenerator.Models.ProfileCircleOperation, GCodeGenerator, Version=9.9.9.9, Culture=neutral, PublicKeyToken=null\"," +
                "\"Data\":\"{\\\"CenterX\\\":20,\\\"CenterY\\\":20,\\\"Radius\\\":10}\"}" +
                "]}";

            var loaded = Service.Deserialize(json).Operations;
            Assert.AreEqual(1, loaded.Count, "Операция с версией сборки должна загрузиться");
            Assert.AreEqual(typeof(ProfileCircleOperation), loaded[0].GetType());

            var circle = (ProfileCircleOperation)loaded[0];
            Assert.AreEqual(20.0, circle.CenterX, 1e-9);
            Assert.AreEqual(20.0, circle.CenterY, 1e-9);
            Assert.AreEqual(10.0, circle.Radius, 1e-9);
        }

        /// <summary>
        /// Миграция при сохранении: легаси v1 → загрузить → сохранить → файл становится v2,
        /// операции сохраняются (round-trip через v2).
        /// </summary>
        [TestMethod]
        public void Save_MigratesLegacyToV2()
        {
            var legacyPath = Path.Combine(ReferenceOutputDirectory, "legacy_project_v1.ygc");
            var loaded = Service.Load(legacyPath).Operations;
            Assert.IsNotNull(loaded);
            Assert.AreEqual(19, loaded.Count);

            var v2Path = Path.Combine(Path.GetTempPath(), "gcg_migrate_" + Guid.NewGuid().ToString("N") + ".ygc");
            try
            {
                Service.Save(v2Path, loaded, null);
                var json = File.ReadAllText(v2Path, Encoding.UTF8);
                Assert.IsTrue(json.StartsWith("{\"version\":2,\"operations\":[", StringComparison.Ordinal),
                    "Сохранённый файл должен быть в формате v2");
                Assert.IsFalse(json.Contains("\"Operations\""), "Не должно остаться легаси-секции Operations");

                var reloaded = Service.Load(v2Path).Operations;
                Assert.AreEqual(19, reloaded.Count, "Число операций после миграции");
                for (int i = 0; i < loaded.Count; i++)
                    CompareOperation($"операция[{i}]", loaded[i], reloaded[i]);
            }
            finally
            {
                if (File.Exists(v2Path))
                    File.Delete(v2Path);
            }
        }

        // ------------------------------------------------------------------
        // Секции spindle/coolant (пункт 8.2 плана, D4)
        // ------------------------------------------------------------------

        /// <summary>
        /// Сохранение с настройками: в файл пишутся секции "spindle" и "coolant"
        /// с фактическими значениями (PascalCase, как payload операций).
        /// </summary>
        [TestMethod]
        public void Save_WithSettings_WritesSpindleAndCoolantSections()
        {
            var settings = new GCodeSettings();
            settings.Spindle.SpindleSpeedRpm = 8000;
            settings.Spindle.SpindleStartCommand = "M4";
            settings.Spindle.SpindleDelaySeconds = 3.5;
            settings.Coolant.CoolantStartEnabled = false;

            var json = Service.Serialize(new List<OperationBase> { OperationFixtures.ProfileCircle() }, settings);

            Assert.IsTrue(json.Contains("\"spindle\":{\"SpindleControlEnabled\":true,"),
                "Секция spindle с payload'ом PascalCase");
            Assert.IsTrue(json.Contains("\"SpindleSpeedRpm\":8000"), "Значение шпинделя из настроек");
            Assert.IsTrue(json.Contains("\"SpindleStartCommand\":\"M4\""), "Команда шпинделя из настроек");
            Assert.IsTrue(json.Contains("\"SpindleDelaySeconds\":3.5"), "Задержка шпинделя (double)");
            Assert.IsTrue(json.Contains("\"coolant\":{\"CoolantControlEnabled\":true,"),
                "Секция coolant с payload'ом PascalCase");
            Assert.IsTrue(json.Contains("\"CoolantStartEnabled\":false"), "Значение СОЖ из настроек");
        }

        /// <summary>
        /// Сохранение без настроек (settings = null) — секции не пишутся
        /// (совместимость с вызывающими, у которых нет настроек сессии).
        /// </summary>
        [TestMethod]
        public void Save_NullSettings_NoSectionsWritten()
        {
            var json = Service.Serialize(new List<OperationBase> { OperationFixtures.ProfileCircle() }, null);

            Assert.IsFalse(json.Contains("\"spindle\""), "Секции spindle быть не должно");
            Assert.IsFalse(json.Contains("\"coolant\""), "Секции coolant быть не должно");
        }

        /// <summary>
        /// Round-trip секций: сохранить проект с кастомными spindle/coolant →
        /// открыть → значения совпадают (переоткрытие идемпотентно).
        /// </summary>
        [TestMethod]
        public void RoundTrip_SpindleCoolantSections_PreservesValues()
        {
            var settings = new GCodeSettings();
            settings.Spindle.SpindleControlEnabled = false;
            settings.Spindle.SpindleSpeedEnabled = false;
            settings.Spindle.SpindleSpeedRpm = 4500;
            settings.Spindle.SpindleStartEnabled = false;
            settings.Spindle.SpindleStartCommand = "M4";
            settings.Spindle.SpindleStopEnabled = false;
            settings.Spindle.SpindleDelayEnabled = true;
            settings.Spindle.SpindleDelaySeconds = 1.25;
            settings.Coolant.CoolantControlEnabled = false;
            settings.Coolant.CoolantStartEnabled = false;
            settings.Coolant.CoolantStopEnabled = true;

            var ops = new List<OperationBase> { OperationFixtures.ProfileCircle() };
            var filePath = Path.Combine(Path.GetTempPath(), "gcg_sections_" + Guid.NewGuid().ToString("N") + ".ygc");
            try
            {
                Service.Save(filePath, ops, settings);
                var loaded = Service.Load(filePath);

                Assert.IsNotNull(loaded.Spindle, "Секция spindle должна прочитаться");
                Assert.IsNotNull(loaded.Coolant, "Секция coolant должна прочитаться");
                Assert.AreEqual(settings.Spindle.SpindleControlEnabled, loaded.Spindle.SpindleControlEnabled);
                Assert.AreEqual(settings.Spindle.SpindleSpeedEnabled, loaded.Spindle.SpindleSpeedEnabled);
                Assert.AreEqual(settings.Spindle.SpindleSpeedRpm, loaded.Spindle.SpindleSpeedRpm);
                Assert.AreEqual(settings.Spindle.SpindleStartEnabled, loaded.Spindle.SpindleStartEnabled);
                Assert.AreEqual(settings.Spindle.SpindleStartCommand, loaded.Spindle.SpindleStartCommand);
                Assert.AreEqual(settings.Spindle.SpindleStopEnabled, loaded.Spindle.SpindleStopEnabled);
                Assert.AreEqual(settings.Spindle.SpindleDelayEnabled, loaded.Spindle.SpindleDelayEnabled);
                Assert.AreEqual(settings.Spindle.SpindleDelaySeconds, loaded.Spindle.SpindleDelaySeconds, 1e-9);
                Assert.AreEqual(settings.Coolant.CoolantControlEnabled, loaded.Coolant.CoolantControlEnabled);
                Assert.AreEqual(settings.Coolant.CoolantStartEnabled, loaded.Coolant.CoolantStartEnabled);
                Assert.AreEqual(settings.Coolant.CoolantStopEnabled, loaded.Coolant.CoolantStopEnabled);

                // Переоткрытие (идемпотентность): сохранить прочитанные секции → те же значения.
                var reloaded = Service.Load(filePath);
                Assert.AreEqual(loaded.Spindle.SpindleSpeedRpm, reloaded.Spindle.SpindleSpeedRpm);
                Assert.AreEqual(loaded.Coolant.CoolantStartEnabled, reloaded.Coolant.CoolantStartEnabled);
            }
            finally
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        /// <summary>
        /// Старый файл без секций (легаси v1): операции читаются, секции — null
        /// (в UI — сохраняются глобальные настройки, п. 8.2).
        /// </summary>
        [TestMethod]
        public void Load_OldFileWithoutSections_SectionsAreNull()
        {
            var path = Path.Combine(ReferenceOutputDirectory, "legacy_project_v1.ygc");
            var loaded = Service.Load(path);

            Assert.IsNotNull(loaded.Operations, "Операции из старого файла читаются");
            Assert.AreEqual(19, loaded.Operations.Count);
            Assert.IsNull(loaded.Spindle, "Секции spindle в старом файле нет");
            Assert.IsNull(loaded.Coolant, "Секции coolant в старом файле нет");
        }

        /// <summary>
        /// v2-файл старой схемы (без секций, как до п. 8.2) тоже открывается:
        /// операции читаются, секции — null.
        /// </summary>
        [TestMethod]
        public void Load_V2FileWithoutSections_SectionsAreNull()
        {
            var json = "{\"version\":2,\"operations\":[{\"type\":\"ProfileCircle\",\"data\":{\"CenterX\":1}}]}";
            var loaded = Service.Deserialize(json);

            Assert.AreEqual(1, loaded.Operations.Count);
            Assert.IsNull(loaded.Spindle);
            Assert.IsNull(loaded.Coolant);
        }

        /// <summary>
        /// Секция не-объект (42) — исключение (как для данных операций):
        /// обработчик ошибки — в MainViewModel.OpenProject.
        /// </summary>
        [TestMethod]
        public void Deserialize_NonObjectSpindleSection_Throws()
        {
            var json = "{\"version\":2,\"operations\":[],\"spindle\":42}";
            try
            {
                Service.Deserialize(json);
                Assert.Fail("Ожидалось исключение при не-объектной секции spindle");
            }
            catch (JsonException)
            {
                // Ожидаемо
            }

            var json2 = "{\"version\":2,\"operations\":[],\"coolant\":\"x\"}";
            try
            {
                Service.Deserialize(json2);
                Assert.Fail("Ожидалось исключение при не-объектной секции coolant");
            }
            catch (JsonException)
            {
                // Ожидаемо
            }
        }

        /// <summary>
        /// Секция с null-значением трактуется как отсутствующая (null → глобальные).
        /// </summary>
        [TestMethod]
        public void Deserialize_NullSpindleSection_ReturnsNull()
        {
            var json = "{\"version\":2,\"operations\":[],\"spindle\":null,\"coolant\":null}";
            var loaded = Service.Deserialize(json);

            Assert.IsNull(loaded.Spindle);
            Assert.IsNull(loaded.Coolant);
        }

        // ------------------------------------------------------------------
        // Сравнение операций по полям
        // ------------------------------------------------------------------

        private static void CompareOperation(string label, OperationBase a, OperationBase b)
        {
            Assert.AreEqual(a.GetType(), b.GetType(), $"{label}: тип операции");
            foreach (var prop in a.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead)
                    continue;
                CompareValues($"{label}.{prop.Name}", prop.GetValue(a), prop.GetValue(b));
            }
        }

        private static void CompareValues(string path, object a, object b)
        {
            if (a == null || b == null)
            {
                Assert.AreEqual(a, b, path);
                return;
            }

            // Числа — как double: значения Metadata после round-trip приходят Int32/Decimal.
            if (IsNumericType(a.GetType()) && IsNumericType(b.GetType()))
            {
                Assert.AreEqual(Convert.ToDouble(a, CultureInfo.InvariantCulture),
                                Convert.ToDouble(b, CultureInfo.InvariantCulture), 1e-9, path);
                return;
            }

            if (a is string || a is ValueType)
            {
                Assert.AreEqual(a, b, path);
                return;
            }

            if (a is IDictionary dictA)
            {
                var dictB = b as IDictionary;
                Assert.IsNotNull(dictB, path + ": словарь");
                Assert.AreEqual(dictA.Count, dictB.Count, path + ": число элементов словаря");
                foreach (DictionaryEntry entry in dictA)
                {
                    Assert.IsTrue(dictB.Contains(entry.Key), path + ": ключ " + entry.Key);
                    CompareValues($"{path}[{entry.Key}]", entry.Value, dictB[entry.Key]);
                }
                return;
            }

            if (a is IEnumerable listA)
            {
                var listB = b as IEnumerable;
                Assert.IsNotNull(listB, path + ": коллекция");
                var itemsA = listA.Cast<object>().ToList();
                var itemsB = listB.Cast<object>().ToList();
                Assert.AreEqual(itemsA.Count, itemsB.Count, path + ": число элементов");
                for (int i = 0; i < itemsA.Count; i++)
                    CompareValues($"{path}[{i}]", itemsA[i], itemsB[i]);
                return;
            }

            // Сложный объект — рекурсивно по public-свойствам.
            Assert.AreEqual(a.GetType(), b.GetType(), path + ": тип");
            foreach (var prop in a.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead)
                    continue;
                CompareValues($"{path}.{prop.Name}", prop.GetValue(a), prop.GetValue(b));
            }
        }

        private static bool IsNumericType(Type t)
        {
            return t == typeof(double) || t == typeof(float) || t == typeof(decimal)
                || t == typeof(int) || t == typeof(uint) || t == typeof(long) || t == typeof(ulong)
                || t == typeof(short) || t == typeof(ushort) || t == typeof(byte) || t == typeof(sbyte);
        }
    }
}
