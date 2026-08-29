#nullable enable
using System;
using System.Collections.Generic;
using GCodeGenerator.Models;

namespace GCodeGenerator.Geometry
{
    /// <summary>
    /// Пространственный индекс точек по квадратным ячейкам допуска. Поиск
    /// просматривает только текущую и восемь соседних ячеек, но среди всех
    /// совпадений возвращает самое раннее добавленное — так оптимизация не
    /// меняет детерминированный порядок прежнего линейного поиска.
    /// </summary>
    internal sealed class SpatialPointIndex<T>
    {
        private readonly double _tolerance;
        private readonly Dictionary<(double X, double Y), List<Entry>> _cells = new();
        private long _nextOrder;

        internal SpatialPointIndex(double tolerance)
        {
            if (!double.IsFinite(tolerance) || tolerance <= 0)
                throw new ArgumentOutOfRangeException(nameof(tolerance));
            _tolerance = tolerance;
        }

        internal void Add(Point2D point, T value)
        {
            var cell = Cell(point);
            if (!_cells.TryGetValue(cell, out var entries))
            {
                entries = new List<Entry>();
                _cells[cell] = entries;
            }

            entries.Add(new Entry(point, value, _nextOrder++));
        }

        internal bool TryFindFirst(Point2D point, Predicate<T>? predicate, out T value)
        {
            var center = Cell(point);
            Entry? first = null;

            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    if (!_cells.TryGetValue((center.X + dx, center.Y + dy), out var entries))
                        continue;

                    foreach (var entry in entries)
                    {
                        if ((predicate == null || predicate(entry.Value))
                            && Geometry2D.PointsMatch(entry.Point, point, _tolerance)
                            && (first == null || entry.Order < first.Order))
                        {
                            first = entry;
                        }
                    }
                }
            }

            if (first != null)
            {
                value = first.Value;
                return true;
            }

            value = default!;
            return false;
        }

        private (double X, double Y) Cell(Point2D point)
            => (Math.Floor(point.X / _tolerance), Math.Floor(point.Y / _tolerance));

        private sealed class Entry
        {
            internal Entry(Point2D point, T value, long order)
            {
                Point = point;
                Value = value;
                Order = order;
            }

            internal Point2D Point { get; }
            internal T Value { get; }
            internal long Order { get; }
        }
    }
}
