#nullable enable
using System;
using System.Collections.Generic;
using GCodeGenerator.Models;

namespace GCodeGenerator.Geometry
{
    /// <summary>
    /// Плоские геометрические примитивы, общие для импорта DXF, построения
    /// контуров и генерации траектории.
    ///
    /// До выделения этого класса совпадение точек проверялось восемью
    /// одинаковыми приватными методами, пересечение отрезков решалось тремя
    /// копиями одной формулы, а площадь и центр масс контура считались
    /// дважды каждая. Формулы здесь перенесены дословно, включая порядок
    /// операций с плавающей точкой: результат генератора не должен зависеть
    /// от того, из какого места вызван расчёт.
    ///
    /// Допуски передаются параметрами, а не берутся из
    /// <see cref="GeometryTolerances"/> внутри: у импортёра и у генератора
    /// траектории они разные, и выбор остаётся за вызывающим кодом.
    /// </summary>
    public static class Geometry2D
    {
        /// <summary>Расстояние между двумя точками.</summary>
        public static double Distance(double x1, double y1, double x2, double y2)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Расстояние между двумя точками контура.</summary>
        public static double Distance(Point2D first, Point2D second)
            => Distance(first.X, first.Y, second.X, second.Y);

        /// <summary>
        /// Точки совпадают в пределах допуска. <c>null</c> не совпадает ни с чем,
        /// включая другой <c>null</c>: отсутствующая точка не является координатой.
        /// </summary>
        public static bool PointsMatch(Point2D first, Point2D second, double tolerance)
        {
            if (first == null || second == null)
                return false;
            return Distance(first, second) <= tolerance;
        }

        /// <summary>
        /// Знаковая площадь замкнутого контура (формула шнурования).
        /// Положительная — обход против часовой стрелки, отрицательная — по часовой.
        /// Контур меньше трёх точек площади не имеет.
        /// </summary>
        public static double SignedArea(IReadOnlyList<Point2D>? points)
        {
            if (points == null || points.Count < 3)
                return 0;

            double area = 0;
            for (int i = 0; i < points.Count; i++)
            {
                var p1 = points[i];
                var p2 = points[(i + 1) % points.Count];
                area += p1.X * p2.Y - p2.X * p1.Y;
            }
            return area / 2.0;
        }

        /// <summary>Площадь замкнутого контура без учёта направления обхода.</summary>
        public static double Area(IReadOnlyList<Point2D>? points)
            => Math.Abs(SignedArea(points));

        /// <summary>
        /// Центр масс (центроид) многоугольника. Если площадь контура меньше
        /// <paramref name="degenerateArea"/>, делить на неё нельзя и центр
        /// берётся как среднее арифметическое вершин.
        /// </summary>
        /// <param name="points">Вершины контура.</param>
        /// <param name="degenerateArea">Порог вырожденной площади.</param>
        public static (double x, double y) Centroid(IReadOnlyList<Point2D>? points, double degenerateArea)
        {
            if (points == null || points.Count == 0)
                return (0, 0);

            double area = 0;
            double cx = 0;
            double cy = 0;

            int pointCount = points.Count;
            for (int i = 0; i < pointCount; i++)
            {
                var p1 = points[i];
                var p2 = points[(i + 1) % pointCount];

                double cross = p1.X * p2.Y - p2.X * p1.Y;
                area += cross;
                cx += (p1.X + p2.X) * cross;
                cy += (p1.Y + p2.Y) * cross;
            }

            area *= 0.5;
            if (Math.Abs(area) > degenerateArea)
            {
                double invArea = 1.0 / (6.0 * area);
                return (cx * invArea, cy * invArea);
            }

            double sumX = 0, sumY = 0;
            foreach (var p in points)
            {
                sumX += p.X;
                sumY += p.Y;
            }
            return (sumX / pointCount, sumY / pointCount);
        }

