using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using GCodeGenerator.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Тесты ProjectFileService (пункт 0.6 плана): round-trip проекта .ygc.
    /// Сохранить фикстуру → открыть → сравнить операции по полям.
    ///
    /// Формат файла не должен меняться (старые .ygc обязаны открываться):
    /// JavaScriptSerializer, UTF-8, структура
    /// {"Operations":[{"Type":"&lt;AssemblyQualifiedName&gt;","Data":"&lt;JSON операции&gt;"}]}.
    /// Переход на System.Text.Json запланирован пунктом 1.2 (фаза 1).
    ///
    /// Нюанс сравнения: значения Metadata (Dictionary&lt;string, object&gt;) после
    /// round-trip приходят как double (JSON-число), даже если исходно были int —
    /// числа сравниваются как double.
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

        /// <summary>
        /// Все 19 операций фикстур 0.3 (9 сверл, 6 профилей, 4 кармана) —
        /// покрывают все 15 конкретных типов операций.
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
            ops[12].Name = "Профиль: окружность";
            ops[18].Name = "Карман: DXF";

            return ops;
        }

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
                Service.Save(filePath, original);
                var project = Service.Load(filePath);
                var loaded = Service.ExtractOperations(project);

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

            var json = Service.Serialize(original);
            var inMemory = Service.ExtractOperations(Service.Deserialize(json));

            var filePath = Path.Combine(Path.GetTempPath(), "gcg_roundtrip_" + Guid.NewGuid().ToString("N") + ".ygc");
            try
            {
                Service.Save(filePath, original);
                var fromFile = Service.ExtractOperations(Service.Load(filePath));

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

        /// <summary>
        /// Формат файла зафиксирован: структура {"Operations":[{Type,Data}...]}
        /// и AssemblyQualifiedName типов — старые .ygc должны оставаться читаемыми.
        /// </summary>
        [TestMethod]
        public void FileFormat_StructureIsStable()
        {
            var json = Service.Serialize(BuildAllOperations());

            Assert.IsTrue(json.StartsWith("{\"Operations\":[", StringComparison.Ordinal),
                "Файл должен начинаться с {\"Operations\":[");
            Assert.IsTrue(json.Contains("\"Type\":\"GCodeGenerator.Models."),
                "Type — AssemblyQualifiedName из GCodeGenerator.Models");
            Assert.IsTrue(json.Contains("\"Data\":\""),
                "Data — JSON-строка операции");
            Assert.AreEqual(19, json.Split(new[] { "\"Type\":\"" }, StringSplitOptions.None).Length - 1,
                "По одной записи на операцию");
        }

        /// <summary>
        /// Некорректные записи пропускаются (поведение прежнего LoadOperationsFromProject):
        /// пустой Type, пустой Data, неизвестный тип.
        /// </summary>
        [TestMethod]
        public void ExtractOperations_SkipsInvalidEntries()
        {
            var valid = OperationFixtures.ProfileCircle();
            var validJson = Service.Serialize(new[] { (OperationBase)valid });
            var validDto = Service.Deserialize(validJson).Operations.Single();

            var project = new ProjectData
            {
                Operations = new List<SerializableOperation>
                {
                    new SerializableOperation { Type = "", Data = validDto.Data },
                    new SerializableOperation { Type = validDto.Type, Data = "" },
                    new SerializableOperation { Type = "System.String, mscorlib", Data = "\"hello\"" },
                    validDto
                }
            };

            var loaded = Service.ExtractOperations(project);
            Assert.AreEqual(1, loaded.Count, "Должна пройти только валидная запись");
            Assert.AreEqual(valid.GetType(), loaded[0].GetType());
            CompareOperation("валидная операция", valid, loaded[0]);
        }

        /// <summary>
        /// Валидный тип + не-объектный JSON ("42") — JavaScriptSerializer БРОСАЕТ исключение
        /// (не возвращает null) — зафиксировано как поведение прежнего
        /// LoadOperationsFromProject: в UI это ошибка «Ошибка при загрузке проекта».
        /// </summary>
        [TestMethod]
        public void ExtractOperations_ValidTypeWithNonObjectData_Throws()
        {
            var valid = OperationFixtures.ProfileCircle();
            var validDto = Service.Deserialize(Service.Serialize(new[] { (OperationBase)valid })).Operations.Single();

            var project = new ProjectData
            {
                Operations = new List<SerializableOperation>
                {
                    new SerializableOperation { Type = validDto.Type, Data = "42" }
                }
            };

            try
            {
                Service.ExtractOperations(project);
                Assert.Fail("Ожидалось исключение при не-объектном JSON данных операции");
            }
            catch (Exception)
            {
                // Ожидаемо: обработчик ошибки — в MainViewModel.OpenProject
            }
        }

        /// <summary>
        /// Пустой проект (null/пустой Operations) — без исключений, ноль операций.
        /// </summary>
        [TestMethod]
        public void ExtractOperations_EmptyProject_ReturnsEmptyList()
        {
            Assert.AreEqual(0, Service.ExtractOperations(null).Count);
            Assert.AreEqual(0, Service.ExtractOperations(new ProjectData()).Count);
            Assert.AreEqual(0, Service.ExtractOperations(new ProjectData { Operations = new List<SerializableOperation>() }).Count);
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

            // Числа — как double: int из Metadata после round-trip приходит double.
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
