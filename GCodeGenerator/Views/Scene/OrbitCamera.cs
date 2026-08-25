#nullable enable
using System;
using System.Windows.Media.Media3D;

namespace GCodeGenerator.Views.Scene
{
    /// <summary>
    /// Камера, вращающаяся вокруг точки: положение, направление взгляда и
    /// «верх» кадра для трёхмерного окна траектории.
    ///
    /// Прежде эти вычисления жили в коде окна вперемешку с обработкой мыши,
    /// и проверить их можно было только вручную, вращая сцену на запущенной
    /// программе. Разделение сцены на построитель мешей, палитру и геометрию
    /// уже показало, что этот код проверяется без окна; управление камерой
    /// оставалось последним нетронутым куском.
    ///
    /// Здесь только состояние камеры и правила его изменения: откуда пришло
    /// движение — от колеса, кнопки мыши или клавиши, — камера не знает.
    /// </summary>
    internal sealed class OrbitCamera
    {
        /// <summary>Наклон не доходит до полюса: иначе кадр переворачивается.</summary>
        private const double MinPolarAngle = 0.1;

        /// <summary>Во сколько раз меняется расстояние за один щелчок колеса.</summary>
        private const double ZoomStep = 1.1;

        /// <summary>Пределы расстояния до цели, мм.</summary>
        private const double MinDistance = 0.1;
        private const double MaxDistance = 10000;

        /// <summary>Поворот в радианах на пиксель движения мыши.</summary>
        private const double RotationSpeed = 0.01;

        /// <summary>Сдвиг за пиксель движения мыши, доля расстояния до цели.</summary>
        private const double PanSpeed = 0.001;

        /// <summary>Вектор, задающий «верх» мира: ось Z станка.</summary>
        private static readonly Vector3D WorldUp = new Vector3D(0, 0, 1);

        /// <summary>Направление «верха» кадра, когда взгляд совпал с осью мира.</summary>
        private static readonly Vector3D FallbackUp = new Vector3D(0, 1, 0);

        /// <summary>Азимут и наклон в сферических координатах, радианы.</summary>
        private double _theta;
        private double _phi = Math.PI / 2;

        public OrbitCamera()
        {
            LookAt(new Point3D(0, 0, 0), 100);
        }

        /// <summary>Точка, вокруг которой вращается камера.</summary>
        public Point3D Target { get; private set; }

        /// <summary>Расстояние от камеры до цели.</summary>
        public double Distance { get; private set; }

        /// <summary>Положение камеры.</summary>
        public Point3D Position { get; private set; }

        /// <summary>Направление взгляда — от камеры к цели.</summary>
        public Vector3D LookDirection { get; private set; }

        /// <summary>Направление «верха» кадра.</summary>
        public Vector3D UpDirection { get; private set; } = FallbackUp;

        /// <summary>
        /// Ставит камеру над целью — вид сверху, с которого начинается
        /// просмотр программы.
        /// </summary>
        /// <param name="target">Точка, на которую смотрит камера.</param>
        /// <param name="distance">Расстояние до неё.</param>
        public void LookAt(Point3D target, double distance)
        {
            Target = target;
            Distance = Clamp(distance, MinDistance, MaxDistance);
            _theta = 0;
            _phi = Math.PI / 2;

            Position = new Point3D(target.X, target.Y, target.Z + Distance);
            LookDirection = new Vector3D(0, 0, -Distance);
            UpDirection = FallbackUp;
        }

        /// <summary>
        /// Приближает или отдаляет камеру, сохраняя направление взгляда.
        /// </summary>
        /// <param name="closer">Приблизить (иначе отдалить).</param>
        public void Zoom(bool closer)
        {
            Distance = Clamp(Distance * (closer ? 1.0 / ZoomStep : ZoomStep), MinDistance, MaxDistance);

            var look = LookDirection;
            if (look.Length < double.Epsilon)
                return;

            look.Normalize();
            Position = Target - look * Distance;
            LookDirection = Target - Position;
        }

        /// <summary>
        /// Вращает камеру вокруг указанной точки: горизонтальное движение
        /// мыши меняет азимут, вертикальное — наклон.
        /// </summary>
        /// <param name="deltaX">Смещение мыши по горизонтали, пиксели.</param>
        /// <param name="deltaY">Смещение мыши по вертикали, пиксели.</param>
        /// <param name="pivot">Точка, вокруг которой идёт вращение.</param>
        public void Orbit(double deltaX, double deltaY, Point3D pivot)
        {
            var radius = (Position - pivot).Length;
            if (radius < 0.001)
                return;

            _theta -= deltaX * RotationSpeed;
            _phi -= deltaY * RotationSpeed;

            // Наклон упирается в пределы, не доходя до полюсов: там «верх»
            // кадра неопределён, и сцена скачком переворачивается.
            _phi = Clamp(_phi, MinPolarAngle, Math.PI - MinPolarAngle);

            var offset = new Vector3D(
                radius * Math.Sin(_phi) * Math.Cos(_theta),
                radius * Math.Sin(_phi) * Math.Sin(_theta),
                radius * Math.Cos(_phi));

            Position = pivot + offset;
            LookDirection = pivot - Position;
            UpDirection = UpFor(offset);
        }

        /// <summary>
        /// Сдвигает камеру вместе с целью в плоскости кадра: сцена движется
        /// под курсором, а взгляд остаётся направленным туда же.
        /// </summary>
        /// <param name="deltaX">Смещение мыши по горизонтали, пиксели.</param>
        /// <param name="deltaY">Смещение мыши по вертикали, пиксели.</param>
        public void Pan(double deltaX, double deltaY)
        {
            var look = LookDirection;
            var up = UpDirection;
            if (look.Length < double.Epsilon || up.Length < double.Epsilon)
                return;

            look.Normalize();
            up.Normalize();

            var right = Vector3D.CrossProduct(look, up);
            if (right.Length < double.Epsilon)
                return;

            right.Normalize();
            var frameUp = Vector3D.CrossProduct(right, look);
            frameUp.Normalize();

            // Чем дальше камера, тем крупнее шаг: иначе на общем плане
            // панорамирование почти не двигает сцену.
            var speed = Distance * PanSpeed;
            var offset = right * (-deltaX * speed) + frameUp * (deltaY * speed);

            Position += offset;
            Target += offset;
            LookDirection = Target - Position;
        }

        /// <summary>
        /// «Верх» кадра, перпендикулярный взгляду. Когда взгляд совпадает с
        /// осью мира, перпендикуляр не определён — тогда берётся запасное
        /// направление, чтобы кадр не схлопнулся.
        /// </summary>
        /// <param name="offset">Вектор от точки вращения к камере.</param>
        private static Vector3D UpFor(Vector3D offset)
        {
            var right = Vector3D.CrossProduct(offset, WorldUp);
            if (right.Length <= 0.001)
                return FallbackUp;

            right.Normalize();
            var up = Vector3D.CrossProduct(right, offset);
            if (up.Length <= 0.001)
                return FallbackUp;

            up.Normalize();
            return up;
        }

        private static double Clamp(double value, double min, double max)
            => Math.Max(min, Math.Min(max, value));
    }
}