        /// <summary>
        /// Точка внутри многоугольника (алгоритм трассировки луча).
        /// Точка ровно на границе может быть отнесена к любой стороне —
        /// вызывающий код добавляет собственный допуск, если это важно.
        /// </summary>
        public static bool IsPointInsidePolygon(double x, double y, IReadOnlyList<Point2D> points)
        {
            if (points == null || points.Count < 3)
                return false;

            bool inside = false;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
            {
                var pi = points[i];
                var pj = points[j];

                if (((pi.Y > y) != (pj.Y > y)) &&
                    (x < (pj.X - pi.X) * (y - pi.Y) / (pj.Y - pi.Y) + pi.X))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>
        /// Расстояние от точки до отрезка: проекция ограничивается концами,
        /// поэтому для точки «за» отрезком возвращается расстояние до
        /// ближайшего конца. Вырожденный отрезок обрабатывается как точка.
        /// </summary>
        /// <param name="px">X точки.</param>
        /// <param name="py">Y точки.</param>
        /// <param name="x1">X начала отрезка.</param>
        /// <param name="y1">Y начала отрезка.</param>
        /// <param name="x2">X конца отрезка.</param>
        /// <param name="y2">Y конца отрезка.</param>
        /// <param name="degenerateLength">Порог вырожденной длины отрезка.</param>
        public static double DistanceToSegment(
            double px, double py,
            double x1, double y1,
            double x2, double y2,
            double degenerateLength)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            if (Math.Abs(dx) < degenerateLength && Math.Abs(dy) < degenerateLength)
                return Math.Sqrt(Math.Pow(px - x1, 2) + Math.Pow(py - y1, 2));

            double t = ((px - x1) * dx + (py - y1) * dy) / (dx * dx + dy * dy);
            t = Math.Max(0, Math.Min(1, t));
            double projX = x1 + t * dx;
            double projY = y1 + t * dy;
            return Math.Sqrt(Math.Pow(px - projX, 2) + Math.Pow(py - projY, 2));
        }

        /// <summary>
        /// Точка пересечения двух отрезков или <c>null</c>, если отрезки
        /// параллельны либо пересекаются вне своих границ.
        ///
        /// Допуска два, потому что они отвечают за разное: параллельность
        /// определяется по определителю системы, а попадание в границы —
        /// по параметрам отрезков. Вызывающий код задаёт оба явно; там, где
        /// исторически использовалось одно значение, оно передаётся дважды.
        /// </summary>
        /// <param name="x1">X начала первого отрезка.</param>
        /// <param name="y1">Y начала первого отрезка.</param>
        /// <param name="x2">X конца первого отрезка.</param>
        /// <param name="y2">Y конца первого отрезка.</param>
        /// <param name="x3">X начала второго отрезка.</param>
        /// <param name="y3">Y начала второго отрезка.</param>
        /// <param name="x4">X конца второго отрезка.</param>
        /// <param name="y4">Y конца второго отрезка.</param>
        /// <param name="parallelTolerance">Порог определителя: ниже него отрезки считаются параллельными.</param>
        /// <param name="boundsTolerance">Допуск выхода параметра за пределы [0; 1].</param>
        public static (double x, double y)? SegmentIntersection(
            double x1, double y1, double x2, double y2,
            double x3, double y3, double x4, double y4,
            double parallelTolerance,
            double boundsTolerance)
        {
            double dx1 = x2 - x1;
            double dy1 = y2 - y1;
            double dx2 = x4 - x3;
            double dy2 = y4 - y3;

            double denom = dx1 * dy2 - dy1 * dx2;
            if (Math.Abs(denom) < parallelTolerance)
                return null;

            double t1 = ((x3 - x1) * dy2 - (y3 - y1) * dx2) / denom;
            double t2 = ((x3 - x1) * dy1 - (y3 - y1) * dx1) / denom;

            if (t1 >= -boundsTolerance && t1 <= 1.0 + boundsTolerance
                && t2 >= -boundsTolerance && t2 <= 1.0 + boundsTolerance)
            {
                t1 = Math.Max(0, Math.Min(1, t1));
                return (x1 + t1 * dx1, y1 + t1 * dy1);
            }

            return null;
        }

        /// <summary>
        /// Пересечение двух отрезков в виде точки контура (<c>null</c>, если
        /// пересечения нет). Обёртка над <see cref="SegmentIntersection"/>
        /// для кода, который работает с <see cref="Point2D"/>.
        /// </summary>
        /// <param name="x1">X начала первого отрезка.</param>
        /// <param name="y1">Y начала первого отрезка.</param>
        /// <param name="x2">X конца первого отрезка.</param>
        /// <param name="y2">Y конца первого отрезка.</param>
        /// <param name="x3">X начала второго отрезка.</param>
        /// <param name="y3">Y начала второго отрезка.</param>
        /// <param name="x4">X конца второго отрезка.</param>
        /// <param name="y4">Y конца второго отрезка.</param>
        /// <param name="parallelTolerance">Порог определителя: ниже него отрезки считаются параллельными.</param>
        /// <param name="boundsTolerance">Допуск выхода параметра за пределы [0; 1].</param>
        public static Point2D? SegmentIntersectionPoint(
            double x1, double y1, double x2, double y2,
            double x3, double y3, double x4, double y4,
            double parallelTolerance,
            double boundsTolerance)
        {
            var intersection = SegmentIntersection(
                x1, y1, x2, y2, x3, y3, x4, y4, parallelTolerance, boundsTolerance);
            if (!intersection.HasValue)
                return null;
            return new Point2D { X = intersection.Value.x, Y = intersection.Value.y };
        }

        /// <summary>
        /// Проекция точки на отрезок или <c>null</c>, если проекция выходит
        /// за его пределы дальше допуска.
        ///
        /// Отрезок нулевой длины — особый случай: проекции у него нет, но
        /// точка может совпадать с ним самим, и тогда возвращается он.
        /// </summary>
        /// <param name="px">X точки.</param>
        /// <param name="py">Y точки.</param>
        /// <param name="x1">X начала отрезка.</param>
        /// <param name="y1">Y начала отрезка.</param>
        /// <param name="x2">X конца отрезка.</param>
        /// <param name="y2">Y конца отрезка.</param>
        /// <param name="boundsTolerance">Допуск выхода параметра за пределы [0; 1].</param>
        /// <param name="degenerateLength">Длина, ниже которой отрезок считается точкой.</param>
        public static (double x, double y)? ProjectOntoSegment(
            double px, double py,
            double x1, double y1,
            double x2, double y2,
            double boundsTolerance,
            double degenerateLength)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            double lengthSquared = dx * dx + dy * dy;

            if (lengthSquared < degenerateLength * degenerateLength)
            {
                return Distance(px, py, x1, y1) < degenerateLength ? (x1, y1) : ((double x, double y)?)null;
            }

            double t = ((px - x1) * dx + (py - y1) * dy) / lengthSquared;
            if (t < -boundsTolerance || t > 1.0 + boundsTolerance)
                return null;

            t = Math.Max(0, Math.Min(1, t));
            return (x1 + t * dx, y1 + t * dy);
        }

