using System;
using System.Collections.Generic;
using GCodeGenerator.Geometry;
using GCodeGenerator.Models;
using GCodeGenerator.Operations;

namespace GCodeGenerator.Preview
{
    /// <summary>
    /// Строит плоскую схему операций (<see cref="OperationScene"/>).
    ///
    /// Раньше построитель сам разбирал типы операций: сверление — точки,
    /// профиль — контур из фабрики геометрии, чертёж — исходные полилинии.
    /// Тот же разбор повторялся в фабриках геометрии и в файле проекта,
    /// поэтому новый тип операции мог собраться, сохраниться и не появиться
    /// на схеме. Теперь очертание операции описано в каталоге вместе с
    /// остальными её свойствами, а здесь остаётся перевод очертания в фигуру
    /// схемы: замкнут ли контур и чем он рисуется.
    /// </summary>
    public static class OperationSceneBuilder
    {
        public static OperationScene Build(IEnumerable<OperationBase> operations)
        {
            var shapes = new List<OperationShape>();
            if (operations == null)
                return new OperationScene(shapes);

            foreach (var operation in operations)
            {
                if (operation == null)
                    continue;

                foreach (var outline in OperationCatalog.OutlinesOf(operation))
                {
                    if (outline?.Points == null || outline.Points.Count == 0)
                        continue;

                    var kind = outline.Kind == OperationOutlineKind.Points
                        ? OperationShapeKind.Point
                        : OperationShapeKind.Contour;

                    shapes.Add(new OperationShape(
                        operation, kind, outline.Points, IsClosed(outline.Points), outline.IsArea));
                }
            }

            return new OperationScene(shapes);
        }

        private static bool IsClosed(IReadOnlyList<(double X, double Y)> points)
        {
            if (points.Count < 2)
                return false;

            var first = points[0];
            var last = points[points.Count - 1];
            return Math.Abs(first.X - last.X) < GeometryTolerances.Vertex
                && Math.Abs(first.Y - last.Y) < GeometryTolerances.Vertex;
        }
    }
}
