using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using GCodeGenerator.Models;
using GCodeGenerator.Operations;
using GCodeGenerator.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GCodeGenerator.Persistence;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Тесты ProjectFileService (пункты 0.6 и 1.2 плана): round-trip проекта .ygc.
    ///
    /// Текущий формат v4 (System.Text.Json), в который всегда сохраняется:
    /// {"version":4,"operations":[{"type":"&lt;короткое имя&gt;","data":{...}}]}.
    /// Легаси-формат v1 (JavaScriptSerializer) — только чтение:
    /// {"Operations":[{"Type":"&lt;AssemblyQualifiedName&gt;","Data":"&lt;JSON операции&gt;"}]}.
    ///
    /// Форматы v2/v3 и легаси v1 читаются для совместимости.
    /// </summary>
    [TestClass]
    public class ProjectFileServiceTests
    {
        private static ProjectFileService Service { get; } = new ProjectFileService();

        /// <summary>
        /// Чтение и запись проекта принадлежат ядру, а не приложению: формат
        /// описывает доменные операции, и открыть проект нужно уметь без
        /// интерфейсной сборки — для консольных сценариев и при смене
        /// интерфейсного стека.
        /// </summary>
        [TestMethod]
        public void ProjectFile_LivesInCoreAssembly()
        {
            var coreAssembly = typeof(OperationBase).Assembly;

            Assert.AreEqual(coreAssembly, typeof(ProjectFileService).Assembly);
            Assert.AreEqual(coreAssembly, typeof(IProjectFileService).Assembly);
            Assert.AreEqual(coreAssembly, typeof(ProjectFileData).Assembly);
            Assert.AreEqual(coreAssembly, typeof(OperationTypeNames).Assembly);
        }

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
            var helicalPocket = (PocketCircleOperation)ops[16];
            helicalPocket.EntryMode = PocketEntryMode.Helical;
            helicalPocket.EntryAngle = 7.5;
            helicalPocket.HelicalEntryDiameter = 4.25;
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

        [TestMethod]
        public void RoundTrip_PreservesPocketIslandMode_AndOldFileDefaultsToMachining()
        {
            var island = OperationFixtures.PocketDxf();
            island.PocketMode = PocketMode.Island;

            var loaded = (PocketDxfOperation)Service.Deserialize(
                Service.Serialize(new OperationBase[] { island }, null)).Operations[0];
            Assert.AreEqual(PocketMode.Island, loaded.PocketMode);

            var oldJson = "{\"version\":4,\"operations\":[{\"type\":\"PocketCircle\",\"data\":{" +
                "\"Radius\":10,\"Name\":\"Старый карман\",\"IsEnabled\":true}}]}";
            var oldPocket = (PocketCircleOperation)Service.Deserialize(oldJson).Operations[0];
            Assert.AreEqual(PocketMode.Machining, oldPocket.PocketMode);
        }

        /// <summary>
        /// Требование латиницы в комментариях включено и для файлов, записанных
        /// до его появления: поля они не содержат, и значение остаётся тем, что
        /// объявлено в настройках. Иначе старый проект, открытый новой сборкой,
        /// молча продолжил бы отправлять кириллицу на стойку — а именно от
        /// этого настройка и защищает.
        /// </summary>
        [TestMethod]
        public void OldFile_WithoutAsciiComments_DefaultsToRequiringThem()
        {
            var withoutField = "{\"version\":4,\"operations\":[],\"format\":{" +
                "\"UseLineNumbers\":true,\"UseComments\":true,\"AllowArcs\":true}}";

            var format = Service.Deserialize(withoutField).Format;

            Assert.IsNotNull(format);
            Assert.IsTrue(format!.AsciiOnlyComments,
                "Файл без этого поля читается с требованием латиницы");
        }

        /// <summary>
        /// Явно выключенное требование переживает сохранение и чтение: стойки,
        /// понимающие UTF-8, настраиваются один раз.
        /// </summary>
        [TestMethod]
        public void DisabledAsciiComments_SurviveRoundTrip()
        {
            var settings = new GCodeSettings();
            settings.Format.AsciiOnlyComments = false;

            var loaded = Service.Deserialize(
                Service.Serialize(Array.Empty<OperationBase>(), settings)).Format;

            Assert.IsNotNull(loaded);
            Assert.IsFalse(loaded!.AsciiOnlyComments);
        }

        // ------------------------------------------------------------------
        // Текущий формат v4
        // ------------------------------------------------------------------

        /// <summary>
        /// Формат v4 зафиксирован: конверт {"version":4,"operations":[{type,data}...]},
        /// короткий дискриминатор типа, данные операции — вложенный JSON-объект.
        /// </summary>
        [TestMethod]
        public void FileFormat_V4Structure()
        {
            var json = Service.Serialize(BuildAllOperations(), null);

            Assert.IsTrue(json.StartsWith("{\"version\":4,\"operations\":[", StringComparison.Ordinal),
                "Файл должен начинаться с {\"version\":4,\"operations\":[");
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
        public void Deserialize_LegacyPropertyOrder_IsRead()
        {
            // Порядок полей в файле, сохранённом до вынесения общих параметров
            // резания в базовый класс: подачи и глубины шли вперемешку с
            // собственными параметрами операции. Формат от порядка не зависит,
            // и такие файлы должны читаться дословно.
            var json = "{\"version\":4,\"operations\":[{\"type\":\"PocketCircle\",\"data\":{" +
                "\"PocketStrategy\":1,\"Direction\":0,\"CenterX\":20,\"CenterY\":20,\"Radius\":10," +
                "\"TotalDepth\":7,\"StepDepth\":1.5,\"ToolDiameter\":4,\"ContourHeight\":-1," +
                "\"FeedXYRapid\":1100,\"FeedXYWork\":320,\"FeedZRapid\":510,\"FeedZWork\":210," +
                "\"SafeZHeight\":2,\"RetractHeight\":0.4,\"StepPercentOfTool\":45,\"Decimals\":4," +
                "\"Name\":\"Карман\",\"IsEnabled\":true}}]}";

            var operations = Service.Deserialize(json).Operations;

            Assert.AreEqual(1, operations.Count);
            var pocket = (PocketCircleOperation)operations[0];
            Assert.AreEqual(10.0, pocket.Radius, 1e-9);
            Assert.AreEqual(7.0, pocket.TotalDepth, 1e-9, "Глубина из файла");
            Assert.AreEqual(1.5, pocket.StepDepth, 1e-9);
            Assert.AreEqual(-1.0, pocket.ContourHeight, 1e-9);
            Assert.AreEqual(2.0, pocket.SafeZHeight, 1e-9);
            Assert.AreEqual(0.4, pocket.RetractHeight, 1e-9);
            Assert.AreEqual(1100.0, pocket.FeedXYRapid, 1e-9);
            Assert.AreEqual(320.0, pocket.FeedXYWork, 1e-9);
            Assert.AreEqual(510.0, pocket.FeedZRapid, 1e-9);
            Assert.AreEqual(210.0, pocket.FeedZWork, 1e-9);
            Assert.AreEqual(4.0, pocket.ToolDiameter, 1e-9);
            Assert.AreEqual(4, pocket.Decimals);
            Assert.AreEqual(PocketEntryMode.Vertical, pocket.EntryMode,
                "в старом файле без настройки подвода сохраняется прежний вертикальный вход");
        }

        [TestMethod]
        public void Deserialize_UnsupportedVersion_Throws()
        {
            var newer = "{\"version\":5,\"operations\":[{\"type\":\"ProfileCircle\",\"data\":{}}]}";
            var olderTagged = "{\"version\":1,\"operations\":[]}";

            // Отказ несёт код: по нему интерфейс подбирает перевод.
            Assert.AreEqual(CoreErrorCodes.ProjectFileUnsupportedVersion,
                Assert.Throws<CoreException>(() => Service.Deserialize(newer)).Code);
            Assert.AreEqual(CoreErrorCodes.ProjectFileUnsupportedVersion,
                Assert.Throws<CoreException>(() => Service.Deserialize(olderTagged)).Code);
        }

        [TestMethod]
        public void Deserialize_UnknownRootSection_Throws()
        {
            const string json = "{\"version\":2,\"operations\":[],\"futureData\":{\"keep\":true}}";

            var failure = Assert.Throws<CoreException>(() => Service.Deserialize(json));

            Assert.AreEqual(CoreErrorCodes.ProjectFileUnknownSection, failure.Code);
            StringAssert.Contains(failure.Message, "futureData");
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
            Assert.IsNull(Service.Deserialize("{\"version\":2}").Operations, "v2 без operations");
            Assert.IsNull(Service.Deserialize("{\"version\":2,\"operations\":null}").Operations, "v2 operations=null");
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
        // Настройки генерации в проекте
        // ------------------------------------------------------------------

        /// <summary>
        /// Сохранение с настройками: в файл пишутся все четыре группы, влияющие
        /// на G-code. UI-настройки в проект не попадают.
        /// </summary>
        [TestMethod]
        public void Save_WithSettings_WritesAllGenerationSections()
        {
            var settings = new GCodeSettings();
            settings.Format.UseLineNumbers = false;
            settings.Spindle.SpindleSpeedRpm = 8000;
            settings.Spindle.SpindleStartCommand = "M4";
            settings.Spindle.SpindleDelaySeconds = 3.5;
            settings.Coolant.CoolantStartEnabled = false;
            settings.WorkCoordinate.SetWorkCoordinateSystem = true;
            settings.WorkCoordinate.WorkCoordinateSystem = "G57";
            settings.Ui.UseDarkTheme = true;

            var json = Service.Serialize(new List<OperationBase> { OperationFixtures.ProfileCircle() }, settings);

            Assert.IsTrue(json.Contains("\"format\":{\"UseLineNumbers\":false,"),
                "Секция format с фактическим значением");
            Assert.IsTrue(json.Contains("\"spindle\":{\"SpindleControlEnabled\":true,"),
                "Секция spindle с payload'ом PascalCase");
            Assert.IsTrue(json.Contains("\"SpindleSpeedRpm\":8000"), "Значение шпинделя из настроек");
            Assert.IsTrue(json.Contains("\"SpindleStartCommand\":\"M4\""), "Команда шпинделя из настроек");
            Assert.IsTrue(json.Contains("\"SpindleDelaySeconds\":3.5"), "Задержка шпинделя (double)");
            Assert.IsTrue(json.Contains("\"coolant\":{\"CoolantControlEnabled\":true,"),
                "Секция coolant с payload'ом PascalCase");
            Assert.IsTrue(json.Contains("\"CoolantStartEnabled\":false"), "Значение СОЖ из настроек");
            Assert.IsTrue(json.Contains("\"workCoordinate\":{\"AddStartPosition\":false,"),
                "Секция workCoordinate с payload'ом PascalCase");
            Assert.IsTrue(json.Contains("\"WorkCoordinateSystem\":\"G57\""),
                "Рабочая система координат из настроек");
            Assert.IsFalse(json.Contains("\"ui\"", StringComparison.OrdinalIgnoreCase),
                "Настройки UI не должны зависеть от проекта");
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
            Assert.IsFalse(json.Contains("\"format\""), "Секции format быть не должно");
            Assert.IsFalse(json.Contains("\"workCoordinate\""), "Секции workCoordinate быть не должно");
        }

        /// <summary>
        /// Round-trip секций: сохранить проект со всеми настройками генерации →
        /// открыть → значения совпадают (переоткрытие идемпотентно).
        /// </summary>
        [TestMethod]
        public void RoundTrip_AllGenerationSections_PreservesValues()
        {
            var settings = new GCodeSettings();
            settings.Format.UseLineNumbers = false;
            settings.Format.LineNumberStart = 25;
            settings.Format.LineNumberStep = 5;
            settings.Format.UseComments = false;
            settings.Format.AllowArcs = false;
            settings.Format.UsePaddedGCodes = true;
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
            settings.WorkCoordinate.AddStartPosition = true;
            settings.WorkCoordinate.StartX = 1.25;
            settings.WorkCoordinate.StartY = -2.5;
            settings.WorkCoordinate.StartZ = 3.75;
            settings.WorkCoordinate.AddEndPosition = true;
            settings.WorkCoordinate.EndX = 10;
            settings.WorkCoordinate.EndY = 20;
            settings.WorkCoordinate.EndZ = 30;
            settings.WorkCoordinate.SetWorkCoordinateSystem = true;
            settings.WorkCoordinate.WorkCoordinateSystem = "G58";

            var ops = new List<OperationBase> { OperationFixtures.ProfileCircle() };
            var filePath = Path.Combine(Path.GetTempPath(), "gcg_sections_" + Guid.NewGuid().ToString("N") + ".ygc");
            try
            {
                Service.Save(filePath, ops, settings);
                var loaded = Service.Load(filePath);

                Assert.IsNotNull(loaded.Format, "Секция format должна прочитаться");
                Assert.IsNotNull(loaded.Spindle, "Секция spindle должна прочитаться");
                Assert.IsNotNull(loaded.Coolant, "Секция coolant должна прочитаться");
                Assert.IsNotNull(loaded.WorkCoordinate, "Секция workCoordinate должна прочитаться");
                Assert.AreEqual(settings.Format.UseLineNumbers, loaded.Format.UseLineNumbers);
                Assert.AreEqual(settings.Format.LineNumberStart, loaded.Format.LineNumberStart);
                Assert.AreEqual(settings.Format.LineNumberStep, loaded.Format.LineNumberStep);
                Assert.AreEqual(settings.Format.UseComments, loaded.Format.UseComments);
                Assert.AreEqual(settings.Format.AllowArcs, loaded.Format.AllowArcs);
                Assert.AreEqual(settings.Format.UsePaddedGCodes, loaded.Format.UsePaddedGCodes);
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
                Assert.AreEqual(settings.WorkCoordinate.AddStartPosition, loaded.WorkCoordinate.AddStartPosition);
                Assert.AreEqual(settings.WorkCoordinate.StartX, loaded.WorkCoordinate.StartX, 1e-9);
                Assert.AreEqual(settings.WorkCoordinate.StartY, loaded.WorkCoordinate.StartY, 1e-9);
                Assert.AreEqual(settings.WorkCoordinate.StartZ, loaded.WorkCoordinate.StartZ, 1e-9);
                Assert.AreEqual(settings.WorkCoordinate.AddEndPosition, loaded.WorkCoordinate.AddEndPosition);
                Assert.AreEqual(settings.WorkCoordinate.EndX, loaded.WorkCoordinate.EndX, 1e-9);
                Assert.AreEqual(settings.WorkCoordinate.EndY, loaded.WorkCoordinate.EndY, 1e-9);
                Assert.AreEqual(settings.WorkCoordinate.EndZ, loaded.WorkCoordinate.EndZ, 1e-9);
                Assert.AreEqual(settings.WorkCoordinate.SetWorkCoordinateSystem, loaded.WorkCoordinate.SetWorkCoordinateSystem);
                Assert.AreEqual(settings.WorkCoordinate.WorkCoordinateSystem, loaded.WorkCoordinate.WorkCoordinateSystem);

                // Переоткрытие (идемпотентность): сохранить прочитанные секции → те же значения.
                var reloaded = Service.Load(filePath);
                Assert.AreEqual(loaded.Spindle.SpindleSpeedRpm, reloaded.Spindle.SpindleSpeedRpm);
                Assert.AreEqual(loaded.Coolant.CoolantStartEnabled, reloaded.Coolant.CoolantStartEnabled);
                Assert.AreEqual(loaded.Format.UsePaddedGCodes, reloaded.Format.UsePaddedGCodes);
                Assert.AreEqual(loaded.WorkCoordinate.WorkCoordinateSystem, reloaded.WorkCoordinate.WorkCoordinateSystem);
            }
            finally
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        /// <summary>
        /// Файл первой версии формата больше не читается, и отказ объясняет,
        /// что делать: такие файлы писались до появления поля версии, их
        /// поддержка удалена вместе с миграцией.
        /// </summary>
        [TestMethod]
        public void Load_FirstFormatVersion_IsRefusedWithExplanation()
        {
            var legacy = "{\"Operations\":[{\"Type\":\"GCodeGenerator.Models.ProfileCircleOperation, GCodeGenerator\","
                + "\"Data\":\"{\\\"Radius\\\":10}\"}]}";

            var failure = Assert.Throws<CoreException>(() => Service.Deserialize(legacy));

            Assert.AreEqual(CoreErrorCodes.ProjectFileLegacyVersion, failure.Code);
            StringAssert.Contains(failure.Message, "first-format");
        }

        /// <summary>
        /// Чужой или пустой JSON-объект — тоже отказ: без версии формата
        /// нельзя понять, что перед нами.
        /// </summary>
        [TestMethod]
        public void Deserialize_ObjectWithoutVersion_IsRefused()
        {
            Assert.AreEqual(CoreErrorCodes.ProjectFileLegacyVersion,
                Assert.Throws<CoreException>(() => Service.Deserialize("{}")).Code);
            Assert.AreEqual(CoreErrorCodes.ProjectFileLegacyVersion,
                Assert.Throws<CoreException>(() => Service.Deserialize("{\"Foo\":\"bar\"}")).Code);
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
            Assert.IsNull(loaded.Format);
            Assert.IsNull(loaded.WorkCoordinate);
        }

        /// <summary>
        /// Секция не-объект (42) — исключение (как для данных операций):
        /// обработчик ошибки — в MainViewModel.OpenProject.
        /// </summary>
        [TestMethod]
        public void Deserialize_NonObjectSettingsSection_Throws()
        {
            var sections = new[]
            {
                "{\"version\":2,\"operations\":[],\"spindle\":42}",
                "{\"version\":3,\"operations\":[],\"format\":true}",
                "{\"version\":3,\"operations\":[],\"workCoordinate\":[]}",
                "{\"version\":2,\"operations\":[],\"coolant\":\"x\"}",
            };

            foreach (var json in sections)
            {
                Assert.AreEqual(CoreErrorCodes.ProjectFileCorrupt,
                    Assert.Throws<CoreException>(() => Service.Deserialize(json)).Code, json);
            }
        }

        /// <summary>
        /// Секция с null-значением трактуется как отсутствующая (null → глобальные).
        /// </summary>
        [TestMethod]
        public void Deserialize_NullSpindleSection_ReturnsNull()
        {
            var json = "{\"version\":3,\"operations\":[],\"format\":null,\"spindle\":null,\"coolant\":null,\"workCoordinate\":null}";
            var loaded = Service.Deserialize(json);

            Assert.IsNull(loaded.Spindle);
            Assert.IsNull(loaded.Coolant);
            Assert.IsNull(loaded.Format);
            Assert.IsNull(loaded.WorkCoordinate);
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

                // Идентификатор — идентичность операции на время жизни
                // процесса, а не данные файла: он не сериализуется, и у двух
                // независимых загрузок он законно разный.
                if (prop.Name == nameof(OperationBase.Id) && prop.DeclaringType == typeof(OperationBase))
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

            // Числовые свойства сравниваем как double независимо от конкретного CLR-типа.
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

                // Идентификатор — идентичность операции на время жизни
                // процесса, а не данные файла: он не сериализуется, и у двух
                // независимых загрузок он законно разный.
                if (prop.Name == nameof(OperationBase.Id) && prop.DeclaringType == typeof(OperationBase))
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

        // ------------------------------------------------------------------
        // Состав payload: вычисляемое состояние проверки — не часть формата

        /// <summary>
        /// Вычисляемое HasErrors не попадает в файл ни у одного типа каталога:
        /// это состояние проверки, а не параметр операции, и его запись
        /// попутно запускала бы саму проверку при каждом сохранении.
        /// Проверка нарочно не опирается на эталонный файл, поэтому переживёт
        /// его перегенерацию: даже нарочно обновлённый эталон с утечкой
        /// этот тест назовёт по имени типа.
        /// </summary>
        [TestMethod]
        public void Serialize_DoesNotEmitValidationState()
        {
            foreach (var descriptor in OperationCatalog.All)
            {
                var json = Service.Serialize(new[] { descriptor.Create() }, null);
                Assert.IsFalse(json.Contains("\"HasErrors\""),
                    $"{descriptor.OperationType.Name}: вычисляемое HasErrors утекло в файл проекта.");
            }
        }

        /// <summary>
        /// Прочитанная версия формата сообщается вызывающему: по ней
        /// приложение решает, предупреждать ли, что файл старой версии после
        /// сохранения станет файлом текущей и перестанет открываться прежними
        /// сборками. Прежде версия разбиралась и выбрасывалась.
        /// </summary>
        [TestMethod]
        public void Deserialize_ReportsFormatVersion()
        {
            Assert.AreEqual(2, Service.Deserialize("{\"version\":2,\"operations\":[]}").Version);

            var current = Service.Serialize(new OperationBase[] { new ProfileCircleOperation() }, null);
            Assert.AreEqual(ProjectFileService.CurrentVersion, Service.Deserialize(current).Version);
        }

        /// <summary>
        /// Файлы, сохранённые сборками с утечкой HasErrors, открываются:
        /// лишнее поле в данных операции пропускается, как и прочие
        /// неизвестные поля payload, — целиком-отказ действует только
        /// на уровне конверта.
        /// </summary>
        [TestMethod]
        public void Load_PayloadWithLeakedHasErrors_IsAccepted()
        {
            var json = "{\"version\":4,\"operations\":[{\"type\":\"ProfileCircle\"," +
                       "\"data\":{\"Radius\":7,\"HasErrors\":false}}]}";

            var data = Service.Deserialize(json);

            var circle = (ProfileCircleOperation)data.Operations.Single();
            Assert.AreEqual(7.0, circle.Radius, 1e-12);
        }
    }
}