        /// <summary>
        /// Номер вершины ломаной, ближайшей к точке; −1 для пустой ломаной.
        /// </summary>
        /// <param name="points">Вершины ломаной.</param>
        /// <param name="x">X точки.</param>
        /// <param name="y">Y точки.</param>
        public static int ClosestVertexIndex(IReadOnlyList<(double x, double y)> points, double x, double y)
        {
            if (points == null || points.Count == 0)
                return -1;

            int closest = 0;
            double minSquared = double.MaxValue;

            for (int i = 0; i < points.Count; i++)
            {
                double dx = points[i].x - x;
                double dy = points[i].y - y;
                double squared = dx * dx + dy * dy;
                if (squared < minSquared)
                {
                    minSquared = squared;
                    closest = i;
                }
            }

            return closest;
        }

        /// <summary>
        /// Ближайшая к точке точка на замкнутой ломаной вместе с номером
        /// стороны, на которой она лежит; <c>null</c>, если ломаной нет
        /// или точка не проецируется ни на одну сторону.
        ///
        /// Нужно там, где точка вычислена как пересечение и лежит на контуре
        /// лишь с точностью до погрешности: обход контура должен начинаться
        /// с настоящей точки контура, а не рядом с ним.
        /// </summary>
        /// <param name="points">Вершины замкнутой ломаной.</param>
        /// <param name="x">X точки.</param>
        /// <param name="y">Y точки.</param>
        /// <param name="boundsTolerance">Допуск выхода проекции за пределы стороны.</param>
        /// <param name="degenerateLength">Длина, ниже которой сторона считается точкой.</param>
        public static ((double x, double y) point, int segmentIndex)? ClosestPointOnClosedPolyline(
            IReadOnlyList<(double x, double y)> points,
            double x, double y,
            double boundsTolerance,
            double degenerateLength)
        {
            if (points == null || points.Count < 2)
                return null;

            double minSquared = double.MaxValue;
            (double x, double y) closest = default;
            int closestSegment = -1;

            for (int i = 0; i < points.Count; i++)
            {
                var start = points[i];
                var end = points[(i + 1) % points.Count];

                var projection = ProjectOntoSegment(
                    x, y, start.x, start.y, end.x, end.y, boundsTolerance, degenerateLength);
                if (!projection.HasValue)
                    continue;

                double dx = projection.Value.x - x;
                double dy = projection.Value.y - y;
                double squared = dx * dx + dy * dy;
                if (squared < minSquared)
                {
                    minSquared = squared;
                    closest = projection.Value;
                    closestSegment = i;
                }
            }

            if (closestSegment < 0)
                return null;

            return (closest, closestSegment);
        }
    }
}
