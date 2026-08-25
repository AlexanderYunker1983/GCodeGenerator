using System.Diagnostics.CodeAnalysis;
using GCodeGenerator.Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Контракт геометрических допусков. Значения перенесены из кода как есть
    /// (генератор выдаёт прежний G-code), поэтому тест фиксирует и сами числа,
    /// и соотношения между ними: изменение любого допуска — осознанное решение
    /// с пересмотром эталонных программ, а не побочный эффект правки.
    /// </summary>
    [TestClass]
    // Проверки этого класса сравнивают константы между собой, и анализатор
    // считает такие утверждения бессмысленными. Здесь они и есть цель:
    // значения допусков и порядок между ними — договорённость, на которой
    // построены эталонные программы, и правка любого из них должна ронять
    // тест, а не тихо менять G-code.
    [SuppressMessage("Usage", "MSTEST0032:Assertion condition is always true",
        Justification = "Значения допусков фиксируются намеренно")]
    public class GeometryTolerancesTests
    {
        [TestMethod]
        public void Values_MatchAgreedContract()
        {
            Assert.AreEqual(1e-3, GeometryTolerances.PointCoincidence, "Совпадение точек модели, мм");
            Assert.AreEqual(1e-6, GeometryTolerances.Vertex, "Совпадение вершин траектории, мм");
            Assert.AreEqual(1e-6, GeometryTolerances.Containment, "Принадлежность точки контуру, мм");
            Assert.AreEqual(1e-9, GeometryTolerances.Degenerate, "Порог вырожденности");
            Assert.AreEqual(1e-4, GeometryTolerances.Position, "Изменение положения инструмента, мм");
            Assert.AreEqual(1e-3, GeometryTolerances.MinimumContourExtent, "Минимальный размер контура, мм");
        }

        /// <summary>
        /// Порядок допусков — часть контракта: вырожденность строже совпадения
        /// вершин, а вершины строже допуска исходных данных. Нарушение порядка
        /// означало бы, что точки, различимые для импортёра, неразличимы для
        /// генератора (или наоборот).
        /// </summary>
        [TestMethod]
        public void Order_FromStrictestToLoosest()
        {
            Assert.IsTrue(GeometryTolerances.Degenerate < GeometryTolerances.Vertex,
                "Порог вырожденности должен быть строже допуска совпадения вершин");
            Assert.IsTrue(GeometryTolerances.Vertex < GeometryTolerances.Position,
                "Совпадение вершин должно быть строже порога перемещения инструмента");
            Assert.IsTrue(GeometryTolerances.Position < GeometryTolerances.PointCoincidence,
                "Перемещение инструмента должно различаться точнее, чем координаты исходной модели");
        }

        /// <summary>
        /// Проверка замкнутости контура операции обязана совпадать с допуском
        /// импортёра DXF, иначе импортированный контур мог бы не пройти
        /// собственную валидацию.
        /// </summary>
        [TestMethod]
        public void ContourValidation_UsesImportTolerance()
        {
            Assert.AreEqual(
                GeometryTolerances.PointCoincidence,
                GCodeGenerator.Models.OperationValidation.ContourClosedTolerance);
        }
    }
}
