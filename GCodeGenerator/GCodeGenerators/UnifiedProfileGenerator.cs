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

            // После входа мы находимся на начальной точке контура
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
                    GenerateContourFromPoints(op, geometry, toolOffset, startPoint, addLine, g1, settings);
                }
            }
            else
            {
                // Генерируем из точек
                GenerateContourFromPoints(op, geometry, toolOffset, startPoint, addLine, g1, settings);
            }
        }

        /// <summary>
        /// Генерирует контур из точек.
        /// </summary>
        private void GenerateContourFromPoints(
            IProfileOperation op,
            IProfileGeometry geometry,
            double toolOffset,
            (double x, double y) currentPosition,
            Action<string> addLine,
            string g1,
            GCodeSettings settings)
        {
            var fmt = $"0.{new string('0', op.Decimals)}";
            var culture = CultureInfo.InvariantCulture;

            var points = geometry.GetContourPoints(toolOffset, op.Direction).ToList();
            
            if (points.Count == 0)
                return;

            // Находим ближайшую точку к текущей позиции в списке точек
            // Это нужно, потому что после входа мы находимся на startPoint,
            // который может не совпадать с points[0] для некоторых направлений
            int currentIndex = 0;
            double tolerance = 1e-6;
            double minDistance = double.MaxValue;
            
            for (int i = 0; i < points.Count; i++)
            {
                double dx = points[i].x - currentPosition.x;
                double dy = points[i].y - currentPosition.y;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                
                if (distance < minDistance)
                {
                    minDistance = distance;
                    currentIndex = i;
                }
            }

            // Если мы находимся очень близко к найденной точке, начинаем со следующей
            // Иначе обрабатываем все точки с начала
            int startIndex = (minDistance < tolerance && currentIndex < points.Count - 1) 
                ? currentIndex + 1 
                : 0;

            // Обрабатываем все точки контура, начиная с startIndex
            // Последняя точка в списке - это замыкающая (дубликат первой), её тоже обрабатываем
            // Это гарантирует, что все вершины будут обработаны и контур будет замкнут
            for (int i = startIndex; i < points.Count; i++)
            {
                var point = points[i];
                addLine($"{g1} X{point.x.ToString(fmt, culture)} Y{point.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
            }
            
            // Если мы начали не с начала, обрабатываем точки от начала до startIndex
            // Это нужно для замыкания контура, если мы начали не с первой точки
            if (startIndex > 0)
            {
                // Обрабатываем точки от начала до startIndex (не включая startIndex, так как мы его уже обработали)
                for (int i = 0; i < startIndex; i++)
                {
                    var point = points[i];
                    addLine($"{g1} X{point.x.ToString(fmt, culture)} Y{point.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                }
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

