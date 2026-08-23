using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Geometry
{
    /// <summary>
    /// Фабрика для создания объектов геометрии профилей.
    /// </summary>
    public static class ProfileGeometryFactory
    {
        /// <summary>
        /// Создает объект геометрии для операции профиля.
        /// </summary>
        /// <param name="operation">Операция профиля</param>
        /// <returns>Объект геометрии профиля</returns>
        public static IProfileGeometry Create(OperationBase operation)
        {
            if (operation == null)
            {
                throw new System.ArgumentNullException(nameof(operation));
            }

            if (operation is ProfileCircleOperation circleOp)
            {
                return new CircleProfileGeometry(circleOp);
            }

            if (operation is ProfileRectangleOperation rectangleOp)
            {
                return new RectangleProfileGeometry(rectangleOp);
            }

            if (operation is ProfileRoundedRectangleOperation roundedRectangleOp)
            {
                return new RoundedRectangleProfileGeometry(roundedRectangleOp);
            }

            if (operation is ProfileEllipseOperation ellipseOp)
            {
                return new EllipseProfileGeometry(ellipseOp);
            }

            if (operation is ProfilePolygonOperation polygonOp)
            {
                return new PolygonProfileGeometry(polygonOp);
            }

            if (operation is ProfileDxfOperation dxfOp)
            {
                return new DxfProfileGeometry(dxfOp);
            }

            throw new System.NotSupportedException($"Unsupported profile operation: {operation.GetType().Name}");
        }
    }
}

