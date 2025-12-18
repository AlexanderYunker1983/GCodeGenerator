using System;
using System.Globalization;
using System.Linq;
using GCodeGenerator.GCodeGenerators.Geometry;
using GCodeGenerator.GCodeGenerators.Helpers;
using GCodeGenerator.GCodeGenerators.Interfaces;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    /// <summary>
    /// Единый генератор для всех типов профилей.
    /// Использует интерфейсы геометрии и классы-помощники для унификации логики.
    /// </summary>
    public class UnifiedProfileGenerator : IOperationGenerator
    {
        private readonly ProfileGenerationHelper _helper;

        public UnifiedProfileGenerator()
        {
            _helper = new ProfileGenerationHelper();
        }

        public void Generate(
            OperationBase operation,
            Action<string> addLine,
            string g0,
            string g1,
            GCodeSettings settings)
        {
            // Проверяем, что операция является профилем
            if (!(operation is IProfileOperation profileOp))
                return;

            // Создаем геометрию профиля
            var geometry = ProfileGeometryFactory.Create(operation);
            if (geometry == null)
                return;

            // Вычисляем смещение инструмента
            var toolRadius = profileOp.ToolDiameter / 2.0;
            var toolOffset = GCodeGenerationHelper.CalculateToolOffset(profileOp.ToolPathMode, toolRadius);

            // Генерируем цикл по слоям
            _helper.GenerateLayerLoop(
                profileOp,
                (currentZ, nextZ, passNumber) => GenerateLayer(
                    profileOp,
                    geometry,
                    toolOffset,
                    currentZ,
                    nextZ,
                    addLine,
                    g0,
                    g1,
                    settings),
                addLine,
                g0,
                g1,
                settings);
        }

        /// <summary>
        /// Генерирует один слой профиля.
        /// </summary>
        private void GenerateLayer(
            IProfileOperation op,
            IProfileGeometry geometry,
            double toolOffset,
            double currentZ,
            double nextZ,
            Action<string> addLine,
            string g0,
            string g1,
            GCodeSettings settings)
        {
            var fmt = $"0.{new string('0', op.Decimals)}";
            var culture = CultureInfo.InvariantCulture;

            // Получаем начальную точку контура
            var startPoint = geometry.GetStartPoint(toolOffset);

            // Генерируем вход в материал
            _helper.GenerateEntry(
                op,
                startPoint,
                currentZ,
                nextZ,
                distance => geometry.GetPointOnContour(distance, toolOffset),
                () => geometry.GetPerimeter(toolOffset),
                addLine,
                g0,
                g1,
                settings);

            // Если был рамповый вход, нужно вернуться к начальной точке
            if (op.EntryMode == EntryMode.Angled)
            {
                // Вход уже обработан в GenerateEntry, продолжаем с начальной точки
            }

            // Генерируем путь по контуру
            if (settings.AllowArcs && geometry.SupportsArcs)
            {
                // Используем дуги, если поддерживаются
                var arcSegments = geometry.GetArcSegments(toolOffset).ToList();
                if (arcSegments.Count > 0)
                {
                    GenerateContourWithArcs(op, geometry, toolOffset, arcSegments, addLine, g1, settings);
                }
                else
                {
                    // Fallback на точки, если дуги не доступны
                    GenerateContourFromPoints(op, geometry, toolOffset, addLine, g1, settings);
                }
            }
            else
            {
                // Генерируем из точек
                GenerateContourFromPoints(op, geometry, toolOffset, addLine, g1, settings);
            }
        }

        /// <summary>
        /// Генерирует контур из точек.
        /// </summary>
        private void GenerateContourFromPoints(
            IProfileOperation op,
            IProfileGeometry geometry,
            double toolOffset,
            Action<string> addLine,
            string g1,
            GCodeSettings settings)
        {
            var fmt = $"0.{new string('0', op.Decimals)}";
            var culture = CultureInfo.InvariantCulture;

            var points = geometry.GetContourPoints(toolOffset, op.Direction).ToList();
            
            // Пропускаем первую точку, так как мы уже на ней после входа
            for (int i = 1; i < points.Count; i++)
            {
                var point = points[i];
                addLine($"{g1} X{point.x.ToString(fmt, culture)} Y{point.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
            }
        }

        /// <summary>
        /// Генерирует контур с использованием дуг.
        /// </summary>
        private void GenerateContourWithArcs(
            IProfileOperation op,
            IProfileGeometry geometry,
            double toolOffset,
            System.Collections.Generic.List<IArcSegment> arcSegments,
            Action<string> addLine,
            string g1,
            GCodeSettings settings)
        {
            var fmt = $"0.{new string('0', op.Decimals)}";
            var culture = CultureInfo.InvariantCulture;

            var g2 = settings.UsePaddedGCodes ? "G02" : "G2";
            var g3 = settings.UsePaddedGCodes ? "G03" : "G3";

            foreach (var arc in arcSegments)
            {
                var arcCommand = arc.IsClockwise ? g2 : g3;
                var i = arc.Center.x - arc.StartPoint.x;
                var j = arc.Center.y - arc.StartPoint.y;

                addLine($"{arcCommand} X{arc.EndPoint.x.ToString(fmt, culture)} Y{arc.EndPoint.y.ToString(fmt, culture)} I{i.ToString(fmt, culture)} J{j.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
            }
        }
    }
}

