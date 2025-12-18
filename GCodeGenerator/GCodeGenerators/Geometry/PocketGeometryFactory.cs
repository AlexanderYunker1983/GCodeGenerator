using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Geometry
{
    /// <summary>
    /// Фабрика для создания объектов геометрии карманов.
    /// </summary>
    public static class PocketGeometryFactory
    {
        /// <summary>
        /// Создает объект геометрии для операции кармана.
        /// </summary>
        /// <param name="operation">Операция кармана</param>
        /// <returns>Объект геометрии кармана</returns>
        public static IPocketGeometry Create(OperationBase operation)
        {
            if (operation == null)
            {
                throw new System.ArgumentNullException(nameof(operation));
            }

            if (operation is PocketCircleOperation circleOp)
            {
                return new CirclePocketGeometry(circleOp);
            }

            if (operation is PocketRectangleOperation rectangleOp)
            {
                return new RectanglePocketGeometry(rectangleOp);
            }

            if (operation is PocketEllipseOperation ellipseOp)
            {
                return new EllipsePocketGeometry(ellipseOp);
            }

            if (operation is PocketDxfOperation dxfOp)
            {
                return new DxfPocketGeometry(dxfOp);
            }

            throw new System.NotSupportedException($"Unsupported pocket operation: {operation.GetType().Name}");
        }
    }
}

